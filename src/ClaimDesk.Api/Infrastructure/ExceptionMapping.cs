using ClaimDesk.Application.Abstractions;
using ClaimDesk.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaimDesk.Api.Infrastructure;

/// <summary>Maps domain and application exceptions to RFC 7807 problem responses.</summary>
public sealed class ExceptionMapping(IProblemDetailsService problemDetails, ILogger<ExceptionMapping> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            DomainValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            ActionNotPermittedException => (StatusCodes.Status403Forbidden, "Action not permitted"),
            InvalidTransitionException => (StatusCodes.Status409Conflict, "Invalid state transition"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "The claim was changed by someone else; reload and retry"),
            _ => (0, string.Empty),
        };

        if (status == 0)
        {
            logger.LogError(exception, "Unhandled exception");
            return false;
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exception is DbUpdateConcurrencyException ? null : exception.Message,
            },
        });
    }
}
