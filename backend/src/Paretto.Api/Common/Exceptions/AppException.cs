namespace Paretto.Api.Common.Exceptions;

/// <summary>
/// Base for domain-level exceptions that know their own HTTP status code. Round-2 correction of
/// Block 5: instead of `ExceptionHandlingMiddleware` growing a `catch` clause per concrete exception
/// type (which would mean touching it again for every new domain exception Block 6/7 introduces —
/// e.g. invalid-credentials, expired-session), the middleware pattern-matches on this single base
/// type and reads `StatusCode` off it. Domain code still throws specific, typed exceptions per
/// `AGENTS.md` ("never a generic Exception") — this only adds the HTTP mapping as a property on
/// them, it does not change what gets thrown or where.
/// </summary>
public abstract class AppException : Exception
{
    public int StatusCode { get; }

    protected AppException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}
