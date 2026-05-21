using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using WordGame.Application.Interfaces;

namespace WordGame.Infrastructure.Identity;

public sealed class GuestSessionService(IDataProtectionProvider dataProtectionProvider) : IGuestSessionService
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("WordGame.PlayerGuestToken.v1");

    public string CreateToken(string roomCode, Guid playerId, DateTimeOffset expiresAt)
    {
        var payload = new GuestTokenPayload(roomCode, playerId, expiresAt);
        return _protector.Protect(JsonSerializer.Serialize(payload));
    }

    public bool TryValidateToken(string token, string roomCode, out Guid playerId)
    {
        playerId = Guid.Empty;
        try
        {
            var json = _protector.Unprotect(token);
            var payload = JsonSerializer.Deserialize<GuestTokenPayload>(json);
            if (payload is null || payload.RoomCode != roomCode || payload.ExpiresAt < DateTimeOffset.UtcNow)
            {
                return false;
            }

            playerId = payload.PlayerId;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed record GuestTokenPayload(string RoomCode, Guid PlayerId, DateTimeOffset ExpiresAt);
}
