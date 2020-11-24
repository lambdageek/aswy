using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


namespace DeltaForwarder {
    public class HotReloadInjectorMiddleware {
        const string HRI_REQUEST_PATH = "/hri";
        private readonly RequestDelegate next;
        
        public HotReloadInjectorMiddleware (RequestDelegate next) {
            this.next = next;
        }

        public async Task Invoke (HttpContext context, DeltaForwarder listener, ILogger<HotReloadInjectorMiddleware> log) {
            if (context.Request.Path == HRI_REQUEST_PATH) {
                if (context.WebSockets.IsWebSocketRequest) {
                    log.LogInformation("websocket go brr");
                    using var websocket = await context.WebSockets.AcceptWebSocketAsync();
                    /* N.B. important to await here to keep the websocket alive */
                    await listener.StartSession(websocket, context.RequestAborted);
                } else {
                    log.LogWarning ("you're not a websocket!");
                    context.Response.StatusCode = 418;
                }
            } else {
                await next (context);
            }
        }
    }
}