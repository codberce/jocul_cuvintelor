namespace WordGame.Application.Options;

public sealed class GuestSessionOptions
{
    public int GuestTokenLifetimeMinutes { get; set; } = 180;
}
