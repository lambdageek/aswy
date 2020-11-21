using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Net.WebSockets;

#nullable enable

namespace DeltaListener {

    public record DeltaPayload (byte[] Bytes);
    public class DeltaListener {
        enum InjectorState {
            Starting,
            Listening,
            InjectorConnected,
            Disconnected
        }

        enum UserAppState {
            Connecting,
            WaitingForInjector,
            Servicing,
            Closing,
            Done

        }

        InjectorState _state;
        readonly TaskCompletionSource<Stream> _injectorReady;

        readonly SemaphoreSlim _readerFinished;        

        public DeltaListener() {
            _state = InjectorState.Starting;
            _injectorReady = new TaskCompletionSource<Stream>();
            _readerFinished = new SemaphoreSlim (0, 1);
            StartListening ();
        }

        private void StartListening () {
            Task.Run (InjectorListenerLoop);
        }

        private async Task InjectorListenerLoop () {
            while (_state != InjectorState.Disconnected) {
                switch (_state) {
                    case InjectorState.Disconnected:
                        /* can't happen */
                        break;
                    case InjectorState.Starting:
                        await CreateInjectorSocket ();
                        break;
                    case InjectorState.Listening:
                        await ListenForInjectorConnection ();
                        break;
                    case InjectorState.InjectorConnected:
                        await WaitForSessionEnd ();
                        break;
                }
            }
        }

        private async Task CreateInjectorSocket()
        {
            await Task.Delay (1000);
            _state = InjectorState.Listening;
        }

        private async Task ListenForInjectorConnection ()
        {
            await Task.Delay (1000);
            _state = InjectorState.InjectorConnected;
            var incomingStream = new MemoryStream();
            _injectorReady.SetResult(incomingStream);
        }

        private async Task WaitForSessionEnd ()
        {
            await _readerFinished.WaitAsync ();
            // or could be _state = InjectorState.Listening if we want to go again
            // would need to reset _injectorReady too
            _state = InjectorState.Disconnected;
        }


        private enum WaitForInjectorOutcome {
            Ready,
            Canceled
        }
        // returns true if injector is ready or false if the wait was cancelled
        private async Task<Stream> WaitForInjector (CancellationToken ct)
        {
            var readyWhenCancelled = new TaskCompletionSource();
            ct.Register( (tcs) => {
                ((TaskCompletionSource)tcs!).TrySetResult();
            }, readyWhenCancelled);
            var injectorReadyTask = _injectorReady.Task;
            var outcome = await Task.WhenAny(injectorReadyTask.ContinueWith((_t) => WaitForInjectorOutcome.Ready),
                                             readyWhenCancelled.Task.ContinueWith((_t) => WaitForInjectorOutcome.Canceled));
            switch (outcome.Result) {
                case WaitForInjectorOutcome.Ready:
                    return injectorReadyTask.Result;
                case WaitForInjectorOutcome.Canceled:
                    ct.ThrowIfCancellationRequested ();
                    break;
            }
            throw new Exception ("should not be possible");
        }
        public void ConverseWith(WebSocket socket, TaskCompletionSource doner, CancellationToken ct = default)
        {
            Task.Run (() => ServiceClient (socket, doner, ct), ct);
        }

        private async Task ServiceClient (WebSocket socket, TaskCompletionSource doner, CancellationToken ct = default)
        {
            UserAppState st = UserAppState.Connecting;
            IAsyncEnumerable<DeltaPayload>? enumDeltas = null;
            while (st != UserAppState.Done) {
                switch (st) {
                    case UserAppState.Connecting:
                        /* send ack */
                        switch (_state) {
                            case InjectorState.Disconnected:
                                st = UserAppState.Closing;
                                break;
                            case InjectorState.Starting:
                            case InjectorState.Listening:
                                st = UserAppState.WaitingForInjector;
                                break;
                            case InjectorState.InjectorConnected:
                                st = UserAppState.Servicing;
                                break;
                        }
                        break;
                    case UserAppState.WaitingForInjector:
                        var stream = await WaitForInjector (ct);
                        enumDeltas = WebSocketFramedSender.EnumerateStreamFrames (stream, ct);
                        st = UserAppState.Servicing;
                        break;
                    case UserAppState.Closing:
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, ct);
                        doner.TrySetResult();
                        st = UserAppState.Done;
                        break;
                    case UserAppState.Servicing:
                        if (socket.State != WebSocketState.Open) {
                            /* FIXME: distinguish the various non-Open states? */
                            st = UserAppState.Closing;
                            break;
                        } else {
                            var s = new WebSocketFramedSender(socket);
                            await s.SendAllAsync (enumDeltas!, ct);
                            st = UserAppState.Closing;
                            _readerFinished.Release ();
                            break;
                        }
                }
            }
        }

   
    }


    class WebSocketFramedSender {
        readonly WebSocket socket;
        public WebSocketFramedSender (WebSocket socket) {
            this.socket = socket;
        }

        public async Task SendAllAsync (IAsyncEnumerable<DeltaPayload> input, CancellationToken ct = default)
        {
            await foreach (var buf in input.WithCancellation(ct)) {
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
        public static async IAsyncEnumerable<DeltaPayload> EnumerateStreamFrames (Stream stream, [EnumeratorCancellation] CancellationToken ct = default)
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