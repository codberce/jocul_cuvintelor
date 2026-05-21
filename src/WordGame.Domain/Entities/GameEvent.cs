using WordGame.Domain.Enums;

namespace WordGame.Domain.Entities;

public sealed class GameEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public GameRoom? Room { get; set; }
    public GameEventType Type { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string PayloadJson { get; set; } = "{}";
}
