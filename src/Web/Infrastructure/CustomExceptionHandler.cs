using Ardalis.GuardClauses;
using EbayClone.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EbayClone.Web.Infrastructure;

public class CustomExceptionHandler(ILogger<CustomExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            // Request binding failed (missing/malformed query value, bad JSON): the client's fault, not a server error.
            case BadHttpRequestException badRequest:
                httpContext.Response.StatusCode = badRequest.StatusCode;
                await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = badRequest.StatusCode,
                    Title = "Yêu cầu không hợp lệ.",
                    Detail = badRequest.Message,
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
                });
                return true;

            case EbayClone.Application.Common.Exceptions.ValidationException validationException:
                await HandleCustomValidationException(httpContext, validationException);
                return true;

            case FluentValidation.ValidationException fluentValidationException:
                await HandleFluentValidationException(httpContext, fluentValidationException);
                return true;

            case Ardalis.GuardClauses.NotFoundException notFoundException:
                await HandleNotFoundException(httpContext, notFoundException);
                return true;

            case KeyNotFoundException keyNotFoundException:
                await HandleNotFoundException(httpContext, keyNotFoundException);
                return true;

            case UnauthorizedAccessException unauthorizedAccessException:
                await HandleUnauthorizedAccessException(httpContext, unauthorizedAccessException);
                return true;

            case ForbiddenAccessException forbiddenAccessException:
                await HandleForbiddenAccessException(httpContext, forbiddenAccessException);
                return true;

            // W7-01: crypto failures must not echo exception text (may hint at key/ciphertext state)
            case System.Security.Cryptography.CryptographicException:
                await HandleSensitiveDataException(httpContext);
                return true;

            case InvalidOperationException invalidOperationException:
                await HandleBadRequestException(httpContext, invalidOperationException);
                return true;

            case ArgumentException argumentException:
                await HandleBadRequestException(httpContext, argumentException);
                return true;

            case Exception notFoundByName when notFoundByName.GetType().Name == "NotFoundException":
                await HandleNotFoundException(httpContext, exception);
                return true;

            // `throw new Exception("...")` is how handlers here report a user-facing rule ("Tài khoản của bạn đã bị khóa."):
            // keep showing that message.
            case not null when exception.GetType() == typeof(Exception):
                await HandleBadRequestException(httpContext, exception);
                return true;

            // Anything else (database, null reference, I/O, ...) is a bug or an outage: its message can expose table names,
            // SQL or file paths, so the client gets a generic 500 and the details go to the log only.
            default:
                logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Đã xảy ra lỗi hệ thống, vui lòng thử lại sau.",
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
                });
                return true;
        }
    }

    private static async Task HandleSensitiveDataException(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Sensitive data could not be verified or decrypted."
        });
    }

    private async Task HandleCustomValidationException(HttpContext httpContext, EbayClone.Application.Common.Exceptions.ValidationException exception)
    {
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        var detail = string.Join("; ", exception.Errors.SelectMany(kv => kv.Value));
        if (string.IsNullOrWhiteSpace(detail))
        {
            detail = exception.Message;
        }

        await httpContext.Response.WriteAsJsonAsync(new ValidationProblemDetails(exception.Errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Detail = detail
        });
    }

    private async Task HandleFluentValidationException(HttpContext httpContext, FluentValidation.ValidationException exception)
    {
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Detail = exception.Message,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        });
    }

    private async Task HandleNotFoundException(HttpContext httpContext, Exception exception)
    {
        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            Title = "The specified resource was not found.",
            Detail = exception.Message
        });
    }

    private async Task HandleUnauthorizedAccessException(HttpContext httpContext, Exception exception)
    {
        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = exception.Message,
            Type = "https://tools.ietf.org/html/rfc7235#section-3.1"
        });
    }

    private async Task HandleForbiddenAccessException(HttpContext httpContext, ForbiddenAccessException exception)
    {
        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = exception.Message,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3"
        });
    }

    private async Task HandleBadRequestException(HttpContext httpContext, Exception exception)
    {
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = exception.Message,
            Detail = exception.Message,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        });
    }
}
