using Octockup.Server.Hubs;
using EasyExtensions.Crypto;
using Octockup.Server.Modules;
using Octockup.Server.Database;
using Octockup.Server.Extensions;
using EasyExtensions.Abstractions;
using Octockup.Server.Abstractions;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.Quartz.Extensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.EntityFrameworkCore.Extensions;
using EasyExtensions.AspNetCore.Authorization.Extensions;

namespace Octockup.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            string masterKey = builder.Configuration.GetMasterKey();
            builder.Configuration["Pepper"] = KeyDerivation.DeriveSubkeyBase64(masterKey, "pepper", 32);
            byte[] cryptoKey = KeyDerivation.DeriveSubkey(masterKey, "crypto", 32);
            string sqlitePassword = KeyDerivation.DeriveSubkeyBase64(masterKey, "sqlite", 32);
            string sqlitePath = Path.Combine(AppContext.BaseDirectory, "data", "octockup.sqlite");

            builder.Services.AddControllers();
            builder.Services
                .AddSqlite<AppDbContext>(connectionString: $"Data Source={sqlitePath};Password={sqlitePassword};")
                .AddScoped<IBackupProvider, S3BackupStorage>()
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

            var app = builder.Build();
            app.UseCors().UseDefaultFiles();
            app.MapStaticAssets();
            app.UseAuthentication()
                .UseAuthorization();
            app.MapControllers();
            app.MapFallbackToFile("/index.html");
            app.MapHub<EventHub>("/api/v1/event-hub");
            app.ApplyMigrations<AppDbContext>();
            app.Run();
        }
    }
}
