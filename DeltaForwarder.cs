using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Net.WebSockets;

#nullable enable

namespace DeltaForwarder {

    public record DeltaPayload (byte[] Bytes);
    public class DeltaForwarder {
        enum ForwarderState {
            Connecting,
            WaitingForServer,
            Servicing,
            Closing,
            Done

        }

        readonly IDeltaStreamServer _server;

        public DeltaForwarder(IDeltaStreamServer server) {
            _server = server;
        }

        public Task StartSession(WebSocket socket, CancellationToken ct = default)
        {
            TaskCompletionSource doner = new TaskCompletionSource();
            Task.Run (() => ServiceClient (socket, doner, ct), ct);
            return doner.Task;
        }

        private async Task ServiceClient (WebSocket socket, TaskCompletionSource doner, CancellationToken ct = default)
        {
            ForwarderState st = ForwarderState.Connecting;
            IDeltaSource? source = null;
            while (st != ForwarderState.Done) {
                switch (st) {
                    case ForwarderState.Connecting:
                        /* send ack */
                        switch (_server.PeekState) {
                            case DeltaServerState.Disconnected:
                                st = ForwarderState.Closing;
                                break;
                            case DeltaServerState.NotReady:
                                st = ForwarderState.WaitingForServer;
                                break;
                            case DeltaServerState.Connected:
                                Task<IDeltaSource> t = _server.GetDeltaSource(CancellationToken.None);
                                if (!t.IsCompleted)
                                    st = ForwarderState.WaitingForServer;
                                else {
                                    source = t.Result;
                                    st = ForwarderState.Servicing;
                                }
                                break;
                        }
                        break;
                    case ForwarderState.WaitingForServer:
                        source = await _server.GetDeltaSource(ct);
                        st = ForwarderState.Servicing;
                        break;
                    case ForwarderState.Closing:
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, ct);
                        doner.TrySetResult();
                        st = ForwarderState.Done;
                        break;
                    case ForwarderState.Servicing:
                        if (socket.State != WebSocketState.Open) {
                            /* FIXME: distinguish the various non-Open states? */
                            st = ForwarderState.Closing;
                            break;
                        } else {
                            var s = new WebSocketFramedSender(socket);
                            await s.SendAllAsync(source!.GetPayloads(), ct);
                            st = ForwarderState.Closing;
                            await source.ClientDone();
                            break;
                        }
                }
            }
        }   
    }

}