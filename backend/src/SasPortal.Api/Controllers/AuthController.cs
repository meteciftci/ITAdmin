using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SasPortal.Api.Contracts.Auth;
using SasPortal.Application.Abstractions.Services;
using AppModels = SasPortal.Application.Common.Models;

namespace SasPortal.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService, ISettingsService settingsService) : ControllerBase
{
    private const string ServiceUnavailableErrorCode = "ServiceUnavailable";
    private const string LoginErrorCode = "LoginError";

    [HttpGet("session-options")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthSessionOptionsResponse>> GetSessionOptions(CancellationToken cancellationToken)
    {
        var options = await settingsService.GetAuthSessionOptionsAsync(cancellationToken);
        return Ok(new AuthSessionOptionsResponse(
            options.RememberMeEnabled,
            options.IdleTimeoutMinutes,
            options.IdleWarningSeconds,
            options.AccessTokenMinutes));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(
            new AppModels.LoginRequest(
                request.UserName,
                request.Password,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(),
                request.RememberMe),
            cancellationToken);

        var response = new LoginResponse(
            result.IsSuccess,
            result.Message,
            result.AccessToken,
            result.RefreshToken,
            result.AccessTokenExpiresAt,
            result.RefreshTokenExpiresAt,
            result.ErrorCode);

        if (result.IsSuccess)
        {
            return Ok(response);
        }

        if (string.Equals(result.ErrorCode, ServiceUnavailableErrorCode, StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }

        // Generic unexpected login error (e.g. unhandled exception in AuthService that is not a
        // database connectivity issue). This is a server-side fault, not a credential mismatch,
        // so it must not be returned as 401 — otherwise the user sees a misleading
        // "wrong username or password" message in the UI.
        if (string.Equals(result.ErrorCode, LoginErrorCode, StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, response);
        }

        return Unauthorized(response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<RefreshTokenResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.RefreshTokenAsync(
            new AppModels.RefreshTokenRequest(
                request.RefreshToken,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString()),
            cancellationToken);

        var response = new RefreshTokenResponse(
            result.IsSuccess,
            result.Message,
            result.AccessToken,
            result.RefreshToken,
            result.AccessTokenExpiresAt,
            result.RefreshTokenExpiresAt,
            result.ErrorCode);

        if (result.IsSuccess)
        {
            return Ok(response);
        }

        return Unauthorized(response);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<ActionResult<LogoutResponse>> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.LogoutAsync(
            new AppModels.LogoutRequest(
                request.RefreshToken,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString()),
            cancellationToken);

        var response = new LogoutResponse(
            result.IsSuccess,
            result.Message);

        if (result.IsSuccess)
        {
            return Ok(response);
        }

        return BadRequest(response);
    }

    private const string JwtSubClaimType = "sub";

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var rawUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(JwtSubClaimType)?.Value;

        if (!Guid.TryParse(rawUserId, out var userId))
        {
            return Unauthorized();
        }

        var result = await authService.GetCurrentUserAsync(userId, cancellationToken);

        if (!result.IsSuccess)
        {
            if (string.Equals(result.Message, "User was not found.", StringComparison.Ordinal))
            {
                return NotFound();
            }

            return Unauthorized();
        }

        if (!result.UserId.HasValue || result.UserName is null || result.DisplayName is null)
        {
            return Unauthorized();
        }

        var response = MapCurrentUserResponse(result);

        return Ok(response);
    }

    [HttpPatch("me/preferences")]
    [Authorize]
    public async Task<ActionResult<CurrentUserResponse>> UpdateCurrentUserPreferences(
        [FromBody] UpdateCurrentUserPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        var rawUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(JwtSubClaimType)?.Value;

        if (!Guid.TryParse(rawUserId, out var userId))
        {
            return Unauthorized();
        }

        var actorUserName = User.Identity?.Name
            ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? User.FindFirst("name")?.Value;

        var result = await authService.UpdateCurrentUserPreferencesAsync(
            new AppModels.UpdateCurrentUserPreferencesRequest(
                userId,
                request.PreferredLanguage,
                actorUserName),
            cancellationToken);

        if (!result.IsSuccess)
        {
            if (string.Equals(result.Message, "User was not found.", StringComparison.Ordinal))
            {
                return NotFound();
            }

            if (string.Equals(result.Message, "User is inactive.", StringComparison.Ordinal))
            {
                return BadRequest(new { message = result.Message });
            }

            return BadRequest(new { message = result.Message });
        }

        if (result.User is null || !result.User.UserId.HasValue || result.User.UserName is null || result.User.DisplayName is null)
        {
            return BadRequest(new { message = "User preferences could not be updated." });
        }

        return Ok(MapCurrentUserResponse(result.User));
    }

    private static CurrentUserResponse MapCurrentUserResponse(AppModels.CurrentUserResult result) =>
        new(
            result.UserId!.Value,
            result.UserName!,
            result.DisplayName!,
            result.Email,
            result.PreferredLanguage ?? "tr",
            result.Roles,
            result.Permissions,
            result.IsSuperAdmin);
}
