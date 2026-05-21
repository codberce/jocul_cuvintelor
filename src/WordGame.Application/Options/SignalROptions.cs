namespace WordGame.Application.Options;

public sealed class SignalROptions
{
    public bool UseRedisBackplane { get; set; } = true;
    public bool UseAzureSignalR { get; set; }
    public string? AzureSignalRConnectionString { get; set; }
}
