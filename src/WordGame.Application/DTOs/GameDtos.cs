using WordGame.Domain.Enums;

namespace WordGame.Application.DTOs;

public sealed record LeaderboardEntryDto(Guid PlayerId, string PlayerName, int Rank, int Score);

public sealed record PlayerDto(
    Guid PlayerId,
    string DisplayName,
    int Score,
    bool Connected,
    DateTimeOffset JoinedAt,
    DateTimeOffset LastSeenAt);

public sealed record RoomCreatedDto(
    Guid RoomId,
    string RoomCode,
    string JoinUrl,
    string QrCodeDataUri,
    IReadOnlyList<string> SubjectSlugs,
    DateTimeOffset ExpiresAt);

public sealed record QuestionStartedDto(
    Guid QuestionId,
    int QuestionIndex,
    int TotalQuestions,
    string Definition,
    string Subject,
    string Category,
    Difficulty Difficulty,
    int LetterCount,
    int TimeLimitSeconds,
    DateTimeOffset ServerStartedAtUtc);

public sealed record QuestionEndedDto(
    Guid QuestionId,
    string CorrectAnswer,
    int CorrectCount,
    int TotalPlayers,
    string? FastestCorrectPlayerName,
    int? FastestCorrectResponseMs,
    IReadOnlyList<LeaderboardEntryDto> Leaderboard);

public sealed record PlayerFeedbackDto(
    Guid QuestionId,
    bool Accepted,
    bool IsCorrect,
    int ScoreAwarded,
    int TotalScore,
    string Message);

public sealed record QuestionHintDto(
    Guid QuestionId,
    IReadOnlyDictionary<int, char> RevealedLetters,
    int PotentialScore);

public sealed record AnswerReceivedDto(Guid PlayerId, int AnsweredCount, int TotalPlayers);

public sealed record JoinRoomResult(
    Guid RoomId,
    string RoomCode,
    Guid PlayerId,
    string DisplayName,
    string GuestToken);

public sealed record ReconnectResult(
    Guid PlayerId,
    string DisplayName,
    RoomSnapshotDto Snapshot);

public sealed record RoomSnapshotDto(
    Guid RoomId,
    string RoomCode,
    Guid HostUserId,
    GameStatus Status,
    IReadOnlyList<PlayerDto> Players,
    IReadOnlyList<LeaderboardEntryDto> Leaderboard,
    QuestionStartedDto? CurrentQuestion,
    QuestionEndedDto? LastQuestionResult,
    int AnsweredCount,
    int TotalPlayers,
    DateTimeOffset ExpiresAt);

public sealed record SubmitAnswerResult(
    PlayerFeedbackDto Feedback,
    AnswerReceivedDto AnswerReceived,
    bool ShouldAutoEndQuestion);

public sealed record GameEndedDto(
    string RoomCode,
    IReadOnlyList<LeaderboardEntryDto> Leaderboard,
    DateTimeOffset FinishedAtUtc);
