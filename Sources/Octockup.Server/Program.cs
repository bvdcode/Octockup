using Octockup.Server.Hubs;
using EasyExtensions.Crypto;
using Octockup.Server.Services;
using Octockup.Server.Extensions;
using EasyExtensions.Abstractions;
using Octockup.Server.Abstractions;
using Octockup.Server.BackupSources;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.Quartz.Extensions;
using EasyExtensions.AspNetCore.Extensions;
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

            builder.Services.AddControllers();
            builder.Services
                .AddScoped<IBackupSource, FileSystemBackupSource>()
                .AddScoped<IUserDataStorage, UserDataStorage>()
                .AddScoped<IStreamCipher>(sp => new AesGcmStreamCipher(cryptoKey))
                .AddPbkdf2PasswordHashService()
                .AddCpuUsageService()
                .AddQuartzJobs()
                .AddHttpContextAccessor()
                .AddMediatR(x => x.RegisterServicesFromAssemblyContaining<Program>())
                .AddJwt()
                .AddSignalR();

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
