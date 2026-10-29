using BeautyAgenda.Application.Common;
using BeautyAgenda.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BeautyAgenda.Api.Infrastructure;

/// <summary>Maps application exceptions to RFC 7807 responses without leaking internals.</summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        ProblemDetails problem = exception switch
        {
            ValidationException ve => new ValidationProblemDetails(
                ve.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Um ou mais campos são inválidos.",
            },
            NotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound, Title = "Registro não encontrado.", Detail = exception.Message,
            },
            ConflictException or DomainException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict, Title = "Conflito.", Detail = exception.Message,
            },
            BadHttpRequestException bad => new ProblemDetails
            {
                Status = bad.StatusCode, Title = "Requisição inválida.",
            },
            OperationCanceledException when context.RequestAborted.IsCancellationRequested => new ProblemDetails
            {
                Status = 499, Title = "Requisição cancelada.",
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Ocorreu um erro inesperado. Tente novamente.",
            },
        };

        if (problem.Status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
