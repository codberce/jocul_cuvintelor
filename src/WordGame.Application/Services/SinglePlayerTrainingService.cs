using Microsoft.Extensions.Options;
using WordGame.Application.DTOs;
using WordGame.Application.Interfaces;
using WordGame.Application.Options;
using WordGame.Application.Subjects;

namespace WordGame.Application.Services;

public sealed class SinglePlayerTrainingService(
    IQuestionSelector questionSelector,
    IAnswerNormalizer answerNormalizer,
    IOptions<GameOptions> options) : ISinglePlayerTrainingService
{
    private readonly GameOptions _options = options.Value;
    private SinglePlayerSessionState? _session;

    public async Task<SinglePlayerSessionDto> StartSessionAsync(StartTrainingRequest request, CancellationToken cancellationToken = default)
    {
        var selectedSubjects = SubjectCatalog.NormalizeSelectedSubjects(request.SubjectSlugs);
        if (selectedSubjects.Count == 0)
        {
            throw new InvalidOperationException("Selecteaza cel putin o materie.");
        }

        var questions = (await questionSelector.SelectQuestionsAsync(_options.QuestionsPerGame, selectedSubjects, cancellationToken)).ToList();
        if (questions.Count == 0)
        {
            throw new InvalidOperationException("Nu exista intrebari active pentru antrenament.");
        }

        var now = DateTimeOffset.UtcNow;
        _session = new SinglePlayerSessionState
        {
            SessionId = Guid.NewGuid(),
            Questions = questions,
            SubjectSlugs = selectedSubjects.ToList(),
            CurrentQuestionIndex = 0,
            TotalGameTimeSeconds = Math.Max(0, _options.TotalGameTimeSeconds),
            SessionStartedAt = now,
            CurrentQuestionStartedAt = now
        };

        return BuildSessionDto(_session);
    }

    public SinglePlayerSessionDto? GetCurrentSession() => _session is null ? null : BuildSessionDto(_session);

    public SinglePlayerSessionDto RequestLetter(Guid sessionId)
    {
        var session = GetSessionOrThrow(sessionId);
        if (session.IsFinished || IsTotalTimeExpired(session))
        {
            session.IsFinished = true;
            return BuildSessionDto(session);
        }

        var current = CurrentQuestion(session);
        if (session.Answers.Any(x => x.QuestionId == current.QuestionId))
        {
            throw new InvalidOperationException("Nu poti cere litere dupa ce ai raspuns.");
        }

        var revealed = GetRevealedLetters(session, current.QuestionId);
        var available = Enumerable.Range(0, current.NormalizedAnswer.Length)
            .Where(index => !revealed.Contains(index))
            .ToList();

        if (available.Count == 0)
        {
            return BuildSessionDto(session);
        }

        revealed.Add(available[Random.Shared.Next(available.Count)]);
        return BuildSessionDto(session);
    }

    public Task<SinglePlayerFeedbackDto> SubmitAnswerAsync(Guid sessionId, Guid questionId, string answer, CancellationToken cancellationToken = default)
    {
        var session = GetSessionOrThrow(sessionId);
        if (session.IsFinished)
        {
            throw new InvalidOperationException("Sesiunea de antrenament este terminata.");
        }

        var current = CurrentQuestion(session);
        if (current.QuestionId != questionId)
        {
            throw new InvalidOperationException("Raspunsul nu corespunde intrebarii curente.");
        }

        var existing = session.Answers.FirstOrDefault(x => x.QuestionId == questionId);
        if (existing is not null)
        {
            return Task.FromResult(existing.Feedback with { Accepted = false, Message = "Ai raspuns deja la aceasta intrebare." });
        }

        var now = DateTimeOffset.UtcNow;
        var totalMs = session.TotalGameTimeSeconds * 1000;
        var elapsedTotalMs = (int)Math.Max(0, (now - session.SessionStartedAt).TotalMilliseconds);
        var responseTimeMs = (int)Math.Max(0, (now - session.CurrentQuestionStartedAt).TotalMilliseconds);
        var expired = elapsedTotalMs >= totalMs;
        var normalizedSubmitted = answerNormalizer.Normalize(answer);
        var isCorrect = !expired && normalizedSubmitted == current.NormalizedAnswer;
        var remainingMs = Math.Max(0, totalMs - elapsedTotalMs);
        var potentialScore = PotentialScore(session, current);
        var scoreAwarded = isCorrect ? potentialScore : -potentialScore;

        session.Score += scoreAwarded;
        if (isCorrect)
        {
            session.CorrectCount++;
        }
        else
        {
            session.WrongCount++;
        }

        var feedback = new SinglePlayerFeedbackDto(
            current.QuestionId,
            true,
            isCorrect,
            scoreAwarded,
            session.Score,
            current.Answer,
            Math.Min(responseTimeMs, totalMs),
            expired ? "Timpul a expirat." : isCorrect ? "Corect!" : "Raspuns gresit. Punctajul cuvantului se scade.");

        session.Answers.Add(new SinglePlayerAnswerState(questionId, feedback));
        if (expired)
        {
            session.IsFinished = true;
        }

        return Task.FromResult(feedback);
    }

    public SinglePlayerSessionDto NextQuestion(Guid sessionId)
    {
        var session = GetSessionOrThrow(sessionId);
        if (session.IsFinished)
        {
            return BuildSessionDto(session);
        }

        if (!session.Answers.Any(x => x.QuestionId == CurrentQuestion(session).QuestionId))
        {
            throw new InvalidOperationException("Raspunde la intrebarea curenta inainte de a continua.");
        }

        if (IsTotalTimeExpired(session) || session.CurrentQuestionIndex + 1 >= session.Questions.Count)
        {
            session.IsFinished = true;
            return BuildSessionDto(session);
        }

        session.CurrentQuestionIndex++;
        session.CurrentQuestionStartedAt = DateTimeOffset.UtcNow;
        return BuildSessionDto(session);
    }

    private SinglePlayerSessionState GetSessionOrThrow(Guid sessionId)
    {
        if (_session is null || _session.SessionId != sessionId)
        {
            throw new InvalidOperationException("Sesiunea de antrenament nu exista.");
        }

        return _session;
    }

    private static ActiveQuestionState CurrentQuestion(SinglePlayerSessionState session)
    {
        return session.Questions[session.CurrentQuestionIndex];
    }

    private static HashSet<int> GetRevealedLetters(SinglePlayerSessionState session, Guid questionId)
    {
        if (!session.RevealedLetters.TryGetValue(questionId, out var revealed))
        {
            revealed = [];
            session.RevealedLetters[questionId] = revealed;
        }

        return revealed;
    }

    private static int PotentialScore(SinglePlayerSessionState session, ActiveQuestionState question)
    {
        var revealedCount = session.RevealedLetters.TryGetValue(question.QuestionId, out var revealed)
            ? revealed.Count
            : 0;

        return Math.Max(0, question.NormalizedAnswer.Count(char.IsLetterOrDigit) * 100 - revealedCount * 100);
    }

    private static IReadOnlyDictionary<int, char> RevealedLetterMap(SinglePlayerSessionState session, ActiveQuestionState question)
    {
        return session.RevealedLetters.TryGetValue(question.QuestionId, out var revealed)
            ? revealed.ToDictionary(index => index, index => question.NormalizedAnswer[index])
            : new Dictionary<int, char>();
    }

    private static bool IsTotalTimeExpired(SinglePlayerSessionState session)
    {
        return DateTimeOffset.UtcNow >= session.SessionStartedAt.AddSeconds(session.TotalGameTimeSeconds);
    }

    private static SinglePlayerSessionDto BuildSessionDto(SinglePlayerSessionState session)
    {
        if (session.IsFinished || IsTotalTimeExpired(session))
        {
            session.IsFinished = true;
            return new SinglePlayerSessionDto(
                session.SessionId,
                SinglePlayerSessionStatus.Finished,
                session.Score,
                session.CorrectCount,
                session.WrongCount,
                session.SubjectSlugs,
                null,
                session.Answers.LastOrDefault()?.Feedback,
                BuildSummary(session));
        }

        var current = CurrentQuestion(session);
        var answered = session.Answers.FirstOrDefault(x => x.QuestionId == current.QuestionId)?.Feedback;
        var status = answered is null ? SinglePlayerSessionStatus.InProgress : SinglePlayerSessionStatus.Answered;

        return new SinglePlayerSessionDto(
            session.SessionId,
            status,
            session.Score,
            session.CorrectCount,
            session.WrongCount,
            session.SubjectSlugs,
            new SinglePlayerQuestionDto(
                current.QuestionId,
                session.CurrentQuestionIndex + 1,
                session.Questions.Count,
                current.Definition,
                current.Subject,
                current.Category,
                current.Difficulty,
                current.NormalizedAnswer.Count(char.IsLetterOrDigit),
                session.TotalGameTimeSeconds,
                session.SessionStartedAt,
                RevealedLetterMap(session, current),
                PotentialScore(session, current)),
            answered,
            null);
    }

    private static SinglePlayerSummaryDto BuildSummary(SinglePlayerSessionState session)
    {
        var averageResponse = session.Answers.Count == 0
            ? 0
            : (int)Math.Round(session.Answers.Average(x => x.Feedback.ResponseTimeMs), MidpointRounding.AwayFromZero);

        return new SinglePlayerSummaryDto(
            session.Score,
            session.CorrectCount,
            session.WrongCount,
            session.Questions.Count,
            averageResponse);
    }

    private sealed class SinglePlayerSessionState
    {
        public Guid SessionId { get; init; }
        public List<ActiveQuestionState> Questions { get; init; } = [];
        public List<string> SubjectSlugs { get; init; } = [];
        public int CurrentQuestionIndex { get; set; }
        public int TotalGameTimeSeconds { get; init; }
        public DateTimeOffset SessionStartedAt { get; set; }
        public DateTimeOffset CurrentQuestionStartedAt { get; set; }
        public int Score { get; set; }
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public bool IsFinished { get; set; }
        public List<SinglePlayerAnswerState> Answers { get; } = [];
        public Dictionary<Guid, HashSet<int>> RevealedLetters { get; } = [];
    }

    private sealed record SinglePlayerAnswerState(Guid QuestionId, SinglePlayerFeedbackDto Feedback);
}
