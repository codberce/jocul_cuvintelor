using Microsoft.EntityFrameworkCore;
using WordGame.Application.DTOs;
using WordGame.Application.Interfaces;

namespace WordGame.Infrastructure.Persistence;

public sealed class QuestionSelector(ApplicationDbContext dbContext, IAnswerNormalizer normalizer) : IQuestionSelector
{
    public async Task<IReadOnlyList<ActiveQuestionState>> SelectQuestionsAsync(int count, IReadOnlyCollection<string> subjectSlugs, CancellationToken cancellationToken = default)
    {
        var normalizedSubjects = subjectSlugs
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (normalizedSubjects.Count == 0)
        {
            throw new InvalidOperationException("Selecteaza cel putin o materie.");
        }

        var candidates = await dbContext.Questions
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Subject)
            .Where(x =>
                x.IsActive &&
                x.Category != null &&
                x.Category.IsActive &&
                x.Subject != null &&
                x.Subject.IsActive &&
                normalizedSubjects.Contains(x.Subject.Slug))
            .ToListAsync(cancellationToken);

        if (candidates.Count < count)
        {
            throw new InvalidOperationException($"Sunt necesare cel putin {count} intrebari active.");
        }

        var questions = candidates
            .OrderBy(_ => Guid.NewGuid())
            .Select(question => new ActiveQuestionState
            {
                QuestionId = question.Id,
                Answer = question.Answer,
                NormalizedAnswer = normalizer.Normalize(question.Answer),
                Definition = question.Definition,
                Subject = question.Subject!.Name,
                SubjectSlug = question.Subject.Slug,
                Category = question.Category!.Name,
                Difficulty = question.Difficulty,
                TimeLimitSeconds = question.TimeLimitSeconds
            })
            .ToList();

        var selected = new List<ActiveQuestionState>();
        foreach (var length in Enumerable.Range(4, 7))
        {
            selected.AddRange(questions
                .Where(question => question.NormalizedAnswer.Count(char.IsLetterOrDigit) == length)
                .Take(2));
        }

        if (selected.Count < count)
        {
            selected.AddRange(questions
                .Where(question => selected.All(selectedQuestion => selectedQuestion.QuestionId != question.QuestionId))
                .Take(count - selected.Count));
        }

        return selected
            .OrderBy(_ => Guid.NewGuid())
            .Take(count)
            .ToList();
    }
}
