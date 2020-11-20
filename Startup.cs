using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace aswy
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<DeltaListener>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseWebSockets();

            {
                var fs = new FileServerOptions();
                fs.DefaultFilesOptions.DefaultFileNames = new string[] {"index.html"};
                app.UseFileServer(fs);
            }

            app.UseRouting();

            app.UseMiddleware<WebbyTalky>();


            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/hw", async context =>
                {
                    var d = context.RequestServices.GetRequiredService<DeltaListener>();
                    d.Emote ("hw begin");
                    await context.Response.WriteAsync("Hello World!");
                    d.Emote ("hw end");
                });
            });
        }
    }
}
