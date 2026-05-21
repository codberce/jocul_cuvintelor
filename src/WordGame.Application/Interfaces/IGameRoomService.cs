using WordGame.Application.DTOs;

namespace WordGame.Application.Interfaces;

public interface IGameRoomService
{
    Task<RoomCreatedDto> CreateRoomAsync(Guid hostUserId, CreateRoomRequest request, CancellationToken cancellationToken = default);
    Task<JoinRoomResult> JoinRoomAsync(JoinRoomRequest request, string? connectionId, CancellationToken cancellationToken = default);
    Task<ReconnectResult> ReconnectPlayerAsync(string roomCode, string guestToken, string connectionId, CancellationToken cancellationToken = default);
    Task<RoomSnapshotDto> GetSnapshotAsync(string roomCode, CancellationToken cancellationToken = default);
    Task<QuestionStartedDto> StartGameAsync(string roomCode, Guid hostUserId, CancellationToken cancellationToken = default);
    Task<QuestionHintDto> RequestLetterAsync(string roomCode, string guestToken, Guid questionId, CancellationToken cancellationToken = default);
    Task<SubmitAnswerResult> SubmitAnswerAsync(SubmitAnswerRequest request, CancellationToken cancellationToken = default);
    Task<QuestionEndedDto> EndQuestionAsync(string roomCode, Guid? hostUserId, CancellationToken cancellationToken = default);
    Task<QuestionStartedDto?> NextQuestionAsync(string roomCode, Guid hostUserId, CancellationToken cancellationToken = default);
    Task<GameEndedDto> EndGameAsync(string roomCode, Guid? hostUserId, CancellationToken cancellationToken = default);
    Task MarkPlayerDisconnectedAsync(string connectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ExpireOldRoomsAsync(CancellationToken cancellationToken = default);
}
