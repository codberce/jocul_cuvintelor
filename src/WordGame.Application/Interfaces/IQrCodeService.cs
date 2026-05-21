namespace WordGame.Application.Interfaces;

public interface IQrCodeService
{
    string BuildJoinUrl(string roomCode);
    string GenerateJoinQrDataUri(string roomCode);
}
