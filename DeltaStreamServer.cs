using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DeltaForwarder
{

    internal class DeltaBackendSessionClosedException : Exception {
        public DeltaBackendSessionClosedException() : base () {}
        public DeltaBackendSessionClosedException(string message) : base (message) {}

        public DeltaBackendSessionClosedException(string message, Exception innerException) : base (message, innerException) {}
    }

    /// Represents a single backend connection that is feeding us changes.
    internal class DeltaBackendSession : IDeltaBackendSession {

        struct ReaderCount {
            bool started;
            int n;
            object _lock;
            internal ReaderCount (int n) {
                this.n = n;
                this.started = false;
                _lock = new object();
            }

            public bool TryIncr () {
                lock (_lock) {
                    if (n == 0 && started)
                        return false;
                    else { 
                        started = true;
                        ++n;
                        return true;
                    }
                }
            }

            public bool Decr () {
                lock (_lock) {
                    if (n == 0)
                        return false;
                    return (--n == 0);
                }
            }
        }

        readonly ReaderCount _readers;
        readonly TaskCompletionSource _readersFinished;
        readonly Stream _serverStream;
        readonly CancellationTokenSource _serverClosed;
        readonly ILogger _log;       
        internal DeltaBackendSession (Stream serverStream, ILogger log) {
            _readers = new ReaderCount(0);
            _readersFinished = new TaskCompletionSource();
            _serverStream = serverStream;
            _log = log;
            _serverClosed = new CancellationTokenSource ();
        }

        private async Task WaitForSessionEnd (CancellationToken ct = default)
        {
            // FIXME: is this what we want? or do we want to wait until the backend is closed?
            await _readersFinished.Task;
        }


        internal void Start () {
            var _t = Task.Run (() => ServiceSession ());
        }

        internal async Task ServiceSession () {
            await WaitForSessionEnd (_serverClosed.Token); // FIXME: cancel when the stream closes            
        }

        internal void DisconnectServer ()
        {
            _serverStream.Close();
            _serverClosed.Cancel();
        }
        internal Task NotifySecondaryAndClose () {
            return Task.Run (() => DisconnectServer());
        }

        private void ReaderDone () {
            if (_readers.Decr())
                _readersFinished.SetResult();
        }
        public Task<IDeltaSource> GetDeltaSource(CancellationToken ct = default)
        {
            if (!_readers.TryIncr())
                return Task.FromException<IDeltaSource>(new DeltaBackendSessionClosedException());
            var src = new DeltaStreamServer.Source (_serverStream, ReaderDone, _log);
            return Task.FromResult(src as IDeltaSource);
        }
    }

    /// Subclasses are responsible for creating a DeltaBackendSession,
    /// for example by listening on a socket. (The accepted connection will then be managed by)
    /// the DeltaBackendSession
    public abstract class DeltaStreamServer : IDeltaStreamServer {

        enum InjectorState {
            Starting,
            Listening,
            Disconnected
        }
        private InjectorState _state;
        readonly TaskCompletionSource<DeltaBackendSession> _defaultSessionReady;
        readonly ILogger _log;
         

        protected DeltaStreamServer (ILogger<DeltaStreamServer> log) {
            _state = InjectorState.Starting;
            _defaultSessionReady = new TaskCompletionSource<DeltaBackendSession>();
            _log = log;

            StartListening ();

        }

        public DeltaServerState PeekState => _state switch {
            InjectorState.Starting => DeltaServerState.NotReady,
            InjectorState.Listening => DeltaServerState.Connected,
            InjectorState.Disconnected => DeltaServerState.Disconnected,
            _ => throw new Exception ($"unexpected injector state {_state}")
        };
        private void StartListening (CancellationToken ct = default) {
            Task.Run (() => InjectorListenerLoop (ct), ct);
        }

        private async Task InjectorListenerLoop (CancellationToken ct = default) {
            _state = InjectorState.Starting;
            await CreateIncomingConnectionListener (ct);
            while (_state != InjectorState.Disconnected) {
                if (ct.IsCancellationRequested)
                    _state = InjectorState.Disconnected;
                await ListenForInjectorConnection (ct);
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
            try {
                var stream = await OnListenForInjectorConnection(ct);
                var defaultSession = new DeltaBackendSession (stream, _log);
                if (!_defaultSessionReady.Task.IsCompleted) {
                    _defaultSessionReady.SetResult(defaultSession);
                    defaultSession.Start ();
                } else {
                    // TODO: save the new session
                    var secondarySession = new DeltaBackendSession (stream, _log);
                    var _t = secondarySession.NotifySecondaryAndClose();
                }
            } catch (TaskCanceledException) {
                // just return and let the InjectorListenerLoop handle the cancellation
            }
        }
        
        internal class Source : IDeltaSource {
            readonly Stream stream;
            readonly Action finished;
            readonly ILogger _log;

            internal Source (Stream stream, Action finished, ILogger log) {
                this.stream = stream;
                this.finished = finished;
                this._log = log;
            }

            public IAsyncEnumerable<DeltaPayload> GetPayloads(CancellationToken ct = default)
            {
                return WebSocketFramedSender.EnumerateStreamFrames (stream, _log, ct);
            }

            public Task ClientDone () {
                finished ();
                return Task.CompletedTask;
            }
        }

        private enum WaitForInjectorOutcome {
            Ready,
            Canceled
        }

        public async Task<IDeltaBackendSession> GetDefaultSession (CancellationToken ct = default)
        {
            var readyWhenCancelled = new TaskCompletionSource();
            if (ct != CancellationToken.None)
                ct.Register( (tcs) => {
                    ((TaskCompletionSource)tcs!).TrySetResult();
                }, readyWhenCancelled);
            var injectorReadyTask = _defaultSessionReady.Task;
            var outcome = await Task.WhenAny(injectorReadyTask.ContinueWith((_t) => WaitForInjectorOutcome.Ready),
                                             readyWhenCancelled.Task.ContinueWith((_t) => WaitForInjectorOutcome.Canceled));
            switch (outcome.Result) {
                case WaitForInjectorOutcome.Ready:
                    // FIXME: this isn't quite right if there's more than one client
                    // - we end up giving the same stream to every client.
                    // If the connection is just for reading, they end up taking consecutive payloads
                    // If the connection is also for writing (client might want to say where it's starting from, for example)
                    // they end up talking over each other.
                    return injectorReadyTask.Result;
                case WaitForInjectorOutcome.Canceled:
                    ct.ThrowIfCancellationRequested ();
                    break;
            }
            throw new Exception ("should not be possible");

        }


    }

    public class NoneDeltaStreamServer : DeltaStreamServer {
        readonly ILogger _log;
        bool created;
        public NoneDeltaStreamServer (ILogger<NoneDeltaStreamServer> log) : base (log) {
            _log = log;
            created = false;
        }

        protected override Task OnCreateIncomingConnectionListener(CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        protected override Task<Stream> OnListenForInjectorConnection (CancellationToken ct = default)
        {
            if (created) {
                var tcs = new TaskCompletionSource<Stream>();
                return tcs.Task; // return a Task that never completes
            }

            var s = "Hello World!";
            var enc = System.Text.Encoding.UTF8;
            const int lenCount = 4;
            var count = enc.GetByteCount(s);
            var buf = new byte[lenCount + count];
            BitConverter.TryWriteBytes(new Span<byte>(buf, 0, lenCount), System.Net.IPAddress.HostToNetworkOrder(count));
            enc.GetBytes(s, new Span<byte>(buf, lenCount, buf.Length - lenCount));
            _log.LogTrace("creating memory stream");
            var t =  Task.FromResult<Stream>(new MemoryStream(buf));
            created = true;
            return t;
        }


    }
}