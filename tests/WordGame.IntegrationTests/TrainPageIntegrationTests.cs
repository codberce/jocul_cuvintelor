using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using Xunit;

namespace WordGame.IntegrationTests;

public sealed class TrainPageIntegrationTests
{
    [Fact]
    public async Task Train_page_is_accessible_without_login()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Database:RunMigrationsOnStartup", "false");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Database:RunMigrationsOnStartup"] = "false",
                        ["SignalR:UseRedisBackplane"] = "false",
                        ["SignalR:UseAzureSignalR"] = "false"
                    });
                });
            });

        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/singleplayer");

        Assert.Contains("Antrenament solo", html);
        Assert.Contains("14 definitii, 4 minute", html);
    }

    [Fact]
    public async Task Blazor_framework_script_from_train_page_is_served()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Database:RunMigrationsOnStartup", "false");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Database:RunMigrationsOnStartup"] = "false",
                        ["SignalR:UseRedisBackplane"] = "false",
                        ["SignalR:UseAzureSignalR"] = "false"
                    });
                });
            });

        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/singleplayer");
        var match = Regex.Match(html, "<script src=\"(?<src>[^\"]*blazor\\.web[^\"]*\\.js|[^\"]*blazor\\.web\\.js)\"");
        Assert.True(match.Success, "The train page should emit the Blazor interactive script.");

        var scriptPath = match.Groups["src"].Value;
        if (!scriptPath.StartsWith('/'))
        {
            scriptPath = "/" + scriptPath.TrimStart('.', '/');
        }

        var response = await client.GetAsync(scriptPath);

        response.EnsureSuccessStatusCode();
    }
}
