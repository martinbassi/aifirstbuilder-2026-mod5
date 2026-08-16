using FluentValidation;
using MediatR;

namespace Paretto.Api.Common.Behaviors;

/// <summary>
/// Centralizes MediatR pipeline validation: runs every registered FluentValidation validator for
/// TRequest before it reaches its Handler, instead of each controller invoking `IValidator&lt;T&gt;`
/// by hand (Round 2 correction of Block 5's AuthController — the manual validation the controller
/// used to do belongs here so it applies uniformly to every Command/Query, current and future).
/// On failure it throws `FluentValidation.ValidationException` with the accumulated
/// `ValidationFailure`s; `ExceptionHandlingMiddleware` (Common/Middleware) is what translates that
/// into the 422 `ValidationProblemDetails` response — this behavior only decides "is the request
/// valid", never how that gets serialized over HTTP.
/// </summary>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next(cancellationToken);
        }

        var validationResults = await Task.WhenAll(
            _validators.Select(validator => validator.ValidateAsync(request, cancellationToken)));

        var failures = validationResults
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
