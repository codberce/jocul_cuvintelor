using Microsoft.AspNetCore.SignalR;
using WordGame.Application.DTOs;
using WordGame.Application.Interfaces;

namespace WordGame.Web.Hubs;

public sealed class GameHub(IGameRoomService gameRoomService, IActiveRoomStore activeRoomStore) : Hub
{
    private static readonly Guid PublicHostUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static string RoomGroup(string roomCode) => $"room:{roomCode}";

    public async Task<RoomCreatedDto> CreateRoom(CreateRoomRequest request)
    {
        var result = await gameRoomService.CreateRoomAsync(PublicHostUserId, request, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(result.RoomCode), Context.ConnectionAborted);
        await Clients.Caller.SendAsync("RoomCreated", result, Context.ConnectionAborted);
        return result;
    }

    public async Task<RoomSnapshotDto> WatchRoom(string roomCode)
    {
        var snapshot = await gameRoomService.GetSnapshotAsync(roomCode, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(roomCode), Context.ConnectionAborted);
        return snapshot;
    }

    public async Task<JoinRoomResult> JoinRoom(JoinRoomRequest request)
    {
        var result = await gameRoomService.JoinRoomAsync(request, Context.ConnectionId, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(result.RoomCode), Context.ConnectionAborted);
        await BroadcastPlayersAsync(result.RoomCode);
        await Clients.Group(RoomGroup(result.RoomCode)).SendAsync("PlayerJoined", result.PlayerId, result.DisplayName, Context.ConnectionAborted);
        return result;
    }

    public async Task LeaveRoom(string roomCode)
    {
        await gameRoomService.MarkPlayerDisconnectedAsync(Context.ConnectionId, Context.ConnectionAborted);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroup(roomCode), Context.ConnectionAborted);
        await BroadcastPlayersAsync(roomCode);
        await Clients.Group(RoomGroup(roomCode)).SendAsync("PlayerLeft", Context.ConnectionId, Context.ConnectionAborted);
    }

    public async Task StartGame(string roomCode)
    {
        var snapshot = await gameRoomService.GetSnapshotAsync(roomCode, Context.ConnectionAborted);
        var question = await gameRoomService.StartGameAsync(roomCode, snapshot.HostUserId, Context.ConnectionAborted);
        await Clients.Group(RoomGroup(roomCode)).SendAsync("GameStarted", roomCode, Context.ConnectionAborted);
        await Clients.Group(RoomGroup(roomCode)).SendAsync("QuestionStarted", question, Context.ConnectionAborted);
        await BroadcastLeaderboardAsync(roomCode);
    }

    public async Task SubmitAnswer(SubmitAnswerRequest request)
    {
        var result = await gameRoomService.SubmitAnswerAsync(request, Context.ConnectionAborted);
        await Clients.Caller.SendAsync("PlayerFeedback", result.Feedback, Context.ConnectionAborted);
        await Clients.Group(RoomGroup(request.RoomCode)).SendAsync("AnswerReceived", result.AnswerReceived, Context.ConnectionAborted);
        await BroadcastLeaderboardAsync(request.RoomCode);

        if (result.ShouldAutoEndQuestion)
        {
            var ended = await gameRoomService.EndQuestionAsync(request.RoomCode, null, Context.ConnectionAborted);
            await Clients.Group(RoomGroup(request.RoomCode)).SendAsync("QuestionEnded", ended, Context.ConnectionAborted);
            await Clients.Group(RoomGroup(request.RoomCode)).SendAsync("LeaderboardUpdated", ended.Leaderboard, Context.ConnectionAborted);
        }
    }

    public async Task NextQuestion(string roomCode)
    {
        var snapshot = await gameRoomService.GetSnapshotAsync(roomCode, Context.ConnectionAborted);
        var question = await gameRoomService.NextQuestionAsync(roomCode, snapshot.HostUserId, Context.ConnectionAborted);
        if (question is null)
        {
            var finalSnapshot = await gameRoomService.GetSnapshotAsync(roomCode, Context.ConnectionAborted);
            await Clients.Group(RoomGroup(roomCode)).SendAsync("GameEnded", new GameEndedDto(roomCode, finalSnapshot.Leaderboard, DateTimeOffset.UtcNow), Context.ConnectionAborted);
            return;
        }

        await Clients.Group(RoomGroup(roomCode)).SendAsync("QuestionStarted", question, Context.ConnectionAborted);
    }

    public async Task EndGame(string roomCode)
    {
        var snapshot = await gameRoomService.GetSnapshotAsync(roomCode, Context.ConnectionAborted);
        var ended = await gameRoomService.EndGameAsync(roomCode, snapshot.HostUserId, Context.ConnectionAborted);
        await Clients.Group(RoomGroup(roomCode)).SendAsync("GameEnded", ended, Context.ConnectionAborted);
    }

    public async Task<ReconnectResult> ReconnectPlayer(string roomCode, string guestToken)
    {
        var result = await gameRoomService.ReconnectPlayerAsync(roomCode, guestToken, Context.ConnectionId, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(roomCode), Context.ConnectionAborted);
        await Clients.Caller.SendAsync("RoomSnapshot", result.Snapshot, Context.ConnectionAborted);
        await BroadcastPlayersAsync(roomCode);
        return result;
    }

    public Task<string> Ping() => Task.FromResult("pong");

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var mapping = await activeRoomStore.GetConnectionMappingAsync(Context.ConnectionId, Context.ConnectionAborted);
        await gameRoomService.MarkPlayerDisconnectedAsync(Context.ConnectionId, Context.ConnectionAborted);
        if (mapping is not null)
        {
            await BroadcastPlayersAsync(mapping.RoomCode);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task BroadcastPlayersAsync(string roomCode)
    {
        var snapshot = await gameRoomService.GetSnapshotAsync(roomCode, Context.ConnectionAborted);
        await Clients.Group(RoomGroup(roomCode)).SendAsync("PlayersUpdated", snapshot.Players, Context.ConnectionAborted);
    }

    private async Task BroadcastLeaderboardAsync(string roomCode)
    {
        var snapshot = await gameRoomService.GetSnapshotAsync(roomCode, Context.ConnectionAborted);
        await Clients.Group(RoomGroup(roomCode)).SendAsync("LeaderboardUpdated", snapshot.Leaderboard, Context.ConnectionAborted);
    }
}
