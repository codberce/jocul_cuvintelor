using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WordGame.Domain.Entities;
using WordGame.Infrastructure.Identity;

namespace WordGame.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<GameRoom> GameRooms => Set<GameRoom>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<PlayerAnswer> PlayerAnswers => Set<PlayerAnswer>();
    public DbSet<LeaderboardEntry> LeaderboardEntries => Set<LeaderboardEntry>();
    public DbSet<GameEvent> GameEvents => Set<GameEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.DisplayName).HasMaxLength(80);
        });

        builder.Entity<Category>(entity =>
        {
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        });

        builder.Entity<Subject>(entity =>
        {
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        });

        builder.Entity<Question>(entity =>
        {
            entity.HasIndex(x => new { x.CategoryId, x.Answer }).IsUnique();
            entity.Property(x => x.Answer).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Definition).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(x => x.Category).WithMany(x => x.Questions).HasForeignKey(x => x.CategoryId);
            entity.HasOne(x => x.Subject).WithMany(x => x.Questions).HasForeignKey(x => x.SubjectId);
        });

        builder.Entity<Lesson>(entity =>
        {
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Title).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Content).IsRequired();
            entity.HasOne(x => x.Category).WithMany(x => x.Lessons).HasForeignKey(x => x.CategoryId);
        });

        builder.Entity<GameRoom>(entity =>
        {
            entity.HasIndex(x => x.RoomCode).IsUnique();
            entity.Property(x => x.RoomCode).HasMaxLength(6).IsRequired();
            entity.Property(x => x.SubjectSlugs).HasMaxLength(512);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.OwnsOne(x => x.Settings, owned =>
            {
                owned.Property(x => x.QuestionsPerGame).HasColumnName("QuestionsPerGame");
                owned.Property(x => x.DefaultQuestionTimeSeconds).HasColumnName("DefaultQuestionTimeSeconds");
                owned.Property(x => x.SpeedBonusMaxPoints).HasColumnName("SpeedBonusMaxPoints");
                owned.Property(x => x.BasePointsPerLetter).HasColumnName("BasePointsPerLetter");
                owned.Property(x => x.MaxPlayersPerRoom).HasColumnName("SettingsMaxPlayersPerRoom");
            });
        });

        builder.Entity<Player>(entity =>
        {
            entity.HasIndex(x => new { x.RoomId, x.DisplayName }).IsUnique();
            entity.Property(x => x.DisplayName).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ConnectionId).HasMaxLength(160);
            entity.HasOne(x => x.Room).WithMany(x => x.Players).HasForeignKey(x => x.RoomId);
        });

        builder.Entity<PlayerAnswer>(entity =>
        {
            entity.HasIndex(x => new { x.RoomId, x.PlayerId, x.QuestionId }).IsUnique();
            entity.HasIndex(x => new { x.RoomId, x.PlayerId, x.QuestionId, x.IdempotencyKey }).IsUnique();
            entity.Property(x => x.SubmittedAnswer).HasMaxLength(128).IsRequired();
            entity.Property(x => x.NormalizedSubmittedAnswer).HasMaxLength(128).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.HasOne(x => x.Room).WithMany(x => x.Answers).HasForeignKey(x => x.RoomId);
            entity.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId);
            entity.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId);
        });

        builder.Entity<GameSession>(entity =>
        {
            entity.HasIndex(x => x.RoomCode);
            entity.Property(x => x.RoomCode).HasMaxLength(6).IsRequired();
            entity.Property(x => x.SubjectSlugs).HasMaxLength(512);
            entity.Property(x => x.FinalLeaderboardJson).IsRequired();
        });

        builder.Entity<LeaderboardEntry>(entity =>
        {
            entity.HasIndex(x => new { x.GameSessionId, x.Rank });
            entity.Property(x => x.PlayerName).HasMaxLength(64).IsRequired();
            entity.HasOne(x => x.GameSession).WithMany().HasForeignKey(x => x.GameSessionId);
        });

        builder.Entity<GameEvent>(entity =>
        {
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.PayloadJson).IsRequired();
            entity.HasOne(x => x.Room).WithMany(x => x.Events).HasForeignKey(x => x.RoomId);
        });
    }
}
