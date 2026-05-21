namespace WordGame.Application.Options;

public sealed class SecurityOptions
{
    public string AdminEmail { get; set; } = "admin@wordgame.local";
    public string AdminPassword { get; set; } = "ChangeMe123!";
    public string HostEmail { get; set; } = "host@wordgame.local";
    public string HostPassword { get; set; } = "ChangeMe123!";
    public int GuestTokenLifetimeMinutes { get; set; } = 180;
}
