namespace WordGame.Application.Options;

public sealed class QrOptions
{
    public string PublicBaseUrl { get; set; } = "http://localhost:8080";
    public int PixelsPerModule { get; set; } = 12;
}
