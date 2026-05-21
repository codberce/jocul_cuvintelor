namespace WordGame.Domain.Enums;

public enum GameEventType
{
    RoomCreated = 0,
    PlayerJoined = 1,
    PlayerLeft = 2,
    GameStarted = 3,
    QuestionStarted = 4,
    AnswerReceived = 5,
    QuestionEnded = 6,
    GameEnded = 7,
    RoomExpired = 8,
    Error = 9
}
