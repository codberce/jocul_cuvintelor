using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WordGame.Application.DTOs;
using WordGame.Application.Interfaces;
using WordGame.Application.Options;
using WordGame.Application.Services;
using WordGame.Application.Validators;
using WordGame.Domain.Entities;
using WordGame.Domain.Enums;
using WordGame.Domain.ValueObjects;
using WordGame.Infrastructure.Persistence;
using Xunit;

namespace WordGame.UnitTests;

public sealed class GameRoomServiceTests
{
    [Fact]
    public async Task SubmitAnswer_is_idempotent_for_same_key()
    {
        var fixture = await ServiceFixture.CreateAsync();
        var request = fixture.CreateSubmitRequest("scoala", "same-key");

        var first = await fixture.Service.SubmitAnswerAsync(request);
        var second = await fixture.Service.SubmitAnswerAsync(request);
        var snapshot = await fixture.Service.GetSnapshotAsync(fixture.RoomCode);

        Assert.True(first.Feedback.IsCorrect);
        Assert.True(second.Feedback.Accepted);
        Assert.Single(snapshot.Leaderboard);
        Assert.Equal(first.Feedback.TotalScore, second.Feedback.TotalScore);
        Assert.Single(fixture.Store.State!.Answers);
    }

    [Fact]
    public async Task SubmitAnswer_rejects_second_answer_with_different_key()
    {
        var fixture = await ServiceFixture.CreateAsync();

        await fixture.Service.SubmitAnswerAsync(fixture.CreateSubmitRequest("gresit", "first"));
        var second = await fixture.Service.SubmitAnswerAsync(fixture.CreateSubmitRequest("scoala", "second"));

        Assert.False(second.Feedback.Accepted);
        Assert.Single(fixture.Store.State!.Answers);
    }

    [Fact]
    public async Task EndQuestion_is_idempotent()
    {
        var fixture = await ServiceFixture.CreateAsync();
        await fixture.Service.SubmitAnswerAsync(fixture.CreateSubmitRequest("scoala", "first"));

        var first = await fixture.Service.EndQuestionAsync(fixture.RoomCode, null);
        var second = await fixture.Service.EndQuestionAsync(fixture.RoomCode, null);

        Assert.Equal(first.QuestionId, second.QuestionId);
        Assert.Equal(first.CorrectCount, second.CorrectCount);
    }

    [Fact]
    public async Task Question_selection_returns_requested_active_questions()
    {
        await using var db = TestDb.Create();
        var normalizer = new AnswerNormalizer();
        var category = new Category { Name = "Vocabular", Slug = "vocabular" };
        var subject = new Subject { Name = "Romanian", Slug = "romanian" };
        var otherSubject = new Subject { Name = "Math", Slug = "math" };
        db.Categories.Add(category);
        db.Subjects.AddRange(subject, otherSubject);
        for (var i = 0; i < 20; i++)
        {
            db.Questions.Add(new Question
            {
                Answer = $"cuvant{i}",
                Definition = $"definitie {i}",
                Category = category,
                Subject = subject,
                Difficulty = Difficulty.Easy,
                IsActive = true
            });
            db.Questions.Add(new Question
            {
                Answer = $"numar{i}",
                Definition = $"definitie matematica {i}",
                Category = category,
                Subject = otherSubject,
                Difficulty = Difficulty.Easy,
                IsActive = true
            });
        }
        await db.SaveChangesAsync();

        var selector = new QuestionSelector(db, normalizer);
        var questions = await selector.SelectQuestionsAsync(14, ["romanian"]);

        Assert.Equal(14, questions.Count);
        Assert.All(questions, q => Assert.DoesNotContain(" ", q.NormalizedAnswer));
        Assert.All(questions, q => Assert.Equal("romanian", q.SubjectSlug));
    }

    private sealed class ServiceFixture
    {
        public required GameRoomService Service { get; init; }
        public required InMemoryActiveRoomStore Store { get; init; }
        public required string RoomCode { get; init; }
        public required string GuestToken { get; init; }
        public required Guid QuestionId { get; init; }

        public SubmitAnswerRequest CreateSubmitRequest(string answer, string key) => new(RoomCode, GuestToken, QuestionId, answer, key);

        public static async Task<ServiceFixture> CreateAsync()
        {
            var db = TestDb.Create();
            var roomId = Guid.NewGuid();
            var playerId = Guid.NewGuid();
            var questionId = Guid.NewGuid();
            var roomCode = "123456";

            db.GameRooms.Add(new GameRoom
            {
                Id = roomId,
                RoomCode = roomCode,
                HostUserId = Guid.NewGuid(),
                Status = GameStatus.Playing,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
                MaxPlayers = 10,
                Settings = new GameSettings()
            });
            db.Players.Add(new Player { Id = playerId, RoomId = roomId, DisplayName = "Ana", Connected = true });
            db.Questions.Add(new Question { Id = questionId, Answer = "scoala", Definition = "Institutie", Category = new Category { Name = "Vocabular", Slug = Guid.NewGuid().ToString("N") }, Subject = new Subject { Name = "Romanian", Slug = "romanian" } });
            await db.SaveChangesAsync();

            var normalizer = new AnswerNormalizer();
            var store = new InMemoryActiveRoomStore
            {
                State = new ActiveRoomState
                {
                    RoomId = roomId,
                    RoomCode = roomCode,
                    HostUserId = Guid.NewGuid(),
                    Status = GameStatus.Playing,
                    CurrentQuestionIndex = 0,
                    CurrentQuestionStartedAt = DateTimeOffset.UtcNow.AddSeconds(-5),
                    CurrentQuestionEndsAt = DateTimeOffset.UtcNow.AddSeconds(20),
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
                    Settings = new GameSettings(),
                    SubjectSlugs = ["romanian"],
                    Players = [new ActivePlayerState { PlayerId = playerId, DisplayName = "Ana", Connected = true, JoinedAt = DateTimeOffset.UtcNow }],
                    Questions =
                    [
                        new ActiveQuestionState
                        {
                            QuestionId = questionId,
                            Answer = "scoala",
                            NormalizedAnswer = normalizer.Normalize("scoala"),
                            Definition = "Institutie",
                            Subject = "Romanian",
                            SubjectSlug = "romanian",
                            Category = "Vocabular",
                            Difficulty = Difficulty.Easy,
                            TimeLimitSeconds = 25
                        }
                    ]
                }
            };

            var guest = new FakeGuestSessionService(playerId);
            var options = Options.Create(new GameOptions());
            var service = new GameRoomService(
                db,
                store,
                new NoopLockProvider(),
                new StaticQuestionSelector(store.State.Questions),
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

            return new ServiceFixture { Service = service, Store = store, RoomCode = roomCode, GuestToken = guest.Token, QuestionId = questionId };
        }
    }

    private sealed class InMemoryActiveRoomStore : IActiveRoomStore
    {
        public ActiveRoomState? State { get; set; }
        public Task<bool> ExistsAsync(string roomCode, CancellationToken cancellationToken = default) => Task.FromResult(State?.RoomCode == roomCode);
        public Task<ActiveRoomState?> GetAsync(string roomCode, CancellationToken cancellationToken = default) => Task.FromResult(State?.RoomCode == roomCode ? State : null);
        public Task SaveAsync(ActiveRoomState state, CancellationToken cancellationToken = default) { State = state; return Task.CompletedTask; }
        public Task RemoveAsync(string roomCode, CancellationToken cancellationToken = default) { State = null; return Task.CompletedTask; }
        public Task<IReadOnlyList<string>> ListActiveRoomCodesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>(State is null ? [] : [State.RoomCode]);
        public Task SetConnectionMappingAsync(string connectionId, ConnectionMapping mapping, TimeSpan ttl, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ConnectionMapping?> GetConnectionMappingAsync(string connectionId, CancellationToken cancellationToken = default) => Task.FromResult<ConnectionMapping?>(null);
        public Task RemoveConnectionMappingAsync(string connectionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoopLockProvider : IDistributedLockProvider
    {
        public Task<IDistributedLockHandle?> TryAcquireAsync(string resource, TimeSpan expiry, CancellationToken cancellationToken = default) => Task.FromResult<IDistributedLockHandle?>(new Handle(resource));
        private sealed class Handle(string resource) : IDistributedLockHandle { public string Resource { get; } = resource; public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
    }

    private sealed class StaticQuestionSelector(IReadOnlyList<ActiveQuestionState> questions) : IQuestionSelector
    {
        public Task<IReadOnlyList<ActiveQuestionState>> SelectQuestionsAsync(int count, IReadOnlyCollection<string> subjectSlugs, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ActiveQuestionState>>(questions.Where(x => subjectSlugs.Contains(x.SubjectSlug)).Take(count).ToList());
    }

    private sealed class FakeGuestSessionService(Guid playerId) : IGuestSessionService
    {
        public string Token { get; } = "token";
        public string CreateToken(string roomCode, Guid playerId, DateTimeOffset expiresAt) => Token;
        public bool TryValidateToken(string token, string roomCode, out Guid id) { id = playerId; return token == Token; }
    }

    private sealed class FakeQrCodeService : IQrCodeService
    {
        public string BuildJoinUrl(string roomCode) => $"http://localhost/join/{roomCode}";
        public string GenerateJoinQrDataUri(string roomCode) => "data:image/png;base64,";
    }

    private static class TestDb
    {
        public static ApplicationDbContext Create()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            return new ApplicationDbContext(options);
        }
    }
}
