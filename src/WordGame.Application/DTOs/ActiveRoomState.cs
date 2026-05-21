using WordGame.Domain.Enums;
using WordGame.Domain.ValueObjects;

namespace WordGame.Application.DTOs;

public sealed class ActiveRoomState
{
    public Guid RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public Guid HostUserId { get; set; }
    public GameStatus Status { get; set; } = GameStatus.Lobby;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public int CurrentQuestionIndex { get; set; } = -1;
    public DateTimeOffset? CurrentQuestionStartedAt { get; set; }
    public DateTimeOffset? CurrentQuestionEndsAt { get; set; }
    public GameSettings Settings { get; set; } = new();
    public List<string> SubjectSlugs { get; set; } = [];
    public List<ActivePlayerState> Players { get; set; } = [];
    public List<ActiveQuestionState> Questions { get; set; } = [];
    public List<ActiveAnswerState> Answers { get; set; } = [];
    public List<ActiveLetterHintState> LetterHints { get; set; } = [];
    public QuestionEndedDto? LastQuestionResult { get; set; }
}

public sealed class ActivePlayerState
{
    public Guid PlayerId { get; set; }
    public string? ConnectionId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int Score { get; set; }
    public bool Connected { get; set; } = true;
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ActiveQuestionState
{
    public Guid QuestionId { get; set; }
    public string Answer { get; set; } = string.Empty;
    public string NormalizedAnswer { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string SubjectSlug { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Difficulty Difficulty { get; set; }
    public int TimeLimitSeconds { get; set; }
}

public sealed class ActiveAnswerState
{
    public Guid PlayerId { get; set; }
    public Guid QuestionId { get; set; }
    public string SubmittedAnswer { get; set; } = string.Empty;
    public string NormalizedSubmittedAnswer { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int ResponseTimeMs { get; set; }
    public int ScoreAwarded { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class ActiveLetterHintState
{
    public Guid PlayerId { get; set; }
    public Guid QuestionId { get; set; }
    public List<int> RevealedIndices { get; set; } = [];
}

public sealed record ConnectionMapping(string RoomCode, Guid PlayerId);
