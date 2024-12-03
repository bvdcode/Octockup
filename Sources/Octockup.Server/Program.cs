using Octockup.Server.Database;
using Octockup.Server.Extensions;
using Microsoft.EntityFrameworkCore;
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
                .AddDbContext<AppDbContext>(options => options.UseSqlite("Data Source=octockup.db"))
                .SetupJwtKey(builder.Configuration)
                .AddJwt(builder.Configuration)
                .AddOpenApi()
                .SetupCors();

            var app = builder.Build();
            app.UseCors()
                .UseDefaultFiles();
            app.MapStaticAssets();
            app.MapOpenApi();
            app.UseHttpsRedirection()
                .UseAuthorization()
                .UseAuthentication();
            app.MapControllers();
            app.MapFallbackToFile("/index.html");
            app.Run();
        }
    }
}
