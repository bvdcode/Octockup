// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Extensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Crypto;
using EasyExtensions.Quartz.Extensions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Abstractions;
using Octockup.Server.Extensions;
using Octockup.Server.Hubs;
using Octockup.Server.Modules;
using Octockup.Server.Services;

namespace Octockup.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            string masterKey = builder.Configuration.GetMasterKey();
            builder.Configuration.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("Pepper", KeyDerivation.DeriveSubkeyBase64(masterKey, "pepper", 32))
            ]);
            byte[] cryptoKey = KeyDerivation.DeriveSubkey(masterKey, "crypto", 32);

            builder.Services.AddControllers();
            builder.Services
                .AddScoped<IBackupProvider, IMAPSource>()
                .AddScoped<IBackupProvider, S3BackupStorage>()
                .AddScoped<IBackupProvider, SFTPBackupStorage>()
                .AddScoped<IBackupProvider, FileSystemBackupSource>()
                .AddScoped<IStreamCipher>(sp => new AesGcmStreamCipher(cryptoKey))
                .AddPbkdf2PasswordHashService()
                .AddCpuUsageService()
                .AddQuartzJobs()
                .AddHttpContextAccessor()
                .AddJwt()
                .AddSignalR();

            string[] corsOrigins = builder.Configuration.GetSection("CorsOrigins").Get<string[]>() ?? [];
            builder.Services.AddDefaultCorsWithOrigins(corsOrigins);
            var logger = LoggerFactory.Create(loggingBuilder => loggingBuilder.AddConsole()).CreateLogger<Program>();
            SetupDatabaseService setupDb = new(logger, builder.Configuration, builder.Services);

            var app = builder.Build();
            app.UseCors().UseDefaultFiles();
            app.MapStaticAssets();
            app.UseAuthentication()
                .UseAuthorization();
            app.MapControllers();
            app.MapFallbackToFile("/index.html");
            app.MapHub<EventHub>("/api/v1/event-hub");
            app.Run();
        }
    }
}
