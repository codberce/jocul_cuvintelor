using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WordGame.Application.DTOs;
using WordGame.Application.Interfaces;
using WordGame.Application.Options;
using WordGame.Application.Services;
using WordGame.Application.Validators;
using WordGame.Domain.Entities;
using WordGame.Domain.Enums;
using WordGame.Infrastructure.Persistence;
using Xunit;

namespace WordGame.IntegrationTests;

public sealed class GameFlowIntegrationTests
{
    [Fact]
    public async Task CreateRoom_and_join_room_updates_snapshot()
    {
        var fixture = await FlowFixture.CreateAsync();
        var room = await fixture.Service.CreateRoomAsync(fixture.HostUserId, new CreateRoomRequest(10, ["romanian"]));

        var joined = await fixture.Service.JoinRoomAsync(new JoinRoomRequest(room.RoomCode, "Ana"), null);
        var snapshot = await fixture.Service.GetSnapshotAsync(room.RoomCode);

        Assert.Equal(room.RoomCode, joined.RoomCode);
        Assert.Single(snapshot.Players);
        Assert.Equal("Ana", snapshot.Players[0].DisplayName);
    }

    [Fact]
    public async Task StartGame_returns_question_without_correct_answer_contract()
    {
        var fixture = await FlowFixture.CreateWithJoinedPlayerAsync();

        var question = await fixture.Service.StartGameAsync(fixture.RoomCode, fixture.HostUserId);

        Assert.NotEqual(Guid.Empty, question.QuestionId);
        Assert.DoesNotContain(typeof(QuestionStartedDto).GetProperties(), p => p.Name.Contains("Answer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SubmitAnswer_and_end_question_update_leaderboard()
    {
        var fixture = await FlowFixture.CreateWithStartedGameAsync();
        var snapshot = await fixture.Service.GetSnapshotAsync(fixture.RoomCode);
        var current = snapshot.CurrentQuestion!;
        var correctAnswer = await fixture.Db.Questions
            .Where(x => x.Id == current.QuestionId)
            .Select(x => x.Answer)
            .SingleAsync();

        var submit = await fixture.Service.SubmitAnswerAsync(new SubmitAnswerRequest(fixture.RoomCode, fixture.GuestToken, current.QuestionId, correctAnswer, "answer-1"));
        var ended = await fixture.Service.EndQuestionAsync(fixture.RoomCode, fixture.HostUserId);

        Assert.True(submit.Feedback.IsCorrect);
        Assert.Equal(1, ended.CorrectCount);
        Assert.Single(ended.Leaderboard);
        Assert.True(ended.Leaderboard[0].Score > 0);
    }

    [Fact]
    public async Task EndGame_persists_game_session()
    {
        var fixture = await FlowFixture.CreateWithStartedGameAsync();

        var ended = await fixture.Service.EndGameAsync(fixture.RoomCode, fixture.HostUserId);
        var sessions = await fixture.Db.GameSessions.ToListAsync();

        Assert.Single(sessions);
        Assert.Equal(fixture.RoomCode, ended.RoomCode);
        Assert.Single(ended.Leaderboard);
    }

    [Fact]
    public async Task Unauthorized_host_actions_are_blocked()
    {
        var fixture = await FlowFixture.CreateWithJoinedPlayerAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.StartGameAsync(fixture.RoomCode, Guid.NewGuid()));
    }

    private sealed class FlowFixture
    {
        public required ApplicationDbContext Db { get; init; }
        public required GameRoomService Service { get; init; }
        public required Guid HostUserId { get; init; }
        public required string RoomCode { get; init; }
        public required string GuestToken { get; init; }

        public static async Task<FlowFixture> CreateAsync()
        {
            var db = CreateDb();
            var normalizer = new AnswerNormalizer();
            var category = new Category { Name = "Vocabular", Slug = "vocabular" };
            var subject = new Subject { Name = "Romanian", Slug = "romanian" };
            db.Categories.Add(category);
            db.Subjects.Add(subject);
            db.Questions.Add(new Question { Answer = "scoala", Definition = "Institutie de invatare.", Category = category, Subject = subject, Difficulty = Difficulty.Easy, TimeLimitSeconds = 25 });
            for (var i = 0; i < 20; i++)
            {
                db.Questions.Add(new Question { Answer = $"cuvant{i}", Definition = $"Definitie {i}", Category = category, Subject = subject, Difficulty = Difficulty.Medium, TimeLimitSeconds = 25 });
            }
            await db.SaveChangesAsync();

            var store = new InMemoryActiveRoomStore();
            var guest = new TokenGuestSessionService();
            var options = Options.Create(new GameOptions { QuestionsPerGame = 14, MaxPlayersPerRoom = 20 });
            var service = new GameRoomService(
                db,
                store,
                new NoopLockProvider(),
                new QuestionSelector(db, normalizer),
                new RoomCodeGenerator(),
                new FakeQrCodeService(),
                guest,
                normalizer,
                new LeaderboardService(),
                new CreateRoomRequestValidator(options),
                new JoinRoomRequestValidator(options),
                new SubmitAnswerRequestValidator(),
                options,
                Options.Create(new RedisOptions()),
                Options.Create(new SecurityOptions()));

            return new FlowFixture { Db = db, Service = service, HostUserId = Guid.NewGuid(), RoomCode = string.Empty, GuestToken = string.Empty };
        }

        public static async Task<FlowFixture> CreateWithJoinedPlayerAsync()
        {
            var fixture = await CreateAsync();
            var room = await fixture.Service.CreateRoomAsync(fixture.HostUserId, new CreateRoomRequest(10, ["romanian"]));
            var player = await fixture.Service.JoinRoomAsync(new JoinRoomRequest(room.RoomCode, "Ana"), null);
            return fixture.WithState(room.RoomCode, player.GuestToken);
        }

        public static async Task<FlowFixture> CreateWithStartedGameAsync()
        {
            var fixture = await CreateWithJoinedPlayerAsync();
            await fixture.Service.StartGameAsync(fixture.RoomCode, fixture.HostUserId);
            return fixture;
        }

        private FlowFixture WithState(string roomCode, string guestToken)
        {
            return new FlowFixture { Db = Db, Service = Service, HostUserId = HostUserId, RoomCode = roomCode, GuestToken = guestToken };
        }

        private static ApplicationDbContext CreateDb()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            return new ApplicationDbContext(options);
        }
    }

    private sealed class InMemoryActiveRoomStore : IActiveRoomStore
    {
        private readonly Dictionary<string, ActiveRoomState> _rooms = [];
        public Task<bool> ExistsAsync(string roomCode, CancellationToken cancellationToken = default) => Task.FromResult(_rooms.ContainsKey(roomCode));
        public Task<ActiveRoomState?> GetAsync(string roomCode, CancellationToken cancellationToken = default) => Task.FromResult(_rooms.GetValueOrDefault(roomCode));
        public Task SaveAsync(ActiveRoomState state, CancellationToken cancellationToken = default) { _rooms[state.RoomCode] = state; return Task.CompletedTask; }
        public Task RemoveAsync(string roomCode, CancellationToken cancellationToken = default) { _rooms.Remove(roomCode); return Task.CompletedTask; }
        public Task<IReadOnlyList<string>> ListActiveRoomCodesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>(_rooms.Keys.ToList());
        public Task SetConnectionMappingAsync(string connectionId, ConnectionMapping mapping, TimeSpan ttl, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ConnectionMapping?> GetConnectionMappingAsync(string connectionId, CancellationToken cancellationToken = default) => Task.FromResult<ConnectionMapping?>(null);
        public Task RemoveConnectionMappingAsync(string connectionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoopLockProvider : IDistributedLockProvider
    {
        public Task<IDistributedLockHandle?> TryAcquireAsync(string resource, TimeSpan expiry, CancellationToken cancellationToken = default) => Task.FromResult<IDistributedLockHandle?>(new Handle(resource));
        private sealed class Handle(string resource) : IDistributedLockHandle { public string Resource { get; } = resource; public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
    }

    private sealed class TokenGuestSessionService : IGuestSessionService
    {
        public string CreateToken(string roomCode, Guid playerId, DateTimeOffset expiresAt) => $"{roomCode}:{playerId:N}";
        public bool TryValidateToken(string token, string roomCode, out Guid playerId)
        {
            playerId = Guid.Empty;
            var parts = token.Split(':');
            return parts.Length == 2 && parts[0] == roomCode && Guid.TryParse(parts[1], out playerId);
        }
    }

    private sealed class FakeQrCodeService : IQrCodeService
    {
        public string BuildJoinUrl(string roomCode) => $"http://localhost/join/{roomCode}";
        public string GenerateJoinQrDataUri(string roomCode) => "data:image/png;base64,";
    }
}
