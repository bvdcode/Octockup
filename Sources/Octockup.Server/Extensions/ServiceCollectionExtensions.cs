using EasyExtensions.AspNetCore.Extensions;

namespace Octockup.Server.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection SetupCors(this IServiceCollection services)
        {
            string? corsOrigins = Environment.GetEnvironmentVariable("CORS_ORIGINS");
            if (string.IsNullOrWhiteSpace(corsOrigins))
            {
                services.AddCors(x =>
                {
                    x.AddDefaultPolicy(y =>
                    {
                        y.AllowAnyOrigin();
                        y.AllowAnyMethod();
                        y.AllowAnyHeader();
                        y.WithExposedHeaders("*");
                    });
                });
                return services;
            }
            return services.AddDefaultCorsWithOrigins(corsOrigins);
        }
    }
}
