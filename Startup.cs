using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DeltaForwarder.Extensions;

namespace aswy
{
    public class Startup
    {
        // This method gets caller by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHotReloadInjector();
                    
        }

        // This method gets caller by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseWebSockets();

            {
                var ctp =  new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider ();
                const string octetStream = "application/octet-stream";
                /* need these ones for Mono wasm */
                ctp.Mappings[".dll"] = octetStream;
                ctp.Mappings[".blat"] = octetStream;
                ctp.Mappings[".dat"] = octetStream;
                /* FIXME: these three are not needed but the sample bundles some */
                ctp.Mappings[".dmeta"] = octetStream;
                ctp.Mappings[".dil"] = octetStream;
                ctp.Mappings[".dpdb"] = octetStream;
                var fs = new FileServerOptions();
                fs.DefaultFilesOptions.DefaultFileNames = new string[] {"index.html"};
                fs.EnableDirectoryBrowsing = true;
                fs.StaticFileOptions.ContentTypeProvider = ctp;
                app.UseFileServer(fs);
            }

            app.UseRouting();

            app.UseHotReloadInjector();

            // app.UseEndpoints(endpoints =>
            // {
            //     endpoints.MapGet("/hw", async context =>
            //     {
            //         await context.Response.WriteAsync("Hello World!");
            //     });
            // });
        }
    }
}
