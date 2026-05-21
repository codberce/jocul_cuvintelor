namespace WordGame.Domain.Entities;

public sealed class GameSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public Guid HostUserId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public int PlayerCount { get; set; }
    public int QuestionCount { get; set; }
    public string SubjectSlugs { get; set; } = string.Empty;
    public string FinalLeaderboardJson { get; set; } = "[]";
}
