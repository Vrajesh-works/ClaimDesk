using ClaimDesk.Application.Abstractions;
using ClaimDesk.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimDesk.Api.Controllers;

[ApiController]
[Route("api/evidence")]
public sealed class EvidenceDownloadController(SignedLinkProtector protector, IEvidenceStore store) : ControllerBase
{
    /// <summary>
    /// Redeems a signed, expiring token. No bearer token is needed, exactly like a SAS URL,
    /// so the browser can open the file directly.
    /// </summary>
    [HttpGet("download")]
    [AllowAnonymous]
    public async Task<IActionResult> Download([FromQuery] string? token, CancellationToken ct)
    {
        var payload = protector.Unprotect(token);
        if (payload is null)
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Link is invalid or has expired");
        }

        var stream = await store.OpenReadAsync(payload.Key, ct);
        if (stream is null)
        {
            return NotFound();
        }

        return File(stream, payload.ContentType, payload.FileName);
    }
}
