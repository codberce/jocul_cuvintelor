using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WordGame.Application.DTOs;
using WordGame.Application.Interfaces;
using WordGame.Application.Options;
using WordGame.Application.Services;
using WordGame.Application.Subjects;
using WordGame.Domain.Entities;
using WordGame.Domain.Enums;
using WordGame.Domain.ValueObjects;

namespace WordGame.Infrastructure.Persistence;

public sealed class GameRoomService(
    ApplicationDbContext dbContext,
    IActiveRoomStore activeRoomStore,
    IDistributedLockProvider lockProvider,
    IQuestionSelector questionSelector,
    IRoomCodeGenerator roomCodeGenerator,
    IQrCodeService qrCodeService,
    IGuestSessionService guestSessionService,
    IAnswerNormalizer answerNormalizer,
    ILeaderboardService leaderboardService,
    IValidator<CreateRoomRequest> createRoomValidator,
    IValidator<JoinRoomRequest> joinRoomValidator,
    IValidator<SubmitAnswerRequest> submitAnswerValidator,
    IOptions<GameOptions> gameOptions,
    IOptions<RedisOptions> redisOptions,
    IOptions<SecurityOptions> securityOptions) : IGameRoomService
{
    private readonly GameOptions _gameOptions = gameOptions.Value;
    private readonly RedisOptions _redisOptions = redisOptions.Value;
    private readonly SecurityOptions _securityOptions = securityOptions.Value;

    public async Task<RoomCreatedDto> CreateRoomAsync(Guid hostUserId, CreateRoomRequest request, CancellationToken cancellationToken = default)
    {
        await createRoomValidator.ValidateAndThrowAsync(request, cancellationToken);
        var selectedSubjects = SubjectCatalog.NormalizeSelectedSubjects(request.SubjectSlugs);

        var roomCode = await roomCodeGenerator.GenerateAsync(async (code, ct) =>
            await activeRoomStore.ExistsAsync(code, ct) ||
            await dbContext.GameRooms.AnyAsync(x => x.RoomCode == code && x.Status != GameStatus.Expired, ct),
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var maxPlayers = Math.Min(request.MaxPlayers ?? _gameOptions.MaxPlayersPerRoom, _gameOptions.MaxPlayersPerRoom);
        var settings = new GameSettings
        {
            QuestionsPerGame = _gameOptions.QuestionsPerGame,
            DefaultQuestionTimeSeconds = _gameOptions.DefaultQuestionTimeSeconds,
            MaxPlayersPerRoom = _gameOptions.MaxPlayersPerRoom,
            SpeedBonusMaxPoints = _gameOptions.SpeedBonusMaxPoints,
            BasePointsPerLetter = _gameOptions.BasePointsPerLetter
        };

        var room = new GameRoom
        {
            RoomCode = roomCode,
            HostUserId = hostUserId,
            MaxPlayers = maxPlayers,
            SubjectSlugs = EncodeSubjectSlugs(selectedSubjects),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(_gameOptions.RoomExpirationMinutes),
            Settings = settings
        };

        dbContext.GameRooms.Add(room);
        AddEvent(room.Id, GameEventType.RoomCreated, new { roomCode, hostUserId });
        await dbContext.SaveChangesAsync(cancellationToken);

        var state = new ActiveRoomState
        {
            RoomId = room.Id,
            RoomCode = roomCode,
            HostUserId = hostUserId,
            Status = GameStatus.Lobby,
            CreatedAt = room.CreatedAt,
            ExpiresAt = room.ExpiresAt,
            Settings = settings,
            SubjectSlugs = selectedSubjects.ToList()
        };

        await activeRoomStore.SaveAsync(state, cancellationToken);

        return new RoomCreatedDto(room.Id, roomCode, qrCodeService.BuildJoinUrl(roomCode), qrCodeService.GenerateJoinQrDataUri(roomCode), selectedSubjects, room.ExpiresAt);
    }

    public async Task<JoinRoomResult> JoinRoomAsync(JoinRoomRequest request, string? connectionId, CancellationToken cancellationToken = default)
    {
        request = request with { DisplayName = SanitizeDisplayName(request.DisplayName) };
        await joinRoomValidator.ValidateAndThrowAsync(request, cancellationToken);

        await using var roomLock = await AcquireRoomLockAsync(request.RoomCode, cancellationToken);
        var state = await GetActiveStateOrThrowAsync(request.RoomCode, cancellationToken);

        if (state.ExpiresAt <= DateTimeOffset.UtcNow || state.Status != GameStatus.Lobby)
        {
            throw new InvalidOperationException("Camera nu mai accepta jucatori.");
        }

        if (state.Players.Count >= state.Settings.MaxPlayersPerRoom)
        {
            throw new InvalidOperationException("Camera este plina.");
        }

        var normalizedName = answerNormalizer.Normalize(request.DisplayName);
        if (state.Players.Any(x => answerNormalizer.Normalize(x.DisplayName) == normalizedName))
        {
            throw new InvalidOperationException("Numele este deja folosit in aceasta camera.");
        }

        var now = DateTimeOffset.UtcNow;
        var player = new Player
        {
            RoomId = state.RoomId,
            ConnectionId = connectionId,
            DisplayName = request.DisplayName,
            JoinedAt = now,
            LastSeenAt = now,
            Connected = connectionId is not null
        };

        dbContext.Players.Add(player);
        AddEvent(state.RoomId, GameEventType.PlayerJoined, new { player.DisplayName, player.Id });
        await dbContext.SaveChangesAsync(cancellationToken);

        state.Players.Add(new ActivePlayerState
        {
            PlayerId = player.Id,
            ConnectionId = connectionId,
            DisplayName = player.DisplayName,
            JoinedAt = player.JoinedAt,
            LastSeenAt = player.LastSeenAt,
            Connected = player.Connected
        });

        await activeRoomStore.SaveAsync(state, cancellationToken);

        if (!string.IsNullOrWhiteSpace(connectionId))
        {
            await activeRoomStore.SetConnectionMappingAsync(connectionId, new ConnectionMapping(state.RoomCode, player.Id), TimeSpan.FromMinutes(_securityOptions.GuestTokenLifetimeMinutes), cancellationToken);
        }

        var token = guestSessionService.CreateToken(state.RoomCode, player.Id, DateTimeOffset.UtcNow.AddMinutes(_securityOptions.GuestTokenLifetimeMinutes));
        return new JoinRoomResult(state.RoomId, state.RoomCode, player.Id, player.DisplayName, token);
    }

    public async Task<ReconnectResult> ReconnectPlayerAsync(string roomCode, string guestToken, string connectionId, CancellationToken cancellationToken = default)
    {
        if (!guestSessionService.TryValidateToken(guestToken, roomCode, out var playerId))
        {
            throw new UnauthorizedAccessException("Sesiunea jucatorului nu este valida.");
        }

        await using var roomLock = await AcquireRoomLockAsync(roomCode, cancellationToken);
        var state = await GetActiveStateOrThrowAsync(roomCode, cancellationToken);
        var player = state.Players.SingleOrDefault(x => x.PlayerId == playerId)
            ?? throw new UnauthorizedAccessException("Jucatorul nu apartine camerei.");

        player.ConnectionId = connectionId;
        player.Connected = true;
        player.LastSeenAt = DateTimeOffset.UtcNow;

        var dbPlayer = await dbContext.Players.FindAsync([playerId], cancellationToken);
        if (dbPlayer is not null)
        {
            dbPlayer.ConnectionId = connectionId;
            dbPlayer.Connected = true;
            dbPlayer.LastSeenAt = player.LastSeenAt;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await activeRoomStore.SetConnectionMappingAsync(connectionId, new ConnectionMapping(roomCode, playerId), TimeSpan.FromMinutes(_securityOptions.GuestTokenLifetimeMinutes), cancellationToken);
        await activeRoomStore.SaveAsync(state, cancellationToken);

        return new ReconnectResult(playerId, player.DisplayName, BuildSnapshot(state));
    }

    public async Task<RoomSnapshotDto> GetSnapshotAsync(string roomCode, CancellationToken cancellationToken = default)
    {
        return BuildSnapshot(await GetActiveStateOrThrowAsync(roomCode, cancellationToken));
    }

    public async Task<QuestionStartedDto> StartGameAsync(string roomCode, Guid hostUserId, CancellationToken cancellationToken = default)
    {
        await using var roomLock = await AcquireRoomLockAsync(roomCode, cancellationToken);
        var state = await GetActiveStateOrThrowAsync(roomCode, cancellationToken);
        EnsureHost(state, hostUserId);

        if (!GameStateMachine.CanStart(state.Status))
        {
            throw new InvalidOperationException("Jocul poate fi pornit doar din lobby.");
        }

        if (state.Players.Count == 0)
        {
            throw new InvalidOperationException("Este necesar cel putin un jucator pentru a porni jocul.");
        }

        state.Questions = (await questionSelector.SelectQuestionsAsync(state.Settings.QuestionsPerGame, state.SubjectSlugs, cancellationToken)).ToList();
        state.Status = GameStatus.Playing;
        state.CurrentQuestionIndex = 0;
        state.LastQuestionResult = null;
        StartCurrentQuestion(state);

        await UpdateRoomStatusAsync(state, cancellationToken);
        AddEvent(state.RoomId, GameEventType.GameStarted, new { state.RoomCode, state.Players.Count });
        await dbContext.SaveChangesAsync(cancellationToken);
        await activeRoomStore.SaveAsync(state, cancellationToken);

        return BuildQuestionStarted(state);
    }

    public async Task<QuestionHintDto> RequestLetterAsync(string roomCode, string guestToken, Guid questionId, CancellationToken cancellationToken = default)
    {
        if (!guestSessionService.TryValidateToken(guestToken, roomCode, out var playerId))
        {
            throw new UnauthorizedAccessException("Sesiunea jucatorului nu este valida.");
        }

        await using var roomLock = await AcquireRoomLockAsync(roomCode, cancellationToken);
        var state = await GetActiveStateOrThrowAsync(roomCode, cancellationToken);
        if (!GameStateMachine.CanSubmitAnswer(state.Status) || state.CurrentQuestionIndex < 0)
        {
            throw new InvalidOperationException("Nu exista o intrebare activa.");
        }

        if (state.Players.All(x => x.PlayerId != playerId))
        {
            throw new UnauthorizedAccessException("Jucatorul nu apartine camerei.");
        }

        var question = CurrentQuestion(state);
        if (question.QuestionId != questionId)
        {
            throw new InvalidOperationException("Litera ceruta nu corespunde intrebarii active.");
        }

        if (state.CurrentQuestionEndsAt is not null && DateTimeOffset.UtcNow >= state.CurrentQuestionEndsAt.Value)
        {
            throw new InvalidOperationException("Timpul a expirat. Nu mai poti cere litere.");
        }

        if (state.Answers.Any(x => x.PlayerId == playerId && x.QuestionId == question.QuestionId))
        {
            throw new InvalidOperationException("Nu poti cere litere dupa ce ai raspuns.");
        }

        var hint = GetHintState(state, playerId, question.QuestionId);
        var available = Enumerable.Range(0, question.NormalizedAnswer.Length)
            .Where(index => !hint.RevealedIndices.Contains(index))
            .ToList();

        if (available.Count > 0)
        {
            hint.RevealedIndices.Add(available[Random.Shared.Next(available.Count)]);
            await activeRoomStore.SaveAsync(state, cancellationToken);
        }

        return new QuestionHintDto(
            question.QuestionId,
            hint.RevealedIndices.ToDictionary(index => index, index => question.NormalizedAnswer[index]),
            PotentialScore(state, playerId, question));
    }

    public async Task<SubmitAnswerResult> SubmitAnswerAsync(SubmitAnswerRequest request, CancellationToken cancellationToken = default)
    {
        await submitAnswerValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (!guestSessionService.TryValidateToken(request.GuestToken, request.RoomCode, out var playerId))
        {
            throw new UnauthorizedAccessException("Sesiunea jucatorului nu este valida.");
        }

        await using var roomLock = await AcquireRoomLockAsync(request.RoomCode, cancellationToken);
        var state = await GetActiveStateOrThrowAsync(request.RoomCode, cancellationToken);

        if (!GameStateMachine.CanSubmitAnswer(state.Status) || state.CurrentQuestionIndex < 0)
        {
            throw new InvalidOperationException("Nu exista o intrebare activa.");
        }

        var player = state.Players.SingleOrDefault(x => x.PlayerId == playerId)
            ?? throw new UnauthorizedAccessException("Jucatorul nu apartine camerei.");

        var question = CurrentQuestion(state);
        if (question.QuestionId != request.QuestionId)
        {
            throw new InvalidOperationException("Raspunsul nu corespunde intrebarii active.");
        }

        var answeredCountBefore = state.Answers.Count(x => x.QuestionId == question.QuestionId);
        var existing = state.Answers.FirstOrDefault(x => x.PlayerId == playerId && x.QuestionId == question.QuestionId);
        if (existing is not null)
        {
            var accepted = existing.IdempotencyKey == request.IdempotencyKey;
            var feedback = new PlayerFeedbackDto(question.QuestionId, accepted, existing.IsCorrect, existing.ScoreAwarded, player.Score, accepted ? "Raspuns deja inregistrat." : "Ai trimis deja un raspuns pentru aceasta intrebare.");
            return new SubmitAnswerResult(feedback, new AnswerReceivedDto(playerId, answeredCountBefore, state.Players.Count), false);
        }

        var now = DateTimeOffset.UtcNow;
        if (state.CurrentQuestionEndsAt is not null && now > state.CurrentQuestionEndsAt.Value)
        {
            var feedback = new PlayerFeedbackDto(question.QuestionId, false, false, 0, player.Score, "Timpul a expirat.");
            return new SubmitAnswerResult(feedback, new AnswerReceivedDto(playerId, answeredCountBefore, state.Players.Count), true);
        }

        var normalizedSubmitted = answerNormalizer.Normalize(request.Answer);
        var isCorrect = normalizedSubmitted == question.NormalizedAnswer;
        var elapsedMs = (int)Math.Max(0, (now - state.CurrentQuestionStartedAt!.Value).TotalMilliseconds);
        var potentialScore = PotentialScore(state, playerId, question);
        var scoreAwarded = isCorrect ? potentialScore : -potentialScore;

        player.Score += scoreAwarded;
        player.LastSeenAt = now;
        player.Connected = true;

        var answer = new ActiveAnswerState
        {
            PlayerId = playerId,
            QuestionId = question.QuestionId,
            SubmittedAnswer = request.Answer.Trim(),
            NormalizedSubmittedAnswer = normalizedSubmitted,
            IsCorrect = isCorrect,
            ResponseTimeMs = elapsedMs,
            ScoreAwarded = scoreAwarded,
            SubmittedAt = now,
            IdempotencyKey = request.IdempotencyKey
        };
        state.Answers.Add(answer);

        dbContext.PlayerAnswers.Add(new PlayerAnswer
        {
            RoomId = state.RoomId,
            PlayerId = playerId,
            QuestionId = question.QuestionId,
            SubmittedAnswer = answer.SubmittedAnswer,
            NormalizedSubmittedAnswer = answer.NormalizedSubmittedAnswer,
            IsCorrect = isCorrect,
            ResponseTimeMs = elapsedMs,
            ScoreAwarded = scoreAwarded,
            SubmittedAt = now,
            IdempotencyKey = request.IdempotencyKey
        });

        var dbPlayer = await dbContext.Players.FindAsync([playerId], cancellationToken);
        if (dbPlayer is not null)
        {
            dbPlayer.Score = player.Score;
            dbPlayer.LastSeenAt = now;
            dbPlayer.Connected = true;
        }

        AddEvent(state.RoomId, GameEventType.AnswerReceived, new { playerId, question.QuestionId, isCorrect, scoreAwarded });
        await dbContext.SaveChangesAsync(cancellationToken);
        await activeRoomStore.SaveAsync(state, cancellationToken);

        var answeredCount = state.Answers.Count(x => x.QuestionId == question.QuestionId);
        var feedbackMessage = isCorrect ? "Corect!" : "Raspuns gresit. Punctajul cuvantului se scade.";
        var shouldEnd = answeredCount >= state.Players.Count;
        return new SubmitAnswerResult(
            new PlayerFeedbackDto(question.QuestionId, true, isCorrect, scoreAwarded, player.Score, feedbackMessage),
            new AnswerReceivedDto(playerId, answeredCount, state.Players.Count),
            shouldEnd);
    }

    public async Task<QuestionEndedDto> EndQuestionAsync(string roomCode, Guid? hostUserId, CancellationToken cancellationToken = default)
    {
        await using var roomLock = await AcquireRoomLockAsync(roomCode, cancellationToken);
        var state = await GetActiveStateOrThrowAsync(roomCode, cancellationToken);
        if (hostUserId.HasValue)
        {
            EnsureHost(state, hostUserId.Value);
        }

        if (state.Status == GameStatus.QuestionResults && state.LastQuestionResult is not null)
        {
            return state.LastQuestionResult;
        }

        if (!GameStateMachine.CanEndQuestion(state.Status) || state.CurrentQuestionIndex < 0)
        {
            throw new InvalidOperationException("Intrebarea nu poate fi inchisa in starea curenta.");
        }

        var result = BuildQuestionEnded(state);
        state.Status = GameStatus.QuestionResults;
        state.LastQuestionResult = result;
        state.CurrentQuestionEndsAt = DateTimeOffset.UtcNow;

        await UpdateRoomStatusAsync(state, cancellationToken);
        AddEvent(state.RoomId, GameEventType.QuestionEnded, new { result.QuestionId, result.CorrectCount });
        await dbContext.SaveChangesAsync(cancellationToken);
        await activeRoomStore.SaveAsync(state, cancellationToken);
        return result;
    }

    public async Task<QuestionStartedDto?> NextQuestionAsync(string roomCode, Guid hostUserId, CancellationToken cancellationToken = default)
    {
        await using var roomLock = await AcquireRoomLockAsync(roomCode, cancellationToken);
        var state = await GetActiveStateOrThrowAsync(roomCode, cancellationToken);
        EnsureHost(state, hostUserId);

        if (!GameStateMachine.CanAdvance(state.Status))
        {
            throw new InvalidOperationException("Urmatoarea intrebare poate fi pornita doar dupa afisarea rezultatelor.");
        }

        if (state.CurrentQuestionIndex + 1 >= state.Questions.Count)
        {
            await FinishGameInsideLockAsync(state, cancellationToken);
            return null;
        }

        state.CurrentQuestionIndex++;
        state.Status = GameStatus.Playing;
        state.LastQuestionResult = null;
        StartCurrentQuestion(state);

        await UpdateRoomStatusAsync(state, cancellationToken);
        AddEvent(state.RoomId, GameEventType.QuestionStarted, new { CurrentQuestion(state).QuestionId, state.CurrentQuestionIndex });
        await dbContext.SaveChangesAsync(cancellationToken);
        await activeRoomStore.SaveAsync(state, cancellationToken);
        return BuildQuestionStarted(state);
    }

    public async Task<GameEndedDto> EndGameAsync(string roomCode, Guid? hostUserId, CancellationToken cancellationToken = default)
    {
        await using var roomLock = await AcquireRoomLockAsync(roomCode, cancellationToken);
        var state = await GetActiveStateOrThrowAsync(roomCode, cancellationToken);
        if (hostUserId.HasValue)
        {
            EnsureHost(state, hostUserId.Value);
        }

        if (state.Status != GameStatus.Finished)
        {
            await FinishGameInsideLockAsync(state, cancellationToken);
        }

        return new GameEndedDto(roomCode, leaderboardService.BuildLeaderboard(state.Players), DateTimeOffset.UtcNow);
    }

    public async Task MarkPlayerDisconnectedAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        var mapping = await activeRoomStore.GetConnectionMappingAsync(connectionId, cancellationToken);
        if (mapping is null)
        {
            return;
        }

        await using var roomLock = await AcquireRoomLockAsync(mapping.RoomCode, cancellationToken);
        var state = await activeRoomStore.GetAsync(mapping.RoomCode, cancellationToken);
        if (state is null)
        {
            return;
        }

        var player = state.Players.SingleOrDefault(x => x.PlayerId == mapping.PlayerId);
        if (player is not null)
        {
            player.Connected = false;
            player.ConnectionId = null;
            player.LastSeenAt = DateTimeOffset.UtcNow;

            var dbPlayer = await dbContext.Players.FindAsync([player.PlayerId], cancellationToken);
            if (dbPlayer is not null)
            {
                dbPlayer.Connected = false;
                dbPlayer.ConnectionId = null;
                dbPlayer.LastSeenAt = player.LastSeenAt;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await activeRoomStore.SaveAsync(state, cancellationToken);
        }

        await activeRoomStore.RemoveConnectionMappingAsync(connectionId, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ExpireOldRoomsAsync(CancellationToken cancellationToken = default)
    {
        var expired = new List<string>();
        foreach (var code in await activeRoomStore.ListActiveRoomCodesAsync(cancellationToken))
        {
            var state = await activeRoomStore.GetAsync(code, cancellationToken);
            if (state is null)
            {
                await activeRoomStore.RemoveAsync(code, cancellationToken);
                continue;
            }

            if (state.ExpiresAt > DateTimeOffset.UtcNow || state.Status is GameStatus.Finished or GameStatus.Cancelled)
            {
                continue;
            }

            state.Status = GameStatus.Expired;
            var room = await dbContext.GameRooms.SingleOrDefaultAsync(x => x.Id == state.RoomId, cancellationToken);
            if (room is not null)
            {
                room.Status = GameStatus.Expired;
                AddEvent(room.Id, GameEventType.RoomExpired, new { code });
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await activeRoomStore.RemoveAsync(code, cancellationToken);
            expired.Add(code);
        }

        return expired;
    }

    private async Task<IDistributedLockHandle> AcquireRoomLockAsync(string roomCode, CancellationToken cancellationToken)
    {
        var expiry = TimeSpan.FromSeconds(_redisOptions.LockExpirySeconds);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var handle = await lockProvider.TryAcquireAsync($"room:{roomCode}", expiry, cancellationToken);
            if (handle is not null)
            {
                return handle;
            }

            await Task.Delay(50, cancellationToken);
        }

        throw new TimeoutException("Camera este ocupata. Reincercati.");
    }

    private async Task<ActiveRoomState> GetActiveStateOrThrowAsync(string roomCode, CancellationToken cancellationToken)
    {
        return await activeRoomStore.GetAsync(roomCode, cancellationToken)
            ?? throw new KeyNotFoundException("Camera nu exista sau a expirat.");
    }

    private async Task UpdateRoomStatusAsync(ActiveRoomState state, CancellationToken cancellationToken)
    {
        var room = await dbContext.GameRooms.SingleOrDefaultAsync(x => x.Id == state.RoomId, cancellationToken);
        if (room is null)
        {
            return;
        }

        room.Status = state.Status;
        room.CurrentQuestionIndex = state.CurrentQuestionIndex;
        room.CurrentQuestionStartedAt = state.CurrentQuestionStartedAt;
    }

    private async Task FinishGameInsideLockAsync(ActiveRoomState state, CancellationToken cancellationToken)
    {
        state.Status = GameStatus.Finished;
        var leaderboard = leaderboardService.BuildLeaderboard(state.Players);
        var finishedAt = DateTimeOffset.UtcNow;

        var room = await dbContext.GameRooms.SingleOrDefaultAsync(x => x.Id == state.RoomId, cancellationToken);
        if (room is not null)
        {
            room.Status = GameStatus.Finished;
            room.CurrentQuestionIndex = state.CurrentQuestionIndex;
        }

        var existingSession = await dbContext.GameSessions.FirstOrDefaultAsync(x => x.RoomCode == state.RoomCode, cancellationToken);
        if (existingSession is null)
        {
            var session = new GameSession
            {
                RoomId = state.RoomId,
                RoomCode = state.RoomCode,
                HostUserId = state.HostUserId,
                StartedAt = state.CreatedAt,
                FinishedAt = finishedAt,
                PlayerCount = state.Players.Count,
                QuestionCount = state.Questions.Count,
                SubjectSlugs = EncodeSubjectSlugs(state.SubjectSlugs),
                FinalLeaderboardJson = JsonSerializer.Serialize(leaderboard)
            };

            dbContext.GameSessions.Add(session);
            foreach (var entry in leaderboard)
            {
                dbContext.LeaderboardEntries.Add(new LeaderboardEntry
                {
                    GameSessionId = session.Id,
                    PlayerId = entry.PlayerId,
                    PlayerName = entry.PlayerName,
                    Rank = entry.Rank,
                    Score = entry.Score
                });
            }

            AddEvent(state.RoomId, GameEventType.GameEnded, new { state.RoomCode, leaderboard });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await activeRoomStore.SaveAsync(state, cancellationToken);
    }

    private RoomSnapshotDto BuildSnapshot(ActiveRoomState state)
    {
        var questionId = state.CurrentQuestionIndex >= 0 && state.CurrentQuestionIndex < state.Questions.Count
            ? state.Questions[state.CurrentQuestionIndex].QuestionId
            : Guid.Empty;

        var answeredCount = questionId == Guid.Empty
            ? 0
            : state.Answers.Count(x => x.QuestionId == questionId);

        return new RoomSnapshotDto(
            state.RoomId,
            state.RoomCode,
            state.HostUserId,
            state.Status,
            state.Players.Select(x => new PlayerDto(x.PlayerId, x.DisplayName, x.Score, x.Connected, x.JoinedAt, x.LastSeenAt)).ToList(),
            leaderboardService.BuildLeaderboard(state.Players),
            state.Status == GameStatus.Playing && state.CurrentQuestionIndex >= 0 ? BuildQuestionStarted(state) : null,
            state.LastQuestionResult,
            answeredCount,
            state.Players.Count,
            state.ExpiresAt);
    }

    private QuestionStartedDto BuildQuestionStarted(ActiveRoomState state)
    {
        var question = CurrentQuestion(state);
        return new QuestionStartedDto(
            question.QuestionId,
            state.CurrentQuestionIndex + 1,
            state.Questions.Count,
            question.Definition,
            question.Subject,
            question.Category,
            question.Difficulty,
            question.NormalizedAnswer.Count(char.IsLetterOrDigit),
            question.TimeLimitSeconds,
            state.CurrentQuestionStartedAt!.Value);
    }

    private QuestionEndedDto BuildQuestionEnded(ActiveRoomState state)
    {
        var question = CurrentQuestion(state);
        var answers = state.Answers.Where(x => x.QuestionId == question.QuestionId).ToList();
        var fastest = answers
            .Where(x => x.IsCorrect)
            .OrderBy(x => x.ResponseTimeMs)
            .FirstOrDefault();

        var fastestPlayer = fastest is null ? null : state.Players.SingleOrDefault(x => x.PlayerId == fastest.PlayerId)?.DisplayName;
        return new QuestionEndedDto(
            question.QuestionId,
            question.Answer,
            answers.Count(x => x.IsCorrect),
            state.Players.Count,
            fastestPlayer,
            fastest?.ResponseTimeMs,
            leaderboardService.BuildLeaderboard(state.Players));
    }

    private static ActiveQuestionState CurrentQuestion(ActiveRoomState state)
    {
        if (state.CurrentQuestionIndex < 0 || state.CurrentQuestionIndex >= state.Questions.Count)
        {
            throw new InvalidOperationException("Indexul intrebarii curente este invalid.");
        }

        return state.Questions[state.CurrentQuestionIndex];
    }

    private static ActiveLetterHintState GetHintState(ActiveRoomState state, Guid playerId, Guid questionId)
    {
        var hint = state.LetterHints.FirstOrDefault(x => x.PlayerId == playerId && x.QuestionId == questionId);
        if (hint is not null)
        {
            return hint;
        }

        hint = new ActiveLetterHintState { PlayerId = playerId, QuestionId = questionId };
        state.LetterHints.Add(hint);
        return hint;
    }

    private static int PotentialScore(ActiveRoomState state, Guid playerId, ActiveQuestionState question)
    {
        var revealedCount = state.LetterHints
            .FirstOrDefault(x => x.PlayerId == playerId && x.QuestionId == question.QuestionId)
            ?.RevealedIndices
            .Count ?? 0;

        return Math.Max(0, question.NormalizedAnswer.Count(char.IsLetterOrDigit) * 100 - revealedCount * 100);
    }

    private static void StartCurrentQuestion(ActiveRoomState state)
    {
        var now = DateTimeOffset.UtcNow;
        var question = CurrentQuestion(state);
        state.CurrentQuestionStartedAt = now;
        state.CurrentQuestionEndsAt = now.AddSeconds(question.TimeLimitSeconds);
    }

    private static void EnsureHost(ActiveRoomState state, Guid hostUserId)
    {
        if (state.HostUserId != hostUserId)
        {
            throw new UnauthorizedAccessException("Doar gazda camerei poate executa aceasta actiune.");
        }
    }

    private static string SanitizeDisplayName(string value)
    {
        var clean = new string(value
            .Where(c => !char.IsControl(c) && c is not '<' and not '>')
            .ToArray());

        return string.Join(' ', clean.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Trim();
    }

    private static string EncodeSubjectSlugs(IEnumerable<string> subjectSlugs)
    {
        return string.Join(',', subjectSlugs.Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).Distinct().OrderBy(x => x));
    }

    private void AddEvent(Guid roomId, GameEventType type, object payload)
    {
        dbContext.GameEvents.Add(new GameEvent
        {
            RoomId = roomId,
            Type = type,
            PayloadJson = JsonSerializer.Serialize(payload)
        });
    }
}
