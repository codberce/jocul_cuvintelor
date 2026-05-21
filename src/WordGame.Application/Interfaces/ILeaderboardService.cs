using WordGame.Application.DTOs;

namespace WordGame.Application.Interfaces;

public interface ILeaderboardService
{
    IReadOnlyList<LeaderboardEntryDto> BuildLeaderboard(IEnumerable<ActivePlayerState> players);
}
