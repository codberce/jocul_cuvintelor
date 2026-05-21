using Microsoft.Extensions.Options;
using StackExchange.Redis;
using WordGame.Application.Interfaces;
using WordGame.Application.Options;

namespace WordGame.Infrastructure.Redis;

public sealed class RedisDistributedLockProvider(IConnectionMultiplexer redis, IOptions<RedisOptions> options) : IDistributedLockProvider
{
    private readonly IDatabase _database = redis.GetDatabase();
    private readonly RedisOptions _options = options.Value;

    public async Task<IDistributedLockHandle?> TryAcquireAsync(string resource, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var token = Guid.NewGuid().ToString("N");
        var key = LockKey(resource);
        var acquired = await _database.StringSetAsync(key, token, expiry, When.NotExists);
        return acquired ? new RedisDistributedLockHandle(_database, key, resource, token) : null;
    }

    private string LockKey(string resource) => $"{_options.InstanceName}lock:{resource}";

    private sealed class RedisDistributedLockHandle(IDatabase database, string key, string resource, string token) : IDistributedLockHandle
    {
        private const string ReleaseScript = """
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end
            """;

        public string Resource { get; } = resource;

        public async ValueTask DisposeAsync()
        {
            await database.ScriptEvaluateAsync(ReleaseScript, [key], [token]);
        }
    }
}
