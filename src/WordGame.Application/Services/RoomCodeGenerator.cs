using System.Security.Cryptography;
using WordGame.Application.Interfaces;

namespace WordGame.Application.Services;

public sealed class RoomCodeGenerator : IRoomCodeGenerator
{
    public async Task<string> GenerateAsync(Func<string, CancellationToken, Task<bool>> existsAsync, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < 64; attempt++)
        {
            var number = RandomNumberGenerator.GetInt32(0, 1_000_000);
            var code = number.ToString("D6");
            if (!await existsAsync(code, cancellationToken))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Nu s-a putut genera un cod de camera unic.");
    }
}
