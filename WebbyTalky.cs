using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;


public class WebbyTalky {
    private readonly RequestDelegate next;
    private readonly DeltaListener listener;
    public WebbyTalky (RequestDelegate next, DeltaListener listener) {
        this.next = next;
        this.listener = listener;
    }

    public async Task Invoke (HttpContext context) {
        if (context.Request.Path == "/ws") {
            if (context.WebSockets.IsWebSocketRequest) {
                listener.Emote("websocket go brr");
                using (var websocket = await context.WebSockets.AcceptWebSocketAsync()) {
                    var tcs = new TaskCompletionSource();
                    listener.ConverseWith(websocket, tcs);
                    await tcs.Task;
                }
            } else {
                listener.Emote ("you're not a websocket!");
                context.Response.StatusCode = 418;
            }
        } else {
            listener.Emote ("middleware bypass begin");
            // middleware go here
            await next (context);
            listener.Emote ("middleware bypass end");
        }
    }
}