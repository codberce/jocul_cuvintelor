namespace WordGame.Application.DTOs;

public sealed record SubjectOptionDto(string Slug, string Name);

public sealed record CreateRoomRequest(int? MaxPlayers = null, IReadOnlyList<string>? SubjectSlugs = null);

public sealed record StartTrainingRequest(IReadOnlyList<string>? SubjectSlugs = null);

public sealed record JoinRoomRequest(string RoomCode, string DisplayName);

public sealed record SubmitAnswerRequest(
    string RoomCode,
    string GuestToken,
    Guid QuestionId,
    string Answer,
    string IdempotencyKey);
