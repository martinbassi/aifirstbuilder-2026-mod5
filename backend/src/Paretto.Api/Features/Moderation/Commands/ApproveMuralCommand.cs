using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Paretto.Api.Common.Exceptions;
using Paretto.Domain.Enums;
using Paretto.Infrastructure.Data;

namespace Paretto.Api.Features.Moderation.Commands;

/// <summary>
/// Request to approve a Pending mural, moving it to Published (admin-only — see
/// `ModerationController`'s `[Authorize(Roles = "Administrator")]`). Spec Block 3.
/// </summary>
public class ApproveMuralCommand : IRequest<ModerationActionResponse>
{
    public Guid MuralId { get; set; }
}

public class ModerationActionResponse
{
    public Guid Id { get; set; }

    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Thrown when the mural id does not exist. Carries a single generic message (same precedent as
/// `MuralAccessDeniedException`) — this endpoint is admin-only, so there is no enumeration concern
/// here, but there is also no reason to leak more than "not found".
/// </summary>
public class ModeratedMuralNotFoundException : AppException
{
    public const string GenericMessage = "Mural not found.";

    public ModeratedMuralNotFoundException() : base(GenericMessage, StatusCodes.Status404NotFound)
    {
    }
}

/// <summary>
/// Thrown when the mural exists but is not `Pending` (already `Published` or `Rejected`). This is an
/// explicit read-then-check on `Status`, NOT real optimistic concurrency (no rowversion/ETag) — an
/// accepted-risk design per the PRD's RF-051 (see `docs/daw/prd/prd-FEAT-001c.md`, "Risks and
/// Mitigations"), not an oversight.
/// </summary>
public class MuralNotPendingException : AppException
{
    public const string GenericMessage = "Mural is not in Pending state.";

    public MuralNotPendingException() : base(GenericMessage, StatusCodes.Status409Conflict)
    {
    }
}

/// <summary>
/// Thrown when the moderation action's `SaveChangesAsync` call fails. Carries a single generic
/// message (same shape as `MuralPersistenceException`, `Features/Murals/Commands/CreateMuralCommand.cs`)
/// — the caller does not need DB-level detail, only that the action was not persisted. Also reused by
/// `RejectMuralCommand` (Block 4).
///
/// Inherits `AppException` so `ExceptionHandlingMiddleware` maps it to `500` generically.
/// </summary>
public class ModerationPersistenceException : AppException
{
    public const string GenericMessage = "Could not save the moderation action. Please try again.";

    public ModerationPersistenceException() : base(GenericMessage, StatusCodes.Status500InternalServerError)
    {
    }
}

public class ApproveMuralCommandHandler : IRequestHandler<ApproveMuralCommand, ModerationActionResponse>
{
    private readonly AppDbContext _dbContext;
    private readonly IMapper _mapper;

    public ApproveMuralCommandHandler(AppDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<ModerationActionResponse> Handle(ApproveMuralCommand request, CancellationToken cancellationToken)
    {
        var mural = await _dbContext.Murals
            .SingleOrDefaultAsync(m => m.Id == request.MuralId, cancellationToken);

        if (mural is null)
        {
            throw new ModeratedMuralNotFoundException();
        }

        if (mural.Status != MuralStatus.Pending)
        {
            throw new MuralNotPendingException();
        }

        mural.Status = MuralStatus.Published;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ModerationPersistenceException();
        }

        return _mapper.Map<ModerationActionResponse>(mural);
    }
}
