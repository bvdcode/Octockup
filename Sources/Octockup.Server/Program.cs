using FluentValidation;
using Octockup.Server.Hubs;
using Microsoft.Data.Sqlite;
using Octockup.Server.Database;
using Octockup.Server.Extensions;
using FluentValidation.AspNetCore;
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

            SqliteConnectionStringBuilder sqliteConnectionStringBuilder = new()
            {
                Pooling = true,
                Password = masterKey,
                DataSource = "/app/data/octockup.sqlite",
                Cache = SqliteCacheMode.Shared,
                Mode = SqliteOpenMode.ReadWriteCreate,
            };

            builder.Services.AddControllers();
            builder.Services
                .AddPbkdf2PasswordHashService()
                .AddCpuUsageService()
                .AddQuartzJobs()
                .AddHttpContextAccessor()
                .AddValidatorsFromAssemblyContaining<Program>()
                .AddFluentValidationAutoValidation()
                .AddExceptionHandler()
                .AddMediatR(x => x.RegisterServicesFromAssemblyContaining<Program>())
                .AddSqlite<AppDbContext>(sqliteConnectionStringBuilder.ConnectionString)
                .AddJwt()
                .AddOpenApi()
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
            app.ApplyMigrations<AppDbContext>();
            app.Run();
        }
    }
}
