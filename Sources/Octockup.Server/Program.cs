using FluentValidation;
using Octockup.Server.Hubs;
using Octockup.Server.Database;
using Octockup.Server.Services;
using Octockup.Server.Extensions;
using FluentValidation.AspNetCore;
using Octockup.Server.Controllers;
using EasyExtensions.Quartz.Extensions;
using Octockup.Server.Providers.Storage;
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
            builder.Services.AddControllers();
            builder.Services
                .AddScoped<ProgressTracker>()
                .AddSingleton<JobCancellationService>()
                .AddQuartzJobs()
                .AddStorageProviders()
                .AddHttpContextAccessor()
                .AddValidatorsFromAssemblyContaining<Program>()
                .AddFluentValidationAutoValidation()
                .AddExceptionHandler()
                .AddAutoMapper(typeof(Program))
                .AddMediatR(x => x.RegisterServicesFromAssemblyContaining<Program>())
                .AddHostedService<InitializeDatabaseService>()
                .AddDbContext<AppDbContext>(builder.Configuration)
                .SetupJwtKey(builder.Configuration)
                .AddJwt(builder.Configuration)
                .AddOpenApi()
                .SetupCors(builder.Configuration)
                .AddSignalR();

            var app = builder.Build();
            app.UseCors().UseDefaultFiles();
            app.MapStaticAssets();
            app.MapOpenApi();
            app.UseAuthentication()
                .UseAuthorization();
            app.MapControllers();
            app.MapFallbackToFile("/index.html");
            app.ApplyMigrations<AppDbContext>();
            app.UseExceptionHandler();
            app.MapHub<BackupHub>(Routes.Version + "/backup/hub");
            app.Run();
        }
    }
}
