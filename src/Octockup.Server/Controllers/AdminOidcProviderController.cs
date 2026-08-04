// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Extensions;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Requests;
using Octockup.Server.Services;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Route("/api/v1/admin/authentication/oidc-providers")]
    public class AdminOidcProviderController(OidcProviderService _providers) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<OidcProviderDto>>> ListAsync(
            CancellationToken cancellationToken)
        {
            IReadOnlyList<OidcProviderDto> providers = await _providers.ListAdminAsync(cancellationToken);
            return Ok(providers);
        }

        [HttpPost]
        public async Task<ActionResult<OidcProviderDto>> CreateAsync(
            [FromBody] OidcProviderRequest request,
            CancellationToken cancellationToken)
        {
            OidcProviderDto provider = await _providers.CreateAsync(request, cancellationToken);
            return Ok(provider);
        }

        [HttpPut("{providerId:guid}")]
        public async Task<ActionResult<OidcProviderDto>> UpdateAsync(
            [FromRoute] Guid providerId,
            [FromBody] OidcProviderRequest request,
            CancellationToken cancellationToken)
        {
            OidcProviderDto provider = await _providers.UpdateAsync(
                providerId,
                request,
                cancellationToken);
            return Ok(provider);
        }

        [HttpDelete("{providerId:guid}")]
        public async Task<IActionResult> DeleteAsync(
            [FromRoute] Guid providerId,
            CancellationToken cancellationToken)
        {
            await _providers.DeleteAsync(providerId, cancellationToken);
            return NoContent();
        }
    }
}
