using System.ComponentModel.DataAnnotations;
using ClaimDesk.Api.Auth;
using ClaimDesk.Api.Infrastructure;
using ClaimDesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClaimDesk.Api.Controllers;

public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(128)]
    public string Password { get; set; } = string.Empty;
}

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAt, UserInfo User);

public sealed record UserInfo(Guid Id, string Email, string DisplayName, string Role);

[ApiController]
[Route("api/auth")]
public sealed class AuthController(UserAuthenticator authenticator, TokenIssuer tokens) : ControllerBase
{
    /// <summary>Demo login that issues a local JWT. Replaced by Entra ID sign-in in Azure.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await authenticator.AuthenticateAsync(request.Email, request.Password, ct);
        if (user is null)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password");
        }

        var token = tokens.Issue(user);
        return new LoginResponse(token.AccessToken, token.ExpiresAt, new UserInfo(user.Id, user.Email, user.DisplayName, user.Role));
    }

    [HttpGet("me")]
    [Authorize(Policy = Policies.AnyRole)]
    public ActionResult<UserInfo> Me() => new UserInfo(
        User.GetUserId(),
        User.FindFirst("email")?.Value ?? string.Empty,
        User.FindFirst("name")?.Value ?? string.Empty,
        User.ToActor().Role);
}
