using WordGame.Application.DTOs;

namespace WordGame.Application.Subjects;

public static class SubjectCatalog
{
    public const int TargetQuestionCount = 100;

    public static readonly IReadOnlyList<SubjectOptionDto> Subjects =
    [
        new("math", "Matematică"),
        new("romanian", "Limba română"),
        new("informatics", "Informatică"),
        new("history", "Istorie"),
        new("physics", "Fizică"),
        new("geography", "Geografie"),
        new("biology", "Biologie"),
        new("chemistry", "Chimie"),
        new("religion", "Religie"),
        new("economy", "Economie"),
        new("english", "Limba engleză"),
        new("french", "Limba franceză"),
        new("german", "Limba germană")
    ];

    private static readonly HashSet<string> ValidSlugs = Subjects.Select(x => x.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> NormalizeSelectedSubjects(IEnumerable<string>? subjectSlugs)
    {
        return subjectSlugs?
            .Select(slug => slug.Trim().ToLowerInvariant())
            .Where(slug => ValidSlugs.Contains(slug))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(slug => slug)
            .ToList() ?? [];
    }

    public static bool IsKnownSubject(string slug) => ValidSlugs.Contains(slug);
}
