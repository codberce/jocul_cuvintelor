using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Serilog;
using Serilog.Events;
using WordGame.Application;
using WordGame.Application.DTOs;
using WordGame.Application.Interfaces;
using WordGame.Application.Options;
using WordGame.Infrastructure;
using WordGame.Infrastructure.Persistence;
using WordGame.Web.Components;
using WordGame.Web.Hubs;
using WordGame.Web.Middleware;
using WordGame.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAntiforgery();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddHostedService<GameTimerService>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedHost |
        ForwardedHeaders.XForwardedProto;

    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var rateLimitOptions = builder.Configuration.GetSection("RateLimits").Get<RateLimitOptions>() ?? new RateLimitOptions();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("create-room", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.PermitLimit = rateLimitOptions.CreateRoomPerMinute;
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("join", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.PermitLimit = rateLimitOptions.JoinPerMinute;
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("submit-answer", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.PermitLimit = rateLimitOptions.SubmitAnswerPerMinute;
        limiter.QueueLimit = 0;
    });
});

var signalROptions = builder.Configuration.GetSection("SignalR").Get<SignalROptions>() ?? new SignalROptions();
var hubBuilder = builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

if (signalROptions.UseAzureSignalR)
{
    hubBuilder.AddAzureSignalR(signalROptions.AzureSignalRConnectionString);
}
else if (signalROptions.UseRedisBackplane)
{
    var redisConnection = builder.Configuration.GetSection("Redis").Get<RedisOptions>()?.ConnectionString ?? "localhost:6379";
    hubBuilder.AddStackExchangeRedis(redisConnection);
}

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();
if (app.Configuration.GetValue("Https:Redirect", false))
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAntiforgery();

app.MapHealthChecks("/health");
app.MapGet("/train", (HttpRequest request) => Results.Redirect($"/singleplayer{request.QueryString}"));
app.MapHub<GameHub>("/gameHub").RequireRateLimiting("submit-answer");
MapGameEndpoints(app);
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

if (app.Configuration.GetValue("Database:RunMigrationsOnStartup", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().MigrateAndSeedAsync();
}

app.Run();

static void MapGameEndpoints(WebApplication app)
{
    var publicHostUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    app.MapPost("/host/create", async (
            HttpContext httpContext,
            IAntiforgery antiforgery,
            IGameRoomService gameRoomService) =>
        {
            await antiforgery.ValidateRequestAsync(httpContext);
            var form = await httpContext.Request.ReadFormAsync();
            var maxPlayers = int.TryParse(form["MaxPlayers"].ToString(), out var parsed) ? parsed : (int?)null;
            var subjectSlugs = form["SubjectSlugs"].Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToArray();
            var result = await gameRoomService.CreateRoomAsync(publicHostUserId, new CreateRoomRequest(maxPlayers, subjectSlugs));
            return Results.Redirect($"/host/{result.RoomCode}");
        })
        .RequireRateLimiting("create-room");

    app.MapPost("/join/{roomCode}/enter", async (
            string roomCode,
            HttpContext httpContext,
            IAntiforgery antiforgery,
            IGameRoomService gameRoomService,
            IHubContext<GameHub> hubContext) =>
        {
            await antiforgery.ValidateRequestAsync(httpContext);
            var form = await httpContext.Request.ReadFormAsync();
            var displayName = form["DisplayName"].ToString();
            var result = await gameRoomService.JoinRoomAsync(new JoinRoomRequest(roomCode, displayName), null);

            httpContext.Response.Cookies.Append(PlayerCookieName(roomCode), result.GuestToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = httpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(3)
            });

            var snapshot = await gameRoomService.GetSnapshotAsync(roomCode);
            await hubContext.Clients.Group(GameHub.RoomGroup(roomCode)).SendAsync("PlayerJoined", result.PlayerId, result.DisplayName);
            await hubContext.Clients.Group(GameHub.RoomGroup(roomCode)).SendAsync("PlayersUpdated", snapshot.Players);
            return Results.Redirect($"/play/{roomCode}");
        })
        .RequireRateLimiting("join");
}

static string PlayerCookieName(string roomCode) => $"WordGame.Player.{roomCode}";

public partial class Program;
