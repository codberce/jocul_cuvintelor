using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using WordGame.Application;
using WordGame.Application.DTOs;
using WordGame.Application.Interfaces;
using WordGame.Application.Options;
using WordGame.Infrastructure;
using WordGame.Infrastructure.Identity;
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
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAntiforgery();
builder.Services.AddAuthorization();
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
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.PermitLimit = rateLimitOptions.LoginPerMinute;
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
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

app.MapHealthChecks("/health");
app.MapGet("/train", (HttpRequest request) => Results.Redirect($"/singleplayer{request.QueryString}"));
app.MapHub<GameHub>("/gameHub").RequireRateLimiting("submit-answer");
MapAuthEndpoints(app);
MapGameEndpoints(app);
MapAdminExportEndpoints(app);
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

if (app.Configuration.GetValue("Database:RunMigrationsOnStartup", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().MigrateAndSeedAsync();
}

app.Run();

static void MapAuthEndpoints(WebApplication app)
{
    app.MapPost("/auth/login", async (
            HttpContext httpContext,
            IAntiforgery antiforgery,
            SignInManager<ApplicationUser> signInManager) =>
        {
            await antiforgery.ValidateRequestAsync(httpContext);
            var form = await httpContext.Request.ReadFormAsync();
            var email = form["Email"].ToString();
            var password = form["Password"].ToString();
            var remember = string.Equals(form["RememberMe"].ToString(), "on", StringComparison.OrdinalIgnoreCase);

            var result = await signInManager.PasswordSignInAsync(email, password, remember, lockoutOnFailure: true);
            return result.Succeeded ? Results.Redirect("/host") : Results.Redirect("/login?error=1");
        })
        .RequireRateLimiting("login");

    app.MapPost("/auth/logout", async (
            HttpContext httpContext,
            IAntiforgery antiforgery,
            SignInManager<ApplicationUser> signInManager) =>
        {
            await antiforgery.ValidateRequestAsync(httpContext);
            await signInManager.SignOutAsync();
            return Results.Redirect("/");
        })
        .RequireAuthorization();
}

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

static void MapAdminExportEndpoints(WebApplication app)
{
    app.MapGet("/admin/questions/export.json", async (IDbContextFactory<ApplicationDbContext> dbFactory) =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var questions = await db.Questions.Include(x => x.Category).Include(x => x.Subject).AsNoTracking()
                .OrderBy(x => x.Subject!.Name).ThenBy(x => x.Category!.Name).ThenBy(x => x.Answer)
                .Select(x => new ImportQuestionDto(x.Answer, x.Definition, x.Subject!.Slug, x.Category!.Name, x.Difficulty.ToString(), x.TimeLimitSeconds, x.IsActive))
                .ToListAsync();
            return Results.Json(questions);
        })
        .RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });

    app.MapGet("/admin/questions/export.csv", async (IDbContextFactory<ApplicationDbContext> dbFactory) =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var rows = await db.Questions.Include(x => x.Category).Include(x => x.Subject).AsNoTracking()
                .OrderBy(x => x.Subject!.Name).ThenBy(x => x.Category!.Name).ThenBy(x => x.Answer)
                .Select(x => new { x.Answer, x.Definition, Subject = x.Subject!.Slug, Category = x.Category!.Name, Difficulty = x.Difficulty.ToString(), x.TimeLimitSeconds, x.IsActive })
                .ToListAsync();

            var csv = new StringBuilder("Answer,Definition,Subject,Category,Difficulty,TimeLimitSeconds,IsActive\n");
            foreach (var row in rows)
            {
                csv.AppendLine($"{Csv(row.Answer)},{Csv(row.Definition)},{Csv(row.Subject)},{Csv(row.Category)},{Csv(row.Difficulty)},{row.TimeLimitSeconds},{row.IsActive}");
            }

            return Results.Text(csv.ToString(), "text/csv", Encoding.UTF8);
        })
        .RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });
}

static string PlayerCookieName(string roomCode) => $"WordGame.Player.{roomCode}";

static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

public partial class Program;
