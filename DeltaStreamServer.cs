using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DeltaForwarder
{
    public abstract class DeltaStreamServer : IDeltaStreamServer {
        

        enum InjectorState {
            Starting,
            Listening,
            Connected,
            Disconnected
        }
        private InjectorState _state;
        readonly TaskCompletionSource<Stream> _injectorReady;

        readonly SemaphoreSlim _readerFinished;        

        protected DeltaStreamServer () {
            _state = InjectorState.Starting;
            _injectorReady = new TaskCompletionSource<Stream>();
            _readerFinished = new SemaphoreSlim (0, 1);
            StartListening ();

        }


        public DeltaServerState PeekState => _state switch {
            InjectorState.Starting => DeltaServerState.NotReady,
            InjectorState.Listening => DeltaServerState.NotReady,
            InjectorState.Connected => DeltaServerState.Connected,
            InjectorState.Disconnected => DeltaServerState.Disconnected,
            _ => throw new Exception ($"unexpected injector state {_state}")
        };
        private void StartListening (CancellationToken ct = default) {
            Task.Run (() => InjectorListenerLoop (ct));
        }

        private async Task InjectorListenerLoop (CancellationToken ct = default) {
            while (_state != InjectorState.Disconnected) {
                if (ct.IsCancellationRequested)
                    _state = InjectorState.Disconnected;
                switch (_state) {
                    case InjectorState.Disconnected:
                        break;
                    case InjectorState.Starting:
                        await CreateIncomingConnectionListener (ct);
                        break;
                    case InjectorState.Listening:
                        await ListenForInjectorConnection (ct);
                        break;
                    case InjectorState.Connected:
                        /* FIXME: So there problem here is that browser tabs exist.
                         * So it's not enough to just have a single session with a single client.
                         * We need to allow for multiple sessions to arise.
                         * So at the very least this should be WaitForAllSessionsToEnd(),
                         * and also before we get to this state and during it, we should be
                         * able to deal with additional calls to GetDeltaSource
                         */ 
                        await WaitForSessionEnd (ct);
                        break;
                }
            }
        }

        protected abstract Task OnCreateIncomingConnectionListener (CancellationToken ct = default);
        protected abstract Task<Stream> OnListenForInjectorConnection (CancellationToken ct = default);

        private async Task CreateIncomingConnectionListener (CancellationToken ct = default)
        {
            await OnCreateIncomingConnectionListener (ct);
            _state = InjectorState.Listening;
        }

        protected async Task ListenForInjectorConnection (CancellationToken ct = default)
        {
            var stream = await OnListenForInjectorConnection(ct);
            _state = InjectorState.Connected;
            _injectorReady.SetResult(stream);
        }

        private async Task WaitForSessionEnd (CancellationToken ct = default)
        {
            await _readerFinished.WaitAsync (ct);
            // or could stay in the connected state until the underlying injector connection closes
            // in case new clients connect.
            _state = InjectorState.Disconnected;
        }

        class Source : IDeltaSource {
            readonly Stream stream;
            readonly SemaphoreSlim finished;

            internal Source (Stream stream, SemaphoreSlim finished) {
                this.stream = stream;
                this.finished = finished;
            }

            public IAsyncEnumerable<DeltaPayload> GetPayloads(CancellationToken ct = default)
            {
                return WebSocketFramedSender.EnumerateStreamFrames (stream, ct);
            }

            public Task ClientDone () {
                finished.Release ();
                return Task.CompletedTask;
            }
        }

        private enum WaitForInjectorOutcome {
            Ready,
            Canceled
        }

        public async Task<IDeltaSource> GetDeltaSource (CancellationToken ct = default)
        {
            var readyWhenCancelled = new TaskCompletionSource();
            if (ct != CancellationToken.None)
                ct.Register( (tcs) => {
                    ((TaskCompletionSource)tcs!).TrySetResult();
                }, readyWhenCancelled);
            var injectorReadyTask = _injectorReady.Task;
            var outcome = await Task.WhenAny(injectorReadyTask.ContinueWith((_t) => WaitForInjectorOutcome.Ready),
                                             readyWhenCancelled.Task.ContinueWith((_t) => WaitForInjectorOutcome.Canceled));
            switch (outcome.Result) {
                case WaitForInjectorOutcome.Ready:
                    // FIXME: this isn't quite right if there's more than one client
                    // - we end up giving the same stream to every client.
                    // If the connection is just for reading, they end up taking consecutive payloads
                    // If the connection is also for writing (client might want to say where it's starting from, for example)
                    // they end up talking over each other.
                    return new Source (injectorReadyTask.Result, _readerFinished);
                case WaitForInjectorOutcome.Canceled:
                    ct.ThrowIfCancellationRequested ();
                    break;
            }
            throw new Exception ("should not be possible");

        }


    }

    public class NoneDeltaStreamServer : DeltaStreamServer {
        public NoneDeltaStreamServer () {

        }

        protected override Task OnCreateIncomingConnectionListener(CancellationToken ct = default)
        {
            return Task.Delay (1000, ct);
        }

        protected override async Task<Stream> OnListenForInjectorConnection (CancellationToken ct = default)
        {
            await Task.Delay (1000, ct);
            return new MemoryStream();
        }


    }
}