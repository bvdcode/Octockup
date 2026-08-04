// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using EasyExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Requests;
using Octockup.Server.Services;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Route("/api/v1/auth")]
    public class OidcController(
        OidcProviderService _providers,
        OidcAuthenticationService _authentication,
        AuthenticationSettingsService _authenticationSettings,
        ILogger<OidcController> _logger) : ControllerBase
    {
        [AllowAnonymous]
        [HttpGet("options")]
        public async Task<ActionResult<AuthOptionsDto>> GetOptionsAsync(
            CancellationToken cancellationToken)
        {
            bool passwordLoginEnabled = await _authenticationSettings.IsPasswordLoginEnabledAsync(
                cancellationToken);
            IReadOnlyList<PublicOidcProviderDto> providers = await _providers.ListPublicAsync(
                cancellationToken);
            return Ok(new AuthOptionsDto
            {
                PasswordLoginEnabled = passwordLoginEnabled,
                OidcProviders = providers,
            });
        }

        [AllowAnonymous]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [HttpPost("oidc/{providerSlug}/authorization-url")]
        public async Task<ActionResult<OidcAuthorizationUrlDto>> CreateAuthorizationUrlAsync(
            [FromRoute] string providerSlug,
            [FromBody] OidcAuthorizationRequest? request,
            CancellationToken cancellationToken)
        {
            string? returnUrl = request?.ReturnUrl;
            bool linkAccount = request?.LinkAccount ?? false;
            string authorizationUrl;
            if (linkAccount)
            {
                if (User.Identity?.IsAuthenticated != true)
                {
                    return Unauthorized();
                }

                authorizationUrl = await _authentication.BeginLinkAsync(
                    User.GetUserId(),
                    providerSlug,
                    returnUrl,
                    Response,
                    cancellationToken);
            }
            else
            {
                authorizationUrl = await _authentication.BeginSignInAsync(
                    providerSlug,
                    returnUrl,
                    Response,
                    cancellationToken);
            }

            return Ok(new OidcAuthorizationUrlDto
            {
                AuthorizationUrl = authorizationUrl,
            });
        }

        [AllowAnonymous]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [HttpGet("oidc/callback")]
        public async Task<IActionResult> CallbackAsync(
            [FromQuery] string? state,
            [FromQuery] string? code,
            [FromQuery] string? error,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    string errorReturnUrl = await _authentication.CancelCallbackAsync(
                        state,
                        Request,
                        Response,
                        cancellationToken);
                    return Redirect(errorReturnUrl);
                }
                if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(code))
                {
                    return Redirect("/login?oidc=error");
                }

                string returnUrl = await _authentication.CompleteCallbackAsync(
                    state.Trim(),
                    code.Trim(),
                    Request,
                    Response,
                    cancellationToken);
                return Redirect(returnUrl);
            }
            catch (OidcCallbackException exception)
            {
                _logger.LogWarning(exception.InnerException, "OIDC callback failed");
                return Redirect(exception.ReturnUrl);
            }
            catch (AuthApiException exception)
            {
                _logger.LogWarning(exception, "OIDC callback failed");
                return Redirect("/login?oidc=error");
            }
        }

        [Authorize]
        [HttpGet("external-identities")]
        public async Task<ActionResult<IReadOnlyList<UserExternalIdentityDto>>> GetExternalIdentitiesAsync(
            CancellationToken cancellationToken)
        {
            IReadOnlyList<UserExternalIdentityDto> identities = await _authentication.ListLinkedAsync(
                User.GetUserId(),
                cancellationToken);
            return Ok(identities);
        }

        [Authorize]
        [HttpDelete("external-identities/{identityId:guid}")]
        public async Task<IActionResult> DeleteExternalIdentityAsync(
            [FromRoute] Guid identityId,
            CancellationToken cancellationToken)
        {
            await _authentication.UnlinkAsync(
                User.GetUserId(),
                identityId,
                cancellationToken);
            return NoContent();
        }
    }
}
