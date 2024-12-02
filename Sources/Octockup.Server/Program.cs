using Octockup.Server.Extensions;

namespace Octockup.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddControllers();
            builder.Services
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
