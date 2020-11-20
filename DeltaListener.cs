using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
public class DeltaListener {
    public DeltaListener() {
        Console.WriteLine ("constructed");
    }

    public void Emote (string x) {
        Console.WriteLine(x);
    }

    public async Task ConnectAsync(string connectSpec) {
        
        await Task.Delay(0);    

    }

    public void ConverseWith(WebSocket socket, TaskCompletionSource doner)
    {
        Task.Run (async () => {
            for (int i = 0; i < 10; ++i) {
                Console.WriteLine($"doing sockety work {i}");
                await Task.Delay(5);
            }
            var bytes = Encoding.UTF8.GetBytes ("Hi there");           
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            doner.SetResult();
        });
    }
}