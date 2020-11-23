using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;


namespace DeltaForwarder {
    public class HotReloadInjectorMiddleware {
        const string HRI_REQUEST_PATH = "/hri";
        private readonly RequestDelegate next;
        private readonly DeltaForwarder listener;
        public HotReloadInjectorMiddleware (RequestDelegate next, DeltaForwarder listener) {
            this.next = next;
            this.listener = listener;
        }

        public async Task Invoke (HttpContext context) {
            if (context.Request.Path == HRI_REQUEST_PATH) {
                if (context.WebSockets.IsWebSocketRequest) {
                    Console.WriteLine("websocket go brr");
                    using var websocket = await context.WebSockets.AcceptWebSocketAsync();
                    /* N.B. important to await here to keep the websocket alive */
                    await listener.StartSession(websocket, context.RequestAborted);
                } else {
                    Console.WriteLine ("you're not a websocket!");
                    context.Response.StatusCode = 418;
                }
            } else {
                Console.WriteLine ("middleware bypass begin");
                // middleware go here
                await next (context);
                Console.WriteLine ("middleware bypass end");
            }
        }
    }
}