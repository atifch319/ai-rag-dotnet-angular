using System.Net;
using System.Text.Json;
using MyAi.Application.Common.Exceptions;

namespace MyAi.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, body) = exception switch
        {
            ValidationException validationException => (
                HttpStatusCode.BadRequest,
                (object)new { title = "Validation failed", errors = validationException.Errors }),
            NotFoundException notFoundException => (
                HttpStatusCode.NotFound,
                (object)new { title = notFoundException.Message }),
            _ => (
                HttpStatusCode.InternalServerError,
                (object)new { title = "An unexpected error occurred." })
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception");
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(body, SerializerOptions));
    }
}
