// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Octockup.Server.Models.Dto;
using Octockup.Server.Services;

namespace Octockup.Server.Handlers.Administration
{
    public class GetAuthenticationSettingsQueryHandler(AuthenticationSettingsService _settings)
        : IRequestHandler<GetAuthenticationSettingsQuery, AuthenticationSettingsDto>
    {
        public async Task<AuthenticationSettingsDto> Handle(
            GetAuthenticationSettingsQuery request,
            CancellationToken cancellationToken)
        {
            return new AuthenticationSettingsDto
            {
                PasswordLoginEnabled = await _settings.IsPasswordLoginEnabledAsync(cancellationToken),
            };
        }
    }
}
