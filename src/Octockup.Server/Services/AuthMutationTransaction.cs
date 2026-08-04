// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Octockup.Server.Database;
using System.Data;

namespace Octockup.Server.Services
{
    public static class AuthMutationTransaction
    {
        public static async Task<TResult> ExecuteAsync<TResult>(
            AppDbContext dbContext,
            Func<Task<TResult>> operation,
            CancellationToken cancellationToken)
        {
            if (dbContext.Database.CurrentTransaction is not null)
            {
                return await operation();
            }

            await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            try
            {
                TResult result = await operation();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                throw CreateConflictException();
            }
        }

        private static bool IsSerializationFailure(Exception exception)
        {
            for (Exception? current = exception; current is not null; current = current.InnerException)
            {
                if (current is PostgresException postgres
                    && postgres.SqlState == PostgresErrorCodes.SerializationFailure)
                {
                    return true;
                }
            }

            return false;
        }

        private static AuthApiException CreateConflictException()
        {
            return new AuthApiException(
                StatusCodes.Status409Conflict,
                "Authentication state changed concurrently. Retry the operation.");
        }
    }
}
