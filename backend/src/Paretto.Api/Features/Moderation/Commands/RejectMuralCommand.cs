using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Paretto.Domain.Enums;
using Paretto.Infrastructure.Data;

namespace Paretto.Api.Features.Moderation.Commands;

/// <summary>
/// Request to reject a Pending mural, moving it to Rejected (admin-only — see
/// `ModerationController`'s `[Authorize(Roles = "Administrator")]`). Spec Block 4. Same shape as
/// `ApproveMuralCommand` — reuses `ModeratedMuralNotFoundException`, `MuralNotPendingException` and
/// `ModerationPersistenceException` from that file instead of redefining them.
/// </summary>
public class RejectMuralCommand : IRequest<ModerationActionResponse>
{
    public Guid MuralId { get; set; }
}

public class RejectMuralCommandHandler : IRequestHandler<RejectMuralCommand, ModerationActionResponse>
{
    private readonly AppDbContext _dbContext;
    private readonly IMapper _mapper;

    public RejectMuralCommandHandler(AppDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<ModerationActionResponse> Handle(RejectMuralCommand request, CancellationToken cancellationToken)
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

        mural.Status = MuralStatus.Rejected;

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
