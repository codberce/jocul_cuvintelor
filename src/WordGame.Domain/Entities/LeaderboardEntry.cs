namespace WordGame.Domain.Entities;

public sealed class LeaderboardEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameSessionId { get; set; }
    public GameSession? GameSession { get; set; }
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int Rank { get; set; }
    public int Score { get; set; }
}
