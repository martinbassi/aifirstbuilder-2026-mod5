using FluentValidation;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Paretto.Domain.Entities;
using Paretto.Domain.Enums;
using Paretto.Infrastructure.Data;
using Paretto.Infrastructure.Storage;

namespace Paretto.Api.Features.Discovery.Queries;

/// <summary>
/// Request to list `Published` murals within a radius around a given location, sorted by distance.
/// Public endpoint (no session, see `DiscoveryController`, Block 3). Spec Block 2 of FEAT-001d.
/// </summary>
public class GetNearbyMuralsQuery : IRequest<GetNearbyMuralsResponse>
{
    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>
    /// Null → default of 5 km applied in the Handler, not in the Validator (a null radius is valid
    /// input, not a validation failure).
    /// </summary>
    public double? RadiusKm { get; set; }
}

public class NearbyMuralItemResponse
{
    public Guid Id { get; set; }

    public string PhotoUrl { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public DateTime CreatedAt { get; set; }

    public double DistanceKm { get; set; }
}

public class GetNearbyMuralsResponse
{
    public List<NearbyMuralItemResponse> Items { get; set; } = [];
}

public class GetNearbyMuralsQueryValidator : AbstractValidator<GetNearbyMuralsQuery>
{
    public GetNearbyMuralsQueryValidator()
    {
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.RadiusKm).InclusiveBetween(0.1, 50).When(x => x.RadiusKm is not null);
    }
}

public class GetNearbyMuralsQueryHandler : IRequestHandler<GetNearbyMuralsQuery, GetNearbyMuralsResponse>
{
    /// <summary>Safety cap on results (mitigation of threat model R2) — bounds the cost of sorting
    /// and serializing in the worst case, on top of Block 3's rate limit.</summary>
    private const int MaxResults = 200;

    private readonly AppDbContext _dbContext;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IMapper _mapper;

    public GetNearbyMuralsQueryHandler(
        AppDbContext dbContext,
        IBlobStorageService blobStorageService,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _blobStorageService = blobStorageService;
        _mapper = mapper;
    }

    public async Task<GetNearbyMuralsResponse> Handle(GetNearbyMuralsQuery request, CancellationToken cancellationToken)
    {
        var radiusKm = request.RadiusKm ?? 5.0;

        // Único punto donde se construye el `Point` de búsqueda — siempre vía el factory de `Mural`
        // (mitigación de threat model R2, nunca `new Point(...)` a mano).
        var searchPoint = Mural.CreateLocation(request.Latitude, request.Longitude);

        // `Status == Published` es siempre la primera cláusula — nunca se omite ni es condicional
        // (mitigación de threat model R1: un mural Pending/Rejected nunca debe ser alcanzable a
        // través de este endpoint público sin sesión).
        // La consulta espacial vía LINQ-to-Entities (`Location.Distance(searchPoint)`, traducida a
        // `STDistance` de SQL Server sobre la columna `geography`) reemplaza el bounding box +
        // Haversine en memoria: el índice espacial de Block 1 acota el candidate set sin necesidad de
        // un bounding box manual. Nunca SQL crudo con interpolación de lat/lng/radius — LINQ-to-
        // Entities parametriza automáticamente (mitigación de threat model R1, no negociable).
        var candidates = await _dbContext.Murals
            .Where(m => m.Status == MuralStatus.Published)
            .Where(m => m.Location.Distance(searchPoint) <= radiusKm * 1000)
            .OrderBy(m => m.Location.Distance(searchPoint))
            .Take(MaxResults)
            .Select(m => new { Mural = m, DistanceKm = m.Location.Distance(searchPoint) / 1000 })
            .ToListAsync(cancellationToken);

        var items = candidates
            .Select(x =>
            {
                var response = _mapper.Map<NearbyMuralItemResponse>(x.Mural);
                response.PhotoUrl = _blobStorageService.GenerateReadSasUrl(x.Mural.PhotoBlobName, TimeSpan.FromMinutes(5));
                response.DistanceKm = x.DistanceKm;
                return response;
            })
            .ToList();

        return new GetNearbyMuralsResponse
        {
            Items = items,
        };
    }
}
