using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Api.Middleware;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext http, Exception exception, CancellationToken ct)
    {
        // Client menutup koneksi di tengah jalan: bukan error server
        if (exception is OperationCanceledException && http.RequestAborted.IsCancellationRequested)
        {
            http.Response.StatusCode = 499;
            return true;
        }

        var extensions = new Dictionary<string, object?> { ["traceId"] = http.TraceIdentifier };

        IResult result = exception switch
        {
            ValidationException validation => Results.ValidationProblem(
                BuildErrors(validation),
                title: "Validation failed",
                statusCode: StatusCodes.Status400BadRequest,
                type: "https://httpstatuses.com/400",
                extensions: extensions),

            AuthenticationFailedException => Problem(401, "Authentication failed", exception.Message, extensions),
            ForbiddenException => Problem(403, "Forbidden", exception.Message, extensions),
            NotFoundException => Problem(404, "Not found", exception.Message, extensions),
            ConflictException => Problem(409, "Conflict", exception.Message, extensions),
            DomainRuleException => Problem(409, "Conflict", exception.Message, extensions),

            _ => UnexpectedError(http, exception, extensions)
        };

        await result.ExecuteAsync(http);
        return true;
    }

    private IResult UnexpectedError(HttpContext http, Exception exception, IDictionary<string, object?> extensions)
    {
        // Detail lengkap hanya masuk log server, tidak dikirim ke client
        logger.LogError(exception, "Unhandled exception pada {Method} {Path}",
            http.Request.Method, http.Request.Path.Value);

        var detail = environment.IsDevelopment() ? exception.Message : null;
        return Problem(500, "An unexpected error occurred.", detail, extensions);
    }

    private static IResult Problem(int status, string title, string? detail, IDictionary<string, object?> extensions) =>
        Results.Problem(
            title: title,
            detail: detail,
            statusCode: status,
            type: $"https://httpstatuses.com/{status}",
            extensions: extensions);

    private static Dictionary<string, string[]> BuildErrors(ValidationException validation) =>
        validation.Errors
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

    private static string ToCamelCase(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToLowerInvariant(text[0]) + text[1..];
}