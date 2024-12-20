using Octockup.Server.Database;
using Octockup.Server.Services;
using Octockup.Server.Extensions;
using FluentValidation.AspNetCore;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.EntityFrameworkCore.Extensions;
using EasyExtensions.AspNetCore.Authorization.Extensions;
using Octockup.Server.Providers.Storage;

namespace Octockup.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddControllers();
            builder.Services
                .AddStorageProvider<FtpProvider>()
                .AddHttpContextAccessor()
                .AddFluentValidationAutoValidation()
                .AddExceptionHandler()
                .AddAutoMapper(typeof(Program))
                .AddMediatR(x => x.RegisterServicesFromAssemblyContaining<Program>())
                .AddHostedService<InitializeDatabaseService>()
                .AddDbContext<AppDbContext>(builder.Configuration)
                .SetupJwtKey(builder.Configuration)
                .AddJwt(builder.Configuration)
                .AddOpenApi()
                .SetupCors();

            var app = builder.Build();
            app.UseCors().UseDefaultFiles();
            app.MapStaticAssets();
            app.MapOpenApi();
            app.UseHttpsRedirection()
                .UseAuthentication()
                .UseAuthorization();
            app.MapControllers();
            app.MapFallbackToFile("/index.html");
            app.ApplyMigrations<AppDbContext>();
            app.UseExceptionHandler();
            app.Run();
        }
    }
}
