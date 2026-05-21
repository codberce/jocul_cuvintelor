using Microsoft.Extensions.Options;
using WordGame.Application.Options;
using WordGame.Application.Services;
using WordGame.Domain.Enums;
using Xunit;

namespace WordGame.UnitTests;

public sealed class CoreLogicTests
{
    [Theory]
    [InlineData("ȘCOALĂ", "scoala")]
    [InlineData(" scoala  ", "scoala")]
    [InlineData("Școală!", "scoala")]
    [InlineData("SCOALA", "scoala")]
    [InlineData("bună   ziua.", "buna ziua")]
    public void AnswerNormalizer_ignores_case_diacritics_spacing_and_simple_punctuation(string input, string expected)
    {
        var normalizer = new AnswerNormalizer();
        Assert.Equal(expected, normalizer.Normalize(input));
    }

    [Fact]
    public void ScoreCalculator_applies_show_score_by_letter_count()
    {
        var calculator = new ScoreCalculator(Options.Create(new GameOptions
        {
            BasePointsPerLetter = 100,
            SpeedBonusMaxPoints = 500
        }));

        Assert.Equal(600, calculator.CalculateScore("scoala", 17500, 25000));
    }

    [Fact]
    public async Task RoomCodeGenerator_returns_six_digit_unique_code()
    {
        var generator = new RoomCodeGenerator();
        var existing = new HashSet<string> { "000000" };

        var code = await generator.GenerateAsync((candidate, _) => Task.FromResult(existing.Contains(candidate)));

        Assert.Matches("^[0-9]{6}$", code);
        Assert.DoesNotContain(code, existing);
    }

    [Theory]
    [InlineData(GameStatus.Lobby, true)]
    [InlineData(GameStatus.Playing, false)]
    [InlineData(GameStatus.QuestionResults, false)]
    public void GameStateMachine_allows_start_only_from_lobby(GameStatus status, bool expected)
    {
        Assert.Equal(expected, GameStateMachine.CanStart(status));
    }

    [Fact]
    public void Leaderboard_orders_by_score_then_join_time()
    {
        var service = new LeaderboardService();
        var now = DateTimeOffset.UtcNow;

        var leaderboard = service.BuildLeaderboard([
            new() { PlayerId = Guid.NewGuid(), DisplayName = "Ana", Score = 100, JoinedAt = now.AddSeconds(2) },
            new() { PlayerId = Guid.NewGuid(), DisplayName = "Mihai", Score = 200, JoinedAt = now.AddSeconds(3) },
            new() { PlayerId = Guid.NewGuid(), DisplayName = "Ioana", Score = 200, JoinedAt = now.AddSeconds(1) }
        ]);

        Assert.Equal("Ioana", leaderboard[0].PlayerName);
        Assert.Equal("Mihai", leaderboard[1].PlayerName);
        Assert.Equal("Ana", leaderboard[2].PlayerName);
    }
}
