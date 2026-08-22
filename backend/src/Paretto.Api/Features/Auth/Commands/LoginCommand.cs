using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Paretto.Api.Common.Exceptions;
using Paretto.Domain.Entities;
using Paretto.Infrastructure.Data;
using Paretto.Infrastructure.Security;

namespace Paretto.Api.Features.Auth.Commands;

public class LoginCommand : IRequest<LoginResponse>
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public string Role { get; set; } = string.Empty;
}

/// <summary>
/// Thrown when the username does not exist, OR the password does not match. Single generic
/// message (FR-05/AC-05) — same reasoning as <see cref="DuplicateAccountException"/> in Block 5:
/// the caller must not be able to tell, from the error text, which of the two failed, or the error
/// becomes a username-enumeration oracle.
///
/// Inherits `AppException` so `ExceptionHandlingMiddleware` maps it to `401` generically, without
/// the controller catching it by hand (same pattern established in Block 5).
/// </summary>
public class InvalidCredentialsException : AppException
{
    public const string GenericMessage = "Invalid username or password.";

    public InvalidCredentialsException() : base(GenericMessage, StatusCodes.Status401Unauthorized)
    {
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionTokenGenerator _sessionTokenGenerator;

    public LoginCommandHandler(
        AppDbContext dbContext,
        IPasswordHasher passwordHasher,
        ISessionTokenGenerator sessionTokenGenerator)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _sessionTokenGenerator = sessionTokenGenerator;
    }

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.SingleOrDefaultAsync(
            u => u.Username == request.Username,
            cancellationToken);

        // Same generic failure whether the username does not exist or the password is wrong —
        // never distinguish the two (FR-05/AC-05).
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        var (rawToken, tokenHash) = _sessionTokenGenerator.Generate();
        var expiresAt = DateTime.UtcNow.AddDays(7); // NFR-03

        _dbContext.Sessions.Add(new Session
        {
            TokenHash = tokenHash,
            UserId = user.Id,
            ExpiresAt = expiresAt,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        // RawToken is the only time the token exists in plaintext outside this request's memory —
        // only TokenHash was persisted (mitigation R2).
        return new LoginResponse { Token = rawToken, ExpiresAt = expiresAt, Role = user.Role.ToString() };
    }
}
