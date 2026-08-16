using FluentValidation;
using MediatR;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Paretto.Api.Common.Exceptions;
using Paretto.Domain.Entities;
using Paretto.Domain.Enums;
using Paretto.Infrastructure.Data;
using Paretto.Infrastructure.Security;

namespace Paretto.Api.Features.Auth.Commands;

/// <summary>
/// Request to create a new account. Deliberately has NO `Role` property — mitigation R1 of the
/// threat model (docs/daw/security/threat-FEAT-001a.md): nothing in the request body can influence
/// the role assigned to the created account, regardless of what extra fields a client sends (a
/// "role" field in the raw JSON body is simply ignored by model binding, since there is no matching
/// property to bind to).
/// </summary>
public class RegisterUserCommand : IRequest<RegisterUserResponse>
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}

public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty()
            .Length(8, 128)
            .Matches("[A-Za-z]").WithMessage("Password must contain at least one letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");
    }
}

public class RegisterUserResponse
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;
}

/// <summary>
/// Thrown when the requested Username or Email is already in use. Carries a single generic
/// message on purpose (FR-02/AC-02): the caller must not be able to tell, from the error text,
/// which of the two fields collided — that would let an attacker enumerate registered accounts.
///
/// Inherits `AppException` (Round 2 correction of Block 5) so `ExceptionHandlingMiddleware` maps it
/// to `400` generically, without the controller catching it by hand.
/// </summary>
public class DuplicateAccountException : AppException
{
    public const string GenericMessage = "Username or email is already in use.";

    public DuplicateAccountException() : base(GenericMessage, StatusCodes.Status400BadRequest)
    {
    }
}

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, RegisterUserResponse>
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IMapper _mapper;

    public RegisterUserCommandHandler(AppDbContext dbContext, IPasswordHasher passwordHasher, IMapper mapper)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _mapper = mapper;
    }

    public async Task<RegisterUserResponse> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var accountAlreadyExists = await _dbContext.Users.AnyAsync(
            u => u.Username == request.Username || u.Email == request.Email,
            cancellationToken);

        if (accountAlreadyExists)
        {
            throw new DuplicateAccountException();
        }

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            // Hardcoded on the server, never taken from the request (mitigation R1).
            Role = UserRole.Standard,
        };

        _dbContext.Users.Add(user);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Defense in depth: a race between two concurrent requests with the same
            // Username/Email can only be caught by the DB's unique constraint, not by the
            // AnyAsync check above. Translated to the same generic duplicate-account error.
            throw new DuplicateAccountException();
        }

        return _mapper.Map<RegisterUserResponse>(user);
    }
}
