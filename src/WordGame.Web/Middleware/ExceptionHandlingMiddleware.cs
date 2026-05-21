using System.Net;
using System.Text.Json;
using FluentValidation;

namespace WordGame.Web.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var status = exception switch
        {
            ValidationException => HttpStatusCode.BadRequest,
            UnauthorizedAccessException => HttpStatusCode.Forbidden,
            KeyNotFoundException => HttpStatusCode.NotFound,
            InvalidOperationException => HttpStatusCode.BadRequest,
            _ => HttpStatusCode.InternalServerError
        };

        if (status == HttpStatusCode.InternalServerError)
        {
            logger.LogError(exception, "Eroare netratata.");
        }
        else
        {
            logger.LogWarning(exception, "Cerere respinsa cu status {StatusCode}.", (int)status);
        }

        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            error = status == HttpStatusCode.InternalServerError ? "A aparut o eroare interna." : exception.Message
        }));
    }
}
