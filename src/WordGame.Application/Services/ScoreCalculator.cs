using WordGame.Application.Interfaces;
using WordGame.Application.Options;
using Microsoft.Extensions.Options;

namespace WordGame.Application.Services;

public sealed class ScoreCalculator(IOptions<GameOptions> options) : IScoreCalculator
{
    private readonly GameOptions _options = options.Value;

    public int CalculateScore(string normalizedAnswer, int remainingTimeMs, int totalTimeMs)
    {
        if (string.IsNullOrWhiteSpace(normalizedAnswer) || totalTimeMs <= 0)
        {
            return 0;
        }

        var baseScore = normalizedAnswer.Length * _options.BasePointsPerLetter;
        return baseScore;
    }
}
