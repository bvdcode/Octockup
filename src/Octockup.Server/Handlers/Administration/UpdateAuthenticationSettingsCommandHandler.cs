// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Octockup.Server.Models.Dto;
using Octockup.Server.Services;

namespace Octockup.Server.Handlers.Administration
{
    public class UpdateAuthenticationSettingsCommandHandler(AuthenticationSettingsService _settings)
        : IRequestHandler<UpdateAuthenticationSettingsCommand, AuthenticationSettingsDto>
    {
        public async Task<AuthenticationSettingsDto> Handle(
            UpdateAuthenticationSettingsCommand request,
            CancellationToken cancellationToken)
        {
            await _settings.SetPasswordLoginEnabledAsync(
                request.PasswordLoginEnabled,
                cancellationToken);
            return new AuthenticationSettingsDto
            {
                PasswordLoginEnabled = request.PasswordLoginEnabled,
            };
        }
    }
}
