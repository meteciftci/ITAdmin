using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SasPortal.Api.Contracts.Auth;
using SasPortal.Application.Abstractions.Services;
using AppModels = SasPortal.Application.Common.Models;

namespace SasPortal.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
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
                Request.Headers.UserAgent.ToString()),
            cancellationToken);

        var response = new LoginResponse(
            result.IsSuccess,
            result.Message,
            result.AccessToken,
            result.RefreshToken,
            result.AccessTokenExpiresAt,
            result.RefreshTokenExpiresAt);

        if (result.IsSuccess)
        {
            return Ok(response);
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
            result.RefreshTokenExpiresAt);

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
}
