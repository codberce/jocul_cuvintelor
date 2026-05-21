using WordGame.Domain.Enums;

namespace WordGame.Domain.Entities;

public sealed class Question
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Answer { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public Guid? SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
    public Difficulty Difficulty { get; set; } = Difficulty.Medium;
    public int TimeLimitSeconds { get; set; } = 25;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
