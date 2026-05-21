using WordGame.Application.DTOs;

namespace WordGame.Application.Interfaces;

public interface ISinglePlayerTrainingService
{
    Task<SinglePlayerSessionDto> StartSessionAsync(StartTrainingRequest request, CancellationToken cancellationToken = default);
    SinglePlayerSessionDto? GetCurrentSession();
    SinglePlayerSessionDto RequestLetter(Guid sessionId);
    Task<SinglePlayerFeedbackDto> SubmitAnswerAsync(Guid sessionId, Guid questionId, string answer, CancellationToken cancellationToken = default);
    SinglePlayerSessionDto NextQuestion(Guid sessionId);
}
