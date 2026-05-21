using WordGame.Domain.Enums;
using WordGame.Domain.ValueObjects;

namespace WordGame.Domain.Entities;

public sealed class GameRoom
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RoomCode { get; set; } = string.Empty;
    public Guid HostUserId { get; set; }
    public GameStatus Status { get; set; } = GameStatus.Lobby;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public int MaxPlayers { get; set; }
    public string SubjectSlugs { get; set; } = string.Empty;
    public int CurrentQuestionIndex { get; set; } = -1;
    public DateTimeOffset? CurrentQuestionStartedAt { get; set; }
    public GameSettings Settings { get; set; } = new();

    public List<Player> Players { get; set; } = [];
    public List<PlayerAnswer> Answers { get; set; } = [];
    public List<GameEvent> Events { get; set; } = [];
}
