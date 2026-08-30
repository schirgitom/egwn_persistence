using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EGWNPersistence.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that validates requests using FluentValidation.
/// Throws <see cref="ValidationException"/> if validation fails.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count != 0)
        {
            logger.LogWarning(
                "Validation failed for {RequestType} with {ErrorCount} error(s): {Errors}",
                typeof(TRequest).Name,
                failures.Count,
                string.Join("; ", failures.Select(f => f.ErrorMessage)));

            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
