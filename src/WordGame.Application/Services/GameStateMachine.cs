using WordGame.Domain.Enums;

namespace WordGame.Application.Services;

public static class GameStateMachine
{
    public static bool CanStart(GameStatus status) => status == GameStatus.Lobby;

    public static bool CanSubmitAnswer(GameStatus status) => status == GameStatus.Playing;

    public static bool CanEndQuestion(GameStatus status) => status is GameStatus.Playing or GameStatus.QuestionResults;

    public static bool CanAdvance(GameStatus status) => status == GameStatus.QuestionResults;

    public static bool CanEndGame(GameStatus status) => status is GameStatus.Lobby or GameStatus.Playing or GameStatus.QuestionResults or GameStatus.Starting;
}
