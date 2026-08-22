using FluentValidation;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Paretto.Api.Features.Murals.Queries;
using Paretto.Domain.Enums;
using Paretto.Infrastructure.Data;
using Paretto.Infrastructure.Storage;

namespace Paretto.Api.Features.Moderation.Queries;

/// <summary>
/// Request to list the Pending murals waiting for moderation, paginated (admin-only — see
/// `ModerationController`'s `[Authorize(Roles = "Administrator")]`). Spec Block 2.
/// </summary>
public class GetPendingMuralsQuery : IRequest<GetPendingMuralsResponse>
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

public class GetPendingMuralsResponse
{
    public MuralResponse[] Murals { get; set; } = [];

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }
}

/// <summary>
/// Hard cap on `pageSize` (mitigation of threat model R4) — without it, a client could request an
/// unbounded page and recreate the very problem pagination exists to solve.
/// </summary>
public class GetPendingMuralsQueryValidator : AbstractValidator<GetPendingMuralsQuery>
{
    public GetPendingMuralsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

public class GetPendingMuralsQueryHandler : IRequestHandler<GetPendingMuralsQuery, GetPendingMuralsResponse>
{
    private readonly AppDbContext _dbContext;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IMapper _mapper;

    public GetPendingMuralsQueryHandler(
        AppDbContext dbContext,
        IBlobStorageService blobStorageService,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _blobStorageService = blobStorageService;
        _mapper = mapper;
    }

    public async Task<GetPendingMuralsResponse> Handle(GetPendingMuralsQuery request, CancellationToken cancellationToken)
    {
        var pendingQuery = _dbContext.Murals.Where(m => m.Status == MuralStatus.Pending);

        // Oldest pending mural first — moderation queue, avoids a mural waiting indefinitely behind
        // newer submissions (spec Block 2).
        var totalCount = await pendingQuery.CountAsync(cancellationToken);
        var murals = await pendingQuery
            .OrderBy(m => m.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var muralResponses = murals.Select(mural =>
        {
            var response = _mapper.Map<MuralResponse>(mural);
            response.PhotoUrl = _blobStorageService.GenerateReadSasUrl(mural.PhotoBlobName, TimeSpan.FromMinutes(5));
            return response;
        }).ToArray();

        return new GetPendingMuralsResponse
        {
            Murals = muralResponses,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
        };
    }
}
