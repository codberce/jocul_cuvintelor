using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using WordGame.Application.DTOs;
using WordGame.Application.Interfaces;
using WordGame.Application.Options;

namespace WordGame.Infrastructure.Redis;

public sealed class RedisActiveRoomStore(IConnectionMultiplexer redis, IOptions<RedisOptions> options) : IActiveRoomStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDatabase _database = redis.GetDatabase();
    private readonly RedisOptions _options = options.Value;

    public async Task<bool> ExistsAsync(string roomCode, CancellationToken cancellationToken = default)
    {
        return await _database.KeyExistsAsync(RoomKey(roomCode));
    }

    public async Task<ActiveRoomState?> GetAsync(string roomCode, CancellationToken cancellationToken = default)
    {
        var json = await _database.StringGetAsync(RoomKey(roomCode));
        return json.IsNullOrEmpty ? null : JsonSerializer.Deserialize<ActiveRoomState>(json.ToString(), JsonOptions);
    }

    public async Task SaveAsync(ActiveRoomState state, CancellationToken cancellationToken = default)
    {
        var ttl = state.ExpiresAt - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            ttl = TimeSpan.FromMinutes(5);
        }

        var json = JsonSerializer.Serialize(state, JsonOptions);
        await _database.StringSetAsync(RoomKey(state.RoomCode), json, ttl);
        await _database.SetAddAsync(ActiveRoomsKey(), state.RoomCode);
    }

    public async Task RemoveAsync(string roomCode, CancellationToken cancellationToken = default)
    {
        await _database.KeyDeleteAsync(RoomKey(roomCode));
        await _database.SetRemoveAsync(ActiveRoomsKey(), roomCode);
    }

    public async Task<IReadOnlyList<string>> ListActiveRoomCodesAsync(CancellationToken cancellationToken = default)
    {
        var values = await _database.SetMembersAsync(ActiveRoomsKey());
        return values.Select(x => x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
    }

    public async Task SetConnectionMappingAsync(string connectionId, ConnectionMapping mapping, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(mapping, JsonOptions);
        await _database.StringSetAsync(ConnectionKey(connectionId), json, ttl);
    }

    public async Task<ConnectionMapping?> GetConnectionMappingAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        var json = await _database.StringGetAsync(ConnectionKey(connectionId));
        return json.IsNullOrEmpty ? null : JsonSerializer.Deserialize<ConnectionMapping>(json.ToString(), JsonOptions);
    }

    public async Task RemoveConnectionMappingAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        await _database.KeyDeleteAsync(ConnectionKey(connectionId));
    }

    private string RoomKey(string roomCode) => $"{_options.InstanceName}room:{roomCode}";
    private string ConnectionKey(string connectionId) => $"{_options.InstanceName}connection:{connectionId}";
    private string ActiveRoomsKey() => $"{_options.InstanceName}rooms:active";
}
