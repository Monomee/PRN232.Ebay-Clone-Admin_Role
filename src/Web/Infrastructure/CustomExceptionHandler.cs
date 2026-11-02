using Ardalis.GuardClauses;
using EbayClone.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EbayClone.Web.Infrastructure;

public class CustomExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
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

            case InvalidOperationException invalidOperationException:
                await HandleBadRequestException(httpContext, invalidOperationException);
                return true;

            case ArgumentException argumentException:
                await HandleBadRequestException(httpContext, argumentException);
                return true;

            case Exception notFoundByName when notFoundByName.GetType().Name == "NotFoundException":
                await HandleNotFoundException(httpContext, exception);
                return true;

            default:
                await HandleBadRequestException(httpContext, exception);
                return true;
        }
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
