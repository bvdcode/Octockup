// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;

namespace Octockup.Server.Authorization
{
    public class ActiveUserAuthorizationHandler(AppDbContext _dbContext)
        : AuthorizationHandler<ActiveUserRequirement>
    {
        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            ActiveUserRequirement requirement)
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                return;
            }
            Guid userId = context.User.GetUserId();

            bool isActive = await _dbContext.Users
                .AsNoTracking()
                .AnyAsync(x => x.Id == userId && !x.IsDisabled);
            if (isActive)
            {
                context.Succeed(requirement);
            }
        }
    }
}
