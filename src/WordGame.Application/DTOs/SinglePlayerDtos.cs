using WordGame.Domain.Enums;

namespace WordGame.Application.DTOs;

public enum SinglePlayerSessionStatus
{
    NotStarted = 0,
    InProgress = 1,
    Answered = 2,
    Finished = 3
}

public sealed record SinglePlayerQuestionDto(
    Guid QuestionId,
    int QuestionIndex,
    int TotalQuestions,
    string Definition,
    string Subject,
    string Category,
    Difficulty Difficulty,
    int LetterCount,
    int TimeLimitSeconds,
    DateTimeOffset ServerStartedAtUtc,
    IReadOnlyDictionary<int, char> RevealedLetters,
    int PotentialScore);

public sealed record SinglePlayerFeedbackDto(
    Guid QuestionId,
    bool Accepted,
    bool IsCorrect,
    int ScoreAwarded,
    int TotalScore,
    string CorrectAnswer,
    int ResponseTimeMs,
    string Message);

public sealed record SinglePlayerSummaryDto(
    int TotalScore,
    int CorrectCount,
    int WrongCount,
    int TotalQuestions,
    int AverageResponseTimeMs);

public sealed record SinglePlayerSessionDto(
    Guid SessionId,
    SinglePlayerSessionStatus Status,
    int Score,
    int CorrectCount,
    int WrongCount,
    IReadOnlyList<string> SubjectSlugs,
    SinglePlayerQuestionDto? CurrentQuestion,
    SinglePlayerFeedbackDto? LastFeedback,
    SinglePlayerSummaryDto? Summary);
