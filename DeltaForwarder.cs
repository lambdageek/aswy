using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Net.WebSockets;
using Microsoft.Extensions.Logging;

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
        readonly ILogger _log;

        public DeltaForwarder(IDeltaStreamServer server, ILogger<DeltaForwarder> log) {
            _server = server;
            _log = log;
        }

        public Task StartSession(WebSocket socket, CancellationToken ct = default)
        {
            TaskCompletionSource doner = new TaskCompletionSource();
            Task.Run (() => ServiceClient (socket, doner, ct), ct);
            return doner.Task;
        }

        private async Task ServiceClient (WebSocket socket, TaskCompletionSource doner, CancellationToken ct = default)
        {
            Task<IDeltaBackendSession> backendSession = _server.GetDefaultSession(ct);
            IDeltaSource? source = null;
            ForwarderState st = ForwarderState.Connecting;
            while (st != ForwarderState.Done) {
                switch (st) {
                    case ForwarderState.Connecting:
                        _log.LogTrace("connecting");
                        /* send ack */
                        switch (_server.PeekState) {
                            case DeltaServerState.Disconnected:
                                st = ForwarderState.Closing;
                                break;
                            case DeltaServerState.NotReady:
                            case DeltaServerState.Connected:
                                st = ForwarderState.WaitingForServer;
                                break;
                            }
                        break;
                    case ForwarderState.WaitingForServer:
                        _log.LogTrace("waiting for server session");
                        try {
                            source = await (await backendSession).GetDeltaSource(ct);
                            st = ForwarderState.Servicing;
                        } catch (DeltaBackendSessionClosedException) {
                            _log.LogInformation ("websocket connecting to a closed backend");
                            st = ForwarderState.Closing;
                        }
                        break;
                    case ForwarderState.Closing:
                        _log.LogTrace("closing");
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, ct);
                        doner.TrySetResult();
                        st = ForwarderState.Done;
                        break;
                    case ForwarderState.Servicing: {
                        using var _ = _log.BeginScope("servicing");
                        if (socket.State != WebSocketState.Open) {
                            /* FIXME: distinguish the various non-Open states? */
                            st = ForwarderState.Closing;
                            break;
                        } else {
                            _log.LogTrace ("servicing is sending");
                            var s = new WebSocketFramedSender(socket, _log);
                            await s.SendAllAsync(source!.GetPayloads(ct), ct);
                            st = ForwarderState.Closing;
                            await source.ClientDone();
                            break;
                        }
                    }
                }
            }
        }   
    }

}