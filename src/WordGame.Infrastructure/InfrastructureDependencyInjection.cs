using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using WordGame.Application.Interfaces;
using WordGame.Application.Options;
using WordGame.Infrastructure.Health;
using WordGame.Infrastructure.Identity;
using WordGame.Infrastructure.Persistence;
using WordGame.Infrastructure.Qr;
using WordGame.Infrastructure.Redis;

namespace WordGame.Infrastructure;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GameOptions>(configuration.GetSection("Game"));
        services.Configure<RedisOptions>(configuration.GetSection("Redis"));
        services.Configure<QrOptions>(configuration.GetSection("Qr"));
        services.Configure<SecurityOptions>(configuration.GetSection("Security"));
        services.Configure<RateLimitOptions>(configuration.GetSection("RateLimits"));
        services.Configure<SignalROptions>(configuration.GetSection("SignalR"));

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection lipseste.");

        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseNpgsql(connectionString), ServiceLifetime.Scoped);

        services
            .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/login";
            options.Cookie.Name = "WordGame.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.SlidingExpiration = true;
        });

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var redisOptions = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
            return ConnectionMultiplexer.Connect(redisOptions.ConnectionString);
        });

        services.AddScoped<IQuestionSelector, QuestionSelector>();
        services.AddScoped<IGameRoomService, GameRoomService>();
        services.AddSingleton<IActiveRoomStore, RedisActiveRoomStore>();
        services.AddSingleton<IDistributedLockProvider, RedisDistributedLockProvider>();
        services.AddSingleton<IQrCodeService, QrCodeService>();
        services.AddSingleton<IGuestSessionService, GuestSessionService>();
        services.AddScoped<DatabaseSeeder>();

        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy())
            .AddCheck("postgresql", new PostgresHealthCheck(connectionString))
            .AddCheck<RedisHealthCheck>("redis");

        return services;
    }
}
