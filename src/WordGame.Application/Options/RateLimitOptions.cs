namespace WordGame.Application.Options;

public sealed class RateLimitOptions
{
    public int CreateRoomPerMinute { get; set; } = 10;
    public int JoinPerMinute { get; set; } = 20;
    public int SubmitAnswerPerMinute { get; set; } = 120;
}
