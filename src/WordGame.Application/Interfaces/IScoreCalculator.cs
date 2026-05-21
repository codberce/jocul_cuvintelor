namespace WordGame.Application.Interfaces;

public interface IScoreCalculator
{
    int CalculateScore(string normalizedAnswer, int remainingTimeMs, int totalTimeMs);
}
