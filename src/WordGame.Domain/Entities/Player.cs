namespace WordGame.Domain.Entities;

public sealed class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public GameRoom? Room { get; set; }
    public string? ConnectionId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int Score { get; set; }
    public bool Connected { get; set; } = true;
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
}
