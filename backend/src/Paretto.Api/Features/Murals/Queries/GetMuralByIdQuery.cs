using System.Security.Claims;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Paretto.Api.Common.Exceptions;
using Paretto.Domain.Enums;
using Paretto.Infrastructure.Data;
using Paretto.Infrastructure.Storage;

namespace Paretto.Api.Features.Murals.Queries;

/// <summary>
/// Request to fetch a single mural by Id, including a short-lived read-only SAS URL for its photo
/// (FR-15/FR-16, spec Block 5).
/// </summary>
public class GetMuralByIdQuery : IRequest<MuralResponse>
{
    public Guid Id { get; set; }
}

public class MuralResponse
{
    public Guid Id { get; set; }

    public string Status { get; set; } = string.Empty;

    public string PhotoUrl { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Thrown both when the mural does not exist AND when it exists but the caller is not allowed to
/// see it (Pending/Rejected mural whose owner is someone else, and the caller is not an
/// Administrator). Carries a single generic message on purpose — mirroring the precedent already
/// established by `DuplicateAccountException` in this repo — so the response never lets a caller
/// distinguish "no existe" from "existe pero no tenés acceso" (anti-enumeration mitigation, threat
/// model R1).
/// </summary>
public class MuralAccessDeniedException : AppException
{
    public const string GenericMessage = "Mural not found.";

    public MuralAccessDeniedException() : base(GenericMessage, StatusCodes.Status404NotFound)
    {
    }
}

public class GetMuralByIdQueryHandler : IRequestHandler<GetMuralByIdQuery, MuralResponse>
{
    private readonly AppDbContext _dbContext;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMapper _mapper;

    public GetMuralByIdQueryHandler(
        AppDbContext dbContext,
        IBlobStorageService blobStorageService,
        IHttpContextAccessor httpContextAccessor,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _blobStorageService = blobStorageService;
        _httpContextAccessor = httpContextAccessor;
        _mapper = mapper;
    }

    public async Task<MuralResponse> Handle(GetMuralByIdQuery request, CancellationToken cancellationToken)
    {
        var mural = await _dbContext.Murals
            .SingleOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

        if (mural is null)
        {
            throw new MuralAccessDeniedException();
        }

        if (mural.Status is MuralStatus.Pending or MuralStatus.Rejected)
        {
            var (userId, role) = ReadCallerIdentity(_httpContextAccessor.HttpContext);

            // This check applies to the entire response, not just PhotoUrl — someone without access
            // does not learn the coordinates or the status either (FR-16 + RF-013 spirit, spec Block
            // 5).
            if (userId != mural.UserId && role != UserRole.Administrator)
            {
                throw new MuralAccessDeniedException();
            }
        }

        var photoUrl = _blobStorageService.GenerateReadSasUrl(mural.PhotoBlobName, TimeSpan.FromMinutes(5));

        var response = _mapper.Map<MuralResponse>(mural);
        response.PhotoUrl = photoUrl;
        return response;
    }

    private static (Guid UserId, UserRole Role) ReadCallerIdentity(HttpContext? httpContext)
    {
        // Defensive only: [Authorize] on the controller action already requires a valid session
        // carrying both claims, so this branch should be unreachable in practice. Same precedent as
        // CreateMuralCommandHandler.ReadUserId.
        var user = httpContext?.User
            ?? throw new InvalidOperationException("Authenticated request is missing an HttpContext.");

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated request is missing a NameIdentifier claim.");
        var roleClaim = user.FindFirst(ClaimTypes.Role)
            ?? throw new InvalidOperationException("Authenticated request is missing a Role claim.");

        return (Guid.Parse(userIdClaim.Value), Enum.Parse<UserRole>(roleClaim.Value));
    }
}
