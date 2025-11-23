using System.Text;
using Octockup.Server.Hubs;
using EasyExtensions.Crypto;
using Octockup.Server.Extensions;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.Quartz.Extensions;
using EasyExtensions.Crypto.Abstractions;
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

            builder.Services.AddControllers();
            builder.Services
                .AddScoped<IStreamCipher>(sp => new AesGcmStreamCipher(SHA256.HashData(Encoding.UTF8.GetBytes(masterKey))))
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
            app.UseExceptionHandler();
            app.MapHub<EventHub>("/api/v1/event-hub");
            app.Run();
        }
    }
}
