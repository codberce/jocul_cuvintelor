using WordGame.Application.DTOs;
using WordGame.Application.Interfaces;

namespace WordGame.Application.Services;

public sealed class LeaderboardService : ILeaderboardService
{
    public IReadOnlyList<LeaderboardEntryDto> BuildLeaderboard(IEnumerable<ActivePlayerState> players)
    {
        return players
            .OrderByDescending(player => player.Score)
            .ThenBy(player => player.JoinedAt)
            .Select((player, index) => new LeaderboardEntryDto(player.PlayerId, player.DisplayName, index + 1, player.Score))
            .ToList();
    }
}
