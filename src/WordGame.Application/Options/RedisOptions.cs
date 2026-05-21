namespace WordGame.Application.Options;

public sealed class RedisOptions
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public string InstanceName { get; set; } = "wordgame:";
    public int LockExpirySeconds { get; set; } = 10;
}
