using Microsoft.AspNetCore.Http;
using Paretto.Api.Common.Exceptions;

namespace Paretto.Api.Features.Addresses;

/// <summary>
/// Thrown by both Handlers in this feature (`SearchAddressesQueryHandler`,
/// `ReverseGeocodeQueryHandler`) when `IAddressProviderClient` reports
/// `AddressProviderOutcome.Unavailable` — same pattern as `MuralAccessDeniedException`
/// (`GetMuralByIdQuery.cs`): parameterless constructor, fixed generic message, status code baked in.
/// `ExceptionHandlingMiddleware` already translates any `AppException` to `ProblemDetails` without
/// needing to know about this type specifically (AC-19).
/// </summary>
public class AddressProviderUnavailableException : AppException
{
    public const string GenericMessage = "The address service is currently unavailable.";

    public AddressProviderUnavailableException() : base(GenericMessage, StatusCodes.Status503ServiceUnavailable)
    {
    }
}
