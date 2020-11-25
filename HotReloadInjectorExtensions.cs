using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;

namespace DeltaForwarder.Extensions {
    public static class HotReloadInjectorExtensions {
        public static IServiceCollection AddHotReloadInjector (this IServiceCollection services) {
            return services//.AddSingleton<IDeltaStreamServer, NoneDeltaStreamServer>()
                           .AddSingleton<IDeltaStreamServer, StaticDeltaStreamServer>()
                           .AddScoped<DeltaForwarder>();
        }

        public static IApplicationBuilder UseHotReloadInjector (this IApplicationBuilder app) {
            return app.UseMiddleware<HotReloadInjectorMiddleware>();
        }
    }
}