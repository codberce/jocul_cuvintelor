namespace WordGame.Domain.ValueObjects;

public sealed class GameSettings
{
    public int QuestionsPerGame { get; set; } = 14;
    public int DefaultQuestionTimeSeconds { get; set; } = 25;
    public int SpeedBonusMaxPoints { get; set; } = 500;
    public int BasePointsPerLetter { get; set; } = 100;
    public int MaxPlayersPerRoom { get; set; } = 50;
}
