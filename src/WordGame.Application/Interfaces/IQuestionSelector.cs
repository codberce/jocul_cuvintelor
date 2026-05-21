using WordGame.Application.DTOs;

namespace WordGame.Application.Interfaces;

public interface IQuestionSelector
{
    Task<IReadOnlyList<ActiveQuestionState>> SelectQuestionsAsync(int count, IReadOnlyCollection<string> subjectSlugs, CancellationToken cancellationToken = default);
}
