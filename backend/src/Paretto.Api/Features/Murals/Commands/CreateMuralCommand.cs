using System.Security.Claims;
using FluentValidation;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Paretto.Api.Common.Exceptions;
using Paretto.Domain.Entities;
using Paretto.Domain.Enums;
using Paretto.Infrastructure.Data;
using Paretto.Infrastructure.Moderation;
using Paretto.Infrastructure.Storage;

namespace Paretto.Api.Features.Murals.Commands;

/// <summary>
/// Request to create a new mural. Deliberately has NO `UserId` property — same tampering
/// mitigation already established by `RegisterUserCommand` never accepting `Role` from the client
/// (see docs/daw/security/threat-FEAT-001b.md): the author is always the caller of the current
/// session, read server-side by the Handler from the `ClaimsPrincipal`, never from the request body.
/// </summary>
public class CreateMuralCommand : IRequest<CreateMuralResponse>
{
    public string Title { get; set; } = string.Empty;

    public IFormFile Photo { get; set; } = null!;

    // `InvariantGlobalization` is enabled process-wide (both `Paretto.Api.csproj` and
    // `Paretto.Api.Tests.csproj` — the latter is what actually applies in-process under
    // `WebApplicationFactory`), so MVC's default `SimpleTypeModelBinder` always parses `double`
    // with the invariant culture regardless of the host OS locale. No per-property model binder
    // needed.
    public double Latitude { get; set; }

    public double Longitude { get; set; }
}

public class CreateMuralCommandValidator : AbstractValidator<CreateMuralCommand>
{
    private const long MaxPhotoSizeBytes = 10 * 1024 * 1024;

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] RiffTag = "RIFF"u8.ToArray();
    private static readonly byte[] WebPTag = "WEBP"u8.ToArray();

    public CreateMuralCommandValidator()
    {
         RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Photo)
            .NotNull()
            .WithMessage("Photo is required.");

        RuleFor(x => x.Photo)
            .Must(photo => photo!.Length <= MaxPhotoSizeBytes)
            .WithMessage($"Photo must not exceed {MaxPhotoSizeBytes} bytes.")
            .When(x => x.Photo is not null);

        RuleFor(x => x.Photo)
            .MustAsync((photo, cancellationToken) => HasValidImageSignatureAsync(photo!, cancellationToken))
            .WithMessage("Photo must be a valid JPEG, PNG or WebP file.")
            .When(x => x.Photo is not null);

        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
    }

    /// <summary>
    /// Reads the first bytes of the file to check its magic-number signature against JPEG, PNG and
    /// WebP — `ContentType`/extension alone are not enough, both are trivially spoofable by the
    /// client (threat model R3). Explicitly resets the stream position to 0 before returning, per
    /// the spec, so the Handler gets a fresh stream when it later calls `Photo.OpenReadStream()`
    /// itself.
    /// </summary>
    private static async Task<bool> HasValidImageSignatureAsync(IFormFile photo, CancellationToken cancellationToken)
    {
        var stream = photo.OpenReadStream();
        try
        {
            var header = new byte[12];
            var totalRead = 0;
            while (totalRead < header.Length)
            {
                var read = await stream.ReadAsync(header.AsMemory(totalRead, header.Length - totalRead), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            return IsJpeg(header, totalRead) || IsPng(header, totalRead) || IsWebP(header, totalRead);
        }
        finally
        {
            stream.Position = 0;
        }
    }

    private static bool IsJpeg(byte[] header, int length) =>
        length >= JpegSignature.Length && header.AsSpan(0, JpegSignature.Length).SequenceEqual(JpegSignature);

    private static bool IsPng(byte[] header, int length) =>
        length >= PngSignature.Length && header.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature);

    private static bool IsWebP(byte[] header, int length) =>
        length >= 12
        && header.AsSpan(0, 4).SequenceEqual(RiffTag)
        && header.AsSpan(8, 4).SequenceEqual(WebPTag);
}

public class CreateMuralResponse
{
    public Guid Id { get; set; }

    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Thrown when either the photo upload to Storage or the `Mural` save to the database fails. Carries
/// a single generic message (FR-12) — the caller does not need to know which of the two operations
/// failed, only that the mural was not saved.
///
/// Inherits `AppException` so `ExceptionHandlingMiddleware` maps it to `500` generically, same
/// pattern as `DuplicateAccountException`/`InvalidCredentialsException`.
/// </summary>
public class MuralPersistenceException : AppException
{
    public const string GenericMessage = "Could not save the mural. Please try again.";

    public MuralPersistenceException() : base(GenericMessage, StatusCodes.Status500InternalServerError)
    {
    }
}

public class CreateMuralCommandHandler : IRequestHandler<CreateMuralCommand, CreateMuralResponse>
{
    private readonly AppDbContext _dbContext;
    private readonly IBlobStorageService _blobStorageService;
    private readonly INsfwContentScanner _nsfwContentScanner;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMapper _mapper;

    public CreateMuralCommandHandler(
        AppDbContext dbContext,
        IBlobStorageService blobStorageService,
        INsfwContentScanner nsfwContentScanner,
        IHttpContextAccessor httpContextAccessor,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _blobStorageService = blobStorageService;
        _nsfwContentScanner = nsfwContentScanner;
        _httpContextAccessor = httpContextAccessor;
        _mapper = mapper;
    }

    public async Task<CreateMuralResponse> Handle(CreateMuralCommand request, CancellationToken cancellationToken)
    {
        var userId = ReadUserId(_httpContextAccessor.HttpContext);

        // Read the file exactly once into a byte[]; independent streams are then created from it
        // for the Storage upload and the NSFW scan, so neither consumer's positioning can affect
        // the other (spec Block 4).
        byte[] photoBytes;
        await using (var sourceStream = request.Photo.OpenReadStream())
        using (var buffer = new MemoryStream())
        {
            await sourceStream.CopyToAsync(buffer, cancellationToken);
            photoBytes = buffer.ToArray();
        }

        // Always generated server-side — never derived from the client's original file name
        // (path traversal/overwrite mitigation, threat model R4).
        var blobName = $"{Guid.NewGuid()}{ExtensionFor(request.Photo.ContentType)}";

        try
        {
            using var uploadStream = new MemoryStream(photoBytes);
            await _blobStorageService.UploadAsync(uploadStream, blobName, request.Photo.ContentType, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new MuralPersistenceException();
        }

        NsfwScanResult scanResult;
        using (var scanStream = new MemoryStream(photoBytes))
        {
            // Never throws (guaranteed by NsfwSpyContentScanner, Block 3) — any failure/timeout of
            // the underlying model already comes back as Inconclusive.
            scanResult = await _nsfwContentScanner.ScanAsync(scanStream, cancellationToken);
        }

        var mural = new Mural
        {
            UserId = userId,
            Title = request.Title,
            PhotoBlobName = blobName,
            // Mural.CreateLocation es el único punto del código C# que decide el orden de ejes
            // (FEAT-009, threat model R2) — nunca se construye un Point a mano acá.
            Location = Mural.CreateLocation(request.Latitude, request.Longitude),
            // Clean and Inconclusive both leave the mural Pending (FR-08/FR-09/FR-10); only Nsfw
            // is Rejected.
            Status = scanResult == NsfwScanResult.Nsfw ? MuralStatus.Rejected : MuralStatus.Pending,
        };

        _dbContext.Murals.Add(mural);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Same pattern as RegisterUserCommandHandler: the mural is never registered as saved.
            // Note (accepted design, see spec Block 4): if the Storage upload above already
            // succeeded, the blob is now orphaned — the container is private and never exposed or
            // linked from anywhere, so this is unused storage, not a security risk. No
            // compensation/rollback of the blob is implemented for this ticket.
            throw new MuralPersistenceException();
        }

        return _mapper.Map<CreateMuralResponse>(mural);
    }

    private static Guid ReadUserId(HttpContext? httpContext)
    {
        // Defensive only: [Authorize] on the controller action already requires a valid session
        // carrying this claim, so this branch should be unreachable in practice. Not worth a typed
        // domain exception for a case that should never execute.
        var claim = httpContext?.User.FindFirst(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated request is missing a NameIdentifier claim.");

        return Guid.Parse(claim.Value);
    }

    private static string ExtensionFor(string? contentType) => contentType?.ToLowerInvariant() switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => string.Empty,
    };
}
