using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;


namespace DeltaListener {
    public class HotReloadInjectorMiddleware {
        private readonly RequestDelegate next;
        private readonly DeltaListener listener;
        public HotReloadInjectorMiddleware (RequestDelegate next, DeltaListener listener) {
            this.next = next;
            this.listener = listener;
        }

        public async Task Invoke (HttpContext context) {
            if (context.Request.Path == "/hri") {
                if (context.WebSockets.IsWebSocketRequest) {
                    Console.WriteLine("websocket go brr");
                    using var websocket = await context.WebSockets.AcceptWebSocketAsync();
                    var tcs = new TaskCompletionSource();
                    listener.ConverseWith(websocket, tcs);
                    await tcs.Task;
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