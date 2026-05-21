namespace WordGame.Application.Interfaces;

public interface IRoomCodeGenerator
{
    Task<string> GenerateAsync(Func<string, CancellationToken, Task<bool>> existsAsync, CancellationToken cancellationToken = default);
}
