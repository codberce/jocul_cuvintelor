namespace WordGame.Application.Interfaces;

public interface IGuestSessionService
{
    string CreateToken(string roomCode, Guid playerId, DateTimeOffset expiresAt);
    bool TryValidateToken(string token, string roomCode, out Guid playerId);
}
