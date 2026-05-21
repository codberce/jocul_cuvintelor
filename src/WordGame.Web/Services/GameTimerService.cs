using Microsoft.AspNetCore.SignalR;
using WordGame.Application.Interfaces;
using WordGame.Domain.Enums;
using WordGame.Web.Hubs;

namespace WordGame.Web.Services;

public sealed class GameTimerService(
    IServiceScopeFactory scopeFactory,
    IHubContext<GameHub> hubContext,
    ILogger<GameTimerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Eroare in timerul jocului.");
            }
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IActiveRoomStore>();
        var gameService = scope.ServiceProvider.GetRequiredService<IGameRoomService>();

        foreach (var roomCode in await store.ListActiveRoomCodesAsync(cancellationToken))
        {
            var snapshot = await TryGetSnapshotAsync(gameService, roomCode, cancellationToken);
            if (snapshot?.Status == GameStatus.Playing &&
                snapshot.CurrentQuestion is not null &&
                DateTimeOffset.UtcNow >= snapshot.CurrentQuestion.ServerStartedAtUtc.AddSeconds(snapshot.CurrentQuestion.TimeLimitSeconds))
            {
                var ended = await gameService.EndQuestionAsync(roomCode, null, cancellationToken);
                await hubContext.Clients.Group(GameHub.RoomGroup(roomCode)).SendAsync("QuestionEnded", ended, cancellationToken);
                await hubContext.Clients.Group(GameHub.RoomGroup(roomCode)).SendAsync("LeaderboardUpdated", ended.Leaderboard, cancellationToken);
            }
        }

        foreach (var expiredRoom in await gameService.ExpireOldRoomsAsync(cancellationToken))
        {
            await hubContext.Clients.Group(GameHub.RoomGroup(expiredRoom)).SendAsync("RoomExpired", expiredRoom, cancellationToken);
        }
    }

    private static async Task<WordGame.Application.DTOs.RoomSnapshotDto?> TryGetSnapshotAsync(IGameRoomService gameService, string roomCode, CancellationToken cancellationToken)
    {
        try
        {
            return await gameService.GetSnapshotAsync(roomCode, cancellationToken);
        }
        catch
        {
            return null;
        }
    }
}
