using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using WordGame.Application.Subjects;
using WordGame.Domain.Entities;
using WordGame.Domain.Enums;

namespace WordGame.Infrastructure.Persistence;

public sealed class DatabaseSeeder(ApplicationDbContext dbContext)
{
    public async Task MigrateAndSeedAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
        await SeedContentAsync(cancellationToken);
    }

    private async Task SeedContentAsync(CancellationToken cancellationToken)
    {
        var categories = new[]
        {
            new Category { Name = "Vocabular", Slug = "vocabular" },
            new Category { Name = "Gramatica", Slug = "gramatica" },
            new Category { Name = "Literatura", Slug = "literatura" },
            new Category { Name = "Cultura generala", Slug = "cultura-generala" },
            new Category { Name = "Stiinte", Slug = "stiinte" },
            new Category { Name = "Matematica", Slug = "matematica" },
            new Category { Name = "Informatica", Slug = "informatica" },
            new Category { Name = "Istorie", Slug = "istorie" },
            new Category { Name = "Fizica", Slug = "fizica" },
            new Category { Name = "Geografie", Slug = "geografie" },
            new Category { Name = "Biologie", Slug = "biologie" },
            new Category { Name = "Chimie", Slug = "chimie" },
            new Category { Name = "Religie", Slug = "religie" },
            new Category { Name = "Economie", Slug = "economie" },
            new Category { Name = "Engleza", Slug = "engleza" },
            new Category { Name = "Franceza", Slug = "franceza" },
            new Category { Name = "Germana", Slug = "germana" }
        };

        foreach (var subjectOption in SubjectCatalog.Subjects)
        {
            var subject = await dbContext.Subjects.SingleOrDefaultAsync(x => x.Slug == subjectOption.Slug, cancellationToken);
            if (subject is null)
            {
                dbContext.Subjects.Add(new Subject
                {
                    Name = subjectOption.Name,
                    Slug = subjectOption.Slug,
                    TargetQuestionCount = SubjectCatalog.TargetQuestionCount
                });
            }
            else
            {
                subject.Name = subjectOption.Name;
                subject.TargetQuestionCount = SubjectCatalog.TargetQuestionCount;
                subject.IsActive = true;
            }
        }

        foreach (var category in categories)
        {
            if (!await dbContext.Categories.AnyAsync(x => x.Slug == category.Slug, cancellationToken))
            {
                dbContext.Categories.Add(category);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var categoryMap = await dbContext.Categories.ToDictionaryAsync(x => x.Slug, x => x, cancellationToken);
        var subjectMap = await dbContext.Subjects.ToDictionaryAsync(x => x.Slug, x => x, cancellationToken);
        await SeedLessonsAsync(categoryMap, cancellationToken);
        await SeedQuestionsFromDataAsync(categoryMap, subjectMap, cancellationToken);
        await CleanupLegacyPlaceholderQuestionsAsync(cancellationToken);
    }

    private async Task SeedLessonsAsync(Dictionary<string, Category> categories, CancellationToken cancellationToken)
    {
        var lessons = new[]
        {
            new Lesson { Title = "Normalizarea raspunsurilor", Slug = "normalizarea-raspunsurilor", DisplayOrder = 1, CategoryId = categories["vocabular"].Id, Content = "In joc, raspunsurile sunt evaluate dupa forma lor normalizata: litere mici, fara diacritice, fara punctuatie simpla si fara spatii inutile. Astfel, SCOALA si scoala sunt echivalente." },
            new Lesson { Title = "Definitia si termenul", Slug = "definitia-si-termenul", DisplayOrder = 2, CategoryId = categories["vocabular"].Id, Content = "O definitie buna identifica sensul esential al unui cuvant. Jucatorul trebuie sa deduca termenul exact pornind de la indicii precum categoria, dificultatea si numarul de litere." },
            new Lesson { Title = "Strategii de raspuns rapid", Slug = "strategii-raspuns-rapid", DisplayOrder = 3, CategoryId = categories["gramatica"].Id, Content = "Scorul combina lungimea raspunsului corect cu bonusul de viteza. Este important sa raspunzi corect, dar si sa confirmi raspunsul inainte ca timpul sa expire." }
        };

        foreach (var lesson in lessons)
        {
            if (!await dbContext.Lessons.AnyAsync(x => x.Slug == lesson.Slug, cancellationToken))
            {
                dbContext.Lessons.Add(lesson);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedQuestionsFromDataAsync(Dictionary<string, Category> categories, Dictionary<string, Subject> subjects, CancellationToken cancellationToken)
    {
        foreach (var subjectOption in SubjectCatalog.Subjects)
        {
            var subject = subjects[subjectOption.Slug];
            var seeds = await LoadQuestionSeedsAsync(subjectOption.Slug, cancellationToken);
            if (seeds.Count != SubjectCatalog.TargetQuestionCount)
            {
                throw new InvalidOperationException($"Question bank '{subjectOption.Slug}' must contain exactly {SubjectCatalog.TargetQuestionCount} questions.");
            }

            var duplicateAnswer = seeds
                .GroupBy(x => x.Answer.Trim(), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(x => x.Count() > 1);
            if (duplicateAnswer is not null)
            {
                throw new InvalidOperationException($"Question bank '{subjectOption.Slug}' has duplicate answer '{duplicateAnswer.Key}'.");
            }

            var activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var seed in seeds)
            {
                if (!categories.TryGetValue(seed.Category, out var category))
                {
                    throw new InvalidOperationException($"Question bank '{subjectOption.Slug}' references unknown category '{seed.Category}'.");
                }

                var answer = seed.Answer.Trim();
                var normalizedAnswer = answer.ToLowerInvariant();
                var definition = seed.Definition.Trim();
                if (string.IsNullOrWhiteSpace(answer) || string.IsNullOrWhiteSpace(definition))
                {
                    throw new InvalidOperationException($"Question bank '{subjectOption.Slug}' contains an empty answer or definition.");
                }

                var key = QuestionKey(category.Id, answer);
                activeKeys.Add(key);

                var matches = await dbContext.Questions
                    .Where(x => x.CategoryId == category.Id && x.Answer.ToLower() == normalizedAnswer)
                    .OrderBy(x => x.CreatedAt)
                    .ToListAsync(cancellationToken);
                var existing = matches.FirstOrDefault();
                foreach (var duplicate in matches.Skip(1))
                {
                    duplicate.IsActive = false;
                }

                if (existing is null)
                {
                    dbContext.Questions.Add(new Question
                    {
                        Answer = answer,
                        Definition = definition,
                        SubjectId = subject.Id,
                        CategoryId = category.Id,
                        Difficulty = seed.Difficulty,
                        TimeLimitSeconds = seed.TimeLimitSeconds,
                        IsActive = true
                    });

                    continue;
                }

                existing.Definition = definition;
                existing.SubjectId = subject.Id;
                existing.Difficulty = seed.Difficulty;
                existing.TimeLimitSeconds = seed.TimeLimitSeconds;
                existing.IsActive = true;
            }

            var activeQuestions = await dbContext.Questions
                .Where(x => x.SubjectId == subject.Id && x.IsActive)
                .ToListAsync(cancellationToken);
            foreach (var question in activeQuestions)
            {
                if (!activeKeys.Contains(QuestionKey(question.CategoryId, question.Answer)))
                {
                    question.IsActive = false;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<IReadOnlyList<QuestionSeedFileEntry>> LoadQuestionSeedsAsync(string subjectSlug, CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "Seed", $"questions.{subjectSlug}.json");
        if (!File.Exists(path))
        {
            path = Path.Combine(Directory.GetCurrentDirectory(), "src", "WordGame.Infrastructure", "Data", "Seed", $"questions.{subjectSlug}.json");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing question bank for subject '{subjectSlug}'.", path);
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<IReadOnlyList<QuestionSeedFileEntry>>(
            stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Converters = { new JsonStringEnumConverter() }
            },
            cancellationToken) ?? [];
    }

    private static string QuestionKey(Guid categoryId, string answer) => $"{categoryId:N}:{answer.Trim().ToLowerInvariant()}";

    private sealed record QuestionSeedFileEntry(string Answer, string Definition, string Category, Difficulty Difficulty, int TimeLimitSeconds = 25);

    private async Task CleanupLegacyPlaceholderQuestionsAsync(CancellationToken cancellationToken)
    {
        await dbContext.Questions
            .Where(x => EF.Functions.ILike(x.Definition, "Notiune din materia%"))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsActive, false)
                .SetProperty(x => x.Definition, "Intrebare veche dezactivata si inlocuita de banca JSON."),
                cancellationToken);
    }

}
