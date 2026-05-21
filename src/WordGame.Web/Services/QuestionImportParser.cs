using System.Text.Json;
using WordGame.Application.DTOs;

namespace WordGame.Web.Services;

public static class QuestionImportParser
{
    public static IReadOnlyList<ImportQuestionDto> Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var trimmed = content.Trim();
        if (trimmed.StartsWith('['))
        {
            return JsonSerializer.Deserialize<List<ImportQuestionDto>>(trimmed, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        }

        var rows = new List<ImportQuestionDto>();
        foreach (var line in trimmed.Split('\n').Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            if (line.StartsWith("Answer,", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var columns = SplitCsv(line);
            if (columns.Count < 4)
            {
                continue;
            }

            var hasSubjectColumn = columns.Count >= 7;
            rows.Add(new ImportQuestionDto(
                columns[0],
                columns[1],
                hasSubjectColumn ? columns[2] : "romanian",
                hasSubjectColumn ? columns[3] : columns[2],
                hasSubjectColumn ? columns[4] : columns[3],
                columns.Count > (hasSubjectColumn ? 5 : 4) && int.TryParse(columns[hasSubjectColumn ? 5 : 4], out var time) ? time : null,
                columns.Count <= (hasSubjectColumn ? 6 : 5) || !bool.TryParse(columns[hasSubjectColumn ? 6 : 5], out var active) || active));
        }

        return rows;
    }

    private static List<string> SplitCsv(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
            {
                current.Append('"');
                i++;
                continue;
            }

            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        result.Add(current.ToString().Trim());
        return result;
    }
}
