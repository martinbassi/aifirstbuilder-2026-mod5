using Microsoft.AspNetCore.Authentication;

namespace Paretto.Infrastructure.Auth;

/// <summary>
/// No custom configuration knobs yet — exists because `AuthenticationHandler&lt;TOptions&gt;`
/// requires an options type parameter. See <see cref="SessionAuthenticationHandler"/>.
/// </summary>
public class SessionAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
}
