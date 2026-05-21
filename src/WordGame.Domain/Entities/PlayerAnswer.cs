namespace WordGame.Domain.Entities;

public sealed class PlayerAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public GameRoom? Room { get; set; }
    public Guid PlayerId { get; set; }
    public Player? Player { get; set; }
    public Guid QuestionId { get; set; }
    public Question? Question { get; set; }
    public string SubmittedAnswer { get; set; } = string.Empty;
    public string NormalizedSubmittedAnswer { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int ResponseTimeMs { get; set; }
    public int ScoreAwarded { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public string IdempotencyKey { get; set; } = string.Empty;
}
