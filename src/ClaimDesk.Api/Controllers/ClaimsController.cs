using ClaimDesk.Api.Auth;
using ClaimDesk.Application.Claims;
using ClaimDesk.Application.Uploads;
using ClaimDesk.Domain.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimDesk.Api.Controllers;

[ApiController]
[Route("api/claims")]
public sealed class ClaimsController(ClaimService claims, IAuthorizationService authorization) : ControllerBase
{
    private static readonly TimeSpan EvidenceLinkLifetime = TimeSpan.FromMinutes(5);

    [HttpPost]
    [Authorize(Policy = Policies.Claimant)]
    public async Task<ActionResult<ClaimDetailDto>> File(FileClaimRequest request, CancellationToken ct)
    {
        var actor = User.ToActor();
        var claim = await claims.FileAsync(actor, request, ct);
        return CreatedAtAction(nameof(Get), new { id = claim.Id }, ClaimDetailDto.From(claim, actor));
    }

    [HttpGet]
    [Authorize(Policy = Policies.AnyRole)]
    public Task<IReadOnlyList<ClaimSummaryDto>> List([FromQuery] ClaimStatus? status, CancellationToken ct) =>
        claims.ListAsync(User.ToActor(), status, ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.AnyRole)]
    public async Task<ActionResult<ClaimDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var claim = await claims.GetAsync(id, ct);
        if (!await CanAsync(claim, ClaimOperations.View))
        {
            return Forbid();
        }

        return ClaimDetailDto.From(claim, User.ToActor());
    }

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = Policies.AnyRole)]
    public async Task<ActionResult<IReadOnlyList<ClaimEventDto>>> History(Guid id, CancellationToken ct)
    {
        var claim = await claims.GetAsync(id, ct);
        if (!await CanAsync(claim, ClaimOperations.View))
        {
            return Forbid();
        }

        return Ok(await claims.HistoryAsync(id, ct));
    }

    [HttpPost("{id:guid}/start-review")]
    [Authorize(Policy = Policies.Staff)]
    public Task<ActionResult<ClaimDetailDto>> StartReview(Guid id, TransitionRequest request, CancellationToken ct) =>
        TransitionAsync(id, ClaimAction.StartReview, request, ct);

    [HttpPost("{id:guid}/request-info")]
    [Authorize(Policy = Policies.Staff)]
    public Task<ActionResult<ClaimDetailDto>> RequestInfo(Guid id, TransitionRequest request, CancellationToken ct) =>
        TransitionAsync(id, ClaimAction.RequestInfo, request, ct);

    [HttpPost("{id:guid}/provide-info")]
    [Authorize(Policy = Policies.Claimant)]
    public Task<ActionResult<ClaimDetailDto>> ProvideInfo(Guid id, TransitionRequest request, CancellationToken ct) =>
        TransitionAsync(id, ClaimAction.ProvideInfo, request, ct);

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Policies.Staff)]
    public Task<ActionResult<ClaimDetailDto>> Approve(Guid id, TransitionRequest request, CancellationToken ct) =>
        TransitionAsync(id, ClaimAction.Approve, request, ct);

    [HttpPost("{id:guid}/deny")]
    [Authorize(Policy = Policies.Staff)]
    public Task<ActionResult<ClaimDetailDto>> Deny(Guid id, TransitionRequest request, CancellationToken ct) =>
        TransitionAsync(id, ClaimAction.Deny, request, ct);

    [HttpPost("{id:guid}/mark-paid")]
    [Authorize(Policy = Policies.Admin)]
    public Task<ActionResult<ClaimDetailDto>> MarkPaid(Guid id, TransitionRequest request, CancellationToken ct) =>
        TransitionAsync(id, ClaimAction.MarkPaid, request, ct);

    [HttpPost("{id:guid}/evidence")]
    [Authorize(Policy = Policies.AnyRole)]
    [RequestSizeLimit(EvidencePolicy.MaxBytes + (64 * 1024))]
    [RequestFormLimits(MultipartBodyLengthLimit = EvidencePolicy.MaxBytes + (64 * 1024))]
    public async Task<ActionResult<EvidenceDto>> UploadEvidence(Guid id, IFormFile file, CancellationToken ct)
    {
        var claim = await claims.GetAsync(id, ct);
        if (!await CanAsync(claim, ClaimOperations.AddEvidence))
        {
            return Forbid();
        }

        await using var stream = file.OpenReadStream();
        var evidence = await claims.AddEvidenceAsync(claim, User.ToActor(), file.FileName, stream, file.Length, ct);
        return StatusCode(StatusCodes.Status201Created, EvidenceDto.From(evidence));
    }

    [HttpGet("{id:guid}/evidence")]
    [Authorize(Policy = Policies.AnyRole)]
    public async Task<ActionResult<IReadOnlyList<EvidenceDto>>> ListEvidence(Guid id, CancellationToken ct)
    {
        var claim = await claims.GetAsync(id, ct);
        if (!await CanAsync(claim, ClaimOperations.View))
        {
            return Forbid();
        }

        return claim.Evidence.OrderBy(e => e.UploadedAt).Select(EvidenceDto.From).ToList();
    }

    /// <summary>Returns a short-lived signed download URL (the local equivalent of a Blob SAS URL).</summary>
    [HttpGet("{id:guid}/evidence/{evidenceId:guid}/link")]
    [Authorize(Policy = Policies.AnyRole)]
    public async Task<ActionResult<EvidenceLinkDto>> EvidenceLink(Guid id, Guid evidenceId, CancellationToken ct)
    {
        var claim = await claims.GetAsync(id, ct);
        if (!await CanAsync(claim, ClaimOperations.View))
        {
            return Forbid();
        }

        var url = claims.CreateEvidenceLink(claim, evidenceId, EvidenceLinkLifetime);
        return new EvidenceLinkDto(url.ToString(), DateTime.UtcNow.Add(EvidenceLinkLifetime));
    }

    private async Task<ActionResult<ClaimDetailDto>> TransitionAsync(
        Guid id,
        ClaimAction action,
        TransitionRequest request,
        CancellationToken ct)
    {
        var claim = await claims.GetAsync(id, ct);
        if (!await CanAsync(claim, ClaimOperations.View))
        {
            return Forbid();
        }

        var actor = User.ToActor();
        await claims.TransitionAsync(claim, action, actor, request, ct);
        return ClaimDetailDto.From(claim, actor);
    }

    private async Task<bool> CanAsync(Claim claim, Microsoft.AspNetCore.Authorization.Infrastructure.OperationAuthorizationRequirement operation) =>
        (await authorization.AuthorizeAsync(User, claim, operation)).Succeeded;
}

public sealed record EvidenceLinkDto(string Url, DateTime ExpiresAt);
