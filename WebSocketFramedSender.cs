using System;
using System.IO;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;


namespace DeltaForwarder {
    class WebSocketFramedSender {
        readonly WebSocket socket;
        readonly ILogger _log;
        public WebSocketFramedSender (WebSocket socket, ILogger log) {
            this.socket = socket;
            _log = log;
        }

        public async Task SendAllAsync (IAsyncEnumerable<DeltaPayload> input, CancellationToken ct = default)
        {
            _log.LogTrace("sending payload to websocket");
            await foreach (var buf in input.WithCancellation(ct)) {
                _log.LogTrace($"forwarding payload of size {buf.Bytes.Length}");
                if (ct.IsCancellationRequested)
                    break;
                if (socket.State != WebSocketState.Open)
                    break;
                await socket.SendAsync (buf.Bytes, WebSocketMessageType.Binary, true, ct);
            }
        }

       
        private static int NetworkGetLength (ReadOnlySpan<byte> netBuf) {
            return System.Net.IPAddress.NetworkToHostOrder (BitConverter.ToInt32(netBuf));
        }
        public static async IAsyncEnumerable<DeltaPayload> EnumerateStreamFrames (Stream stream, ILogger log, [EnumeratorCancellation] CancellationToken ct = default)
        {
            byte[] szBuf = new byte[4];
            while (true) {
                int nread;
                int writeOffset = 0;
                int remaining = 4;
                while (remaining > 0) {
                    nread = await stream.ReadAsync(szBuf.AsMemory(writeOffset, remaining), ct);
                    if (nread == 0 || ct.IsCancellationRequested)
                        yield break;
                    writeOffset += nread;
                    remaining -= nread;
                }
                remaining = NetworkGetLength(szBuf.AsSpan());
                log.LogTrace ($"got payload of size {remaining}");
                byte[] result = new byte[4 + remaining];
                szBuf.CopyTo(result, 0);
                writeOffset = 4;
                while (remaining > 0) {
                    nread = await stream.ReadAsync (result.AsMemory(writeOffset, remaining), ct);
                    if (nread == 0 || ct.IsCancellationRequested)
                        yield break;
                    writeOffset += nread;
                    remaining -= nread;
                }
                yield return new DeltaPayload(result);
            }
        }
    }
}