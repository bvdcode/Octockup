using Microsoft.Extensions.FileProviders;

namespace Link2Stream.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddControllers();
            builder.Services.AddOpenApi();
            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
