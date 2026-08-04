// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;

namespace Octockup.Server.Authorization
{
    public class AdminAuthorizationHandler(AppDbContext _dbContext)
        : AuthorizationHandler<AdminRequirement>
    {
        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            AdminRequirement requirement)
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                return;
            }
            Guid userId = context.User.GetUserId();

            bool isAdmin = await _dbContext.Users
                .AsNoTracking()
                .AnyAsync(x => x.Id == userId && x.IsAdmin && !x.IsDisabled);
            if (isAdmin)
            {
                context.Succeed(requirement);
            }
        }
    }
}
