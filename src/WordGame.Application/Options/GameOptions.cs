namespace WordGame.Application.Options;

public sealed class GameOptions
{
    public int QuestionsPerGame { get; set; } = 14;
    public int DefaultQuestionTimeSeconds { get; set; } = 25;
    public int TotalGameTimeSeconds { get; set; } = 240;
    public int MaxPlayersPerRoom { get; set; } = 50;
    public int RoomExpirationMinutes { get; set; } = 120;
    public int MaxPlayerNameLength { get; set; } = 32;
    public int SpeedBonusMaxPoints { get; set; } = 500;
    public int BasePointsPerLetter { get; set; } = 100;
}
