namespace WordGame.Application.Interfaces;

public interface IDistributedLockProvider
{
    Task<IDistributedLockHandle?> TryAcquireAsync(string resource, TimeSpan expiry, CancellationToken cancellationToken = default);
}

public interface IDistributedLockHandle : IAsyncDisposable
{
    string Resource { get; }
}
