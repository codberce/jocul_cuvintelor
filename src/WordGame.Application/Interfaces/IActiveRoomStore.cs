using WordGame.Application.DTOs;

namespace WordGame.Application.Interfaces;

public interface IActiveRoomStore
{
    Task<bool> ExistsAsync(string roomCode, CancellationToken cancellationToken = default);
    Task<ActiveRoomState?> GetAsync(string roomCode, CancellationToken cancellationToken = default);
    Task SaveAsync(ActiveRoomState state, CancellationToken cancellationToken = default);
    Task RemoveAsync(string roomCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListActiveRoomCodesAsync(CancellationToken cancellationToken = default);
    Task SetConnectionMappingAsync(string connectionId, ConnectionMapping mapping, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task<ConnectionMapping?> GetConnectionMappingAsync(string connectionId, CancellationToken cancellationToken = default);
    Task RemoveConnectionMappingAsync(string connectionId, CancellationToken cancellationToken = default);
}
