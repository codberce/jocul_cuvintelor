using Microsoft.Extensions.Options;
using QRCoder;
using WordGame.Application.Interfaces;
using WordGame.Application.Options;

namespace WordGame.Infrastructure.Qr;

public sealed class QrCodeService(IOptions<QrOptions> options) : IQrCodeService
{
    private readonly QrOptions _options = options.Value;

    public string BuildJoinUrl(string roomCode)
    {
        var baseUrl = _options.PublicBaseUrl.TrimEnd('/');
        return $"{baseUrl}/join/{roomCode}";
    }

    public string GenerateJoinQrDataUri(string roomCode)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(BuildJoinUrl(roomCode), QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data);
        var bytes = qr.GetGraphic(_options.PixelsPerModule);
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }
}
