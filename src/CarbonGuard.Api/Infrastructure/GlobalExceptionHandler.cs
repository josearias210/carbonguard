using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CarbonGuard.Api.Infrastructure;

public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        int statusCode = exception switch
        {
            BadHttpRequestException badRequestException => badRequestException.StatusCode,
            JsonException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError,
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(
                logger,
                httpContext.Request.Method,
                httpContext.Request.Path,
                exception);
        }
        else
        {
            LogRejectedRequest(
                logger,
                httpContext.Request.Method,
                httpContext.Request.Path,
                statusCode);
        }

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = statusCode >= StatusCodes.Status500InternalServerError
                    ? "An unexpected error occurred."
                    : "The request is invalid.",
                Detail = statusCode >= StatusCodes.Status500InternalServerError
                    ? "The request could not be completed. Use the traceId when contacting support."
                    : "Check that the JSON body matches the documented request schema.",
                Extensions = { ["traceId"] = httpContext.TraceIdentifier },
            },
            Exception = exception,
        });
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Unhandled exception while processing {Method} {Path}")]
    private static partial void LogUnhandledException(
        ILogger logger,
        string method,
        string path,
        Exception exception);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Rejected request {Method} {Path} with status code {StatusCode}")]
    private static partial void LogRejectedRequest(
        ILogger logger,
        string method,
        string path,
        int statusCode);
}
