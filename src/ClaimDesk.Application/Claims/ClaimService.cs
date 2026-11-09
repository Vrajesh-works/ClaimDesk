using System.Security.Cryptography;
using ClaimDesk.Application.Abstractions;
using ClaimDesk.Application.Risk;
using ClaimDesk.Application.Uploads;
using ClaimDesk.Domain.Claims;
using ClaimDesk.Domain.Common;
using ClaimDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ClaimDesk.Application.Claims;

public sealed class ClaimService(
    IClaimDeskDbContext db,
    IRiskScorer riskScorer,
    IEvidenceStore evidenceStore,
    TimeProvider time)
{
    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task<Claim> FileAsync(Actor actor, FileClaimRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = Now;
        var claim = Claim.File(
            actor.UserId,
            NewClaimNumber(now),
            request.PolicyNumber,
            DateTime.SpecifyKind(request.IncidentDate, DateTimeKind.Utc),
            request.ClaimedAmount,
            request.Description,
            request.ToVehicle(),
            now);

        var risk = riskScorer.Assess(claim.Vehicle, claim.ClaimedAmount);
        claim.ApplyRiskAssessment(risk.Score, risk.Reasons, risk.Flagged, now);

        db.Claims.Add(claim);
        await db.SaveChangesAsync(ct);

        ClaimDeskMetrics.ClaimsFiled.Add(1);
        if (risk.Flagged)
        {
            ClaimDeskMetrics.ClaimsFlagged.Add(1);
        }

        return claim;
    }

    public async Task<Claim> GetAsync(Guid id, CancellationToken ct) =>
        await db.Claims.Include(c => c.Evidence).FirstOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new NotFoundException($"Claim {id} was not found.");

    /// <summary>Claimants see their own claims; adjusters and admins see all.</summary>
    public async Task<IReadOnlyList<ClaimSummaryDto>> ListAsync(Actor actor, ClaimStatus? status, CancellationToken ct)
    {
        var query = db.Claims.AsNoTracking();
        if (actor.Role == Roles.Claimant)
        {
            query = query.Where(c => c.ClaimantId == actor.UserId);
        }

        if (status is not null)
        {
            query = query.Where(c => c.Status == status);
        }

        var claims = await query.OrderByDescending(c => c.SubmittedAt).Take(500).ToListAsync(ct);
        return claims.Select(ClaimSummaryDto.From).ToList();
    }

    /// <summary>Open claims for adjusters, highest risk first, then oldest first.</summary>
    public async Task<IReadOnlyList<ClaimSummaryDto>> QueueAsync(CancellationToken ct)
    {
        var claims = await db.Claims.AsNoTracking()
            .Where(c => c.Status == ClaimStatus.Submitted
                        || c.Status == ClaimStatus.InReview
                        || c.Status == ClaimStatus.MoreInfoNeeded)
            .OrderByDescending(c => c.RiskScore)
            .ThenBy(c => c.SubmittedAt)
            .Take(500)
            .ToListAsync(ct);
        return claims.Select(ClaimSummaryDto.From).ToList();
    }

    public async Task<IReadOnlyList<ClaimEventDto>> HistoryAsync(Guid claimId, CancellationToken ct)
    {
        var events = await db.ClaimEvents.AsNoTracking()
            .Where(e => e.ClaimId == claimId)
            .OrderBy(e => e.Id)
            .ToListAsync(ct);
        return events.Select(ClaimEventDto.From).ToList();
    }

    public async Task<Claim> TransitionAsync(
        Claim claim,
        ClaimAction action,
        Actor actor,
        TransitionRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(request);
        claim.Apply(action, actor, request.Note, Now, request.ApprovedAmount);
        await db.SaveChangesAsync(ct);
        ClaimDeskMetrics.Transitions.Add(1, new KeyValuePair<string, object?>("action", action.ToString()));
        return claim;
    }

    public async Task<Evidence> AddEvidenceAsync(
        Claim claim,
        Actor actor,
        string fileName,
        Stream content,
        long length,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(content);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        if (bytes.LongLength != length)
        {
            length = bytes.LongLength;
        }

        var safeName = EvidencePolicy.SanitizeFileName(fileName);
        var check = EvidencePolicy.Check(safeName, length, bytes.AsSpan(0, Math.Min(bytes.Length, 16)));
        if (!check.Ok)
        {
            throw new DomainValidationException(check.Error!);
        }

        var evidence = new Evidence
        {
            FileName = safeName,
            ContentType = check.ContentType!,
            SizeBytes = length,
            StorageKey = $"{claim.Id:N}/{Guid.NewGuid():N}{Path.GetExtension(safeName).ToLowerInvariant()}",
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
        };

        claim.AddEvidence(evidence, actor, Now);

        // Evidence has a client-generated key, so EF would treat it as an existing row if it
        // were only discovered through the navigation. Mark it as new explicitly.
        db.Evidence.Add(evidence);

        buffer.Position = 0;
        await evidenceStore.SaveAsync(evidence.StorageKey, buffer, evidence.ContentType, ct);
        await db.SaveChangesAsync(ct);
        ClaimDeskMetrics.EvidenceUploaded.Add(1);
        return evidence;
    }

    public Uri CreateEvidenceLink(Claim claim, Guid evidenceId, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(claim);
        var evidence = claim.Evidence.FirstOrDefault(e => e.Id == evidenceId)
            ?? throw new NotFoundException($"Evidence {evidenceId} was not found on claim {claim.Number}.");
        return evidenceStore.CreateReadUrl(evidence.StorageKey, evidence.FileName, evidence.ContentType, lifetime);
    }

    private static string NewClaimNumber(DateTime now) =>
        $"CLM-{now:yyyyMMdd}-{RandomNumberGenerator.GetHexString(6)}";
}
