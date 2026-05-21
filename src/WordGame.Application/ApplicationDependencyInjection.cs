using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using WordGame.Application.Interfaces;
using WordGame.Application.Services;

namespace WordGame.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationDependencyInjection).Assembly);
        services.AddSingleton<IAnswerNormalizer, AnswerNormalizer>();
        services.AddSingleton<IRoomCodeGenerator, RoomCodeGenerator>();
        services.AddSingleton<ILeaderboardService, LeaderboardService>();
        services.AddScoped<IScoreCalculator, ScoreCalculator>();
        services.AddScoped<ISinglePlayerTrainingService, SinglePlayerTrainingService>();
        return services;
    }
}
