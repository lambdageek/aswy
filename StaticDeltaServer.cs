using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DeltaForwarder {
    public class StaticDeltaStreamServer : DeltaStreamServer
    {
        const string BASE_DIR = "static_sample";

        readonly ILogger log;
        readonly string basePath;

        readonly List<string> usableContent;

        bool served;
        public StaticDeltaStreamServer (ILogger<StaticDeltaStreamServer> log) {
            basePath = Path.Combine (Directory.GetCurrentDirectory(), BASE_DIR);
            usableContent = new List<string>();
            this.log = log;
            served = false;
        }
        protected override Task OnCreateIncomingConnectionListener (CancellationToken ct = default) {
            foreach (var filePath in Directory.GetFiles (basePath, "*.dmeta")) {
                var noExt = Path.GetFileNameWithoutExtension (filePath);
                var dil = Path.Combine (basePath, noExt + ".dil");
                if (File.Exists(dil))
                    usableContent.Add (noExt);
            }
            log.LogInformation ($"Will serve: {String.Join(", ", usableContent)}");
            return Task.CompletedTask;
        }
        private protected override Task<DeltaBackendSession> OnListenForInjectorConnection (CancellationToken ct = default) {
            if (served)
                return new TaskCompletionSource<DeltaBackendSession>().Task;
            served = true;
            // Would this be easier if we used a DeltaBackendSession subclass instead of a Stream?
            return Task.Run (async () => {
                var payloads = new DeltaPayload[usableContent.Count];
                int curPayload = 0;
                foreach (var noExt in usableContent) {
                    ct.ThrowIfCancellationRequested();
                    var dmetaPath = Path.Combine(basePath, noExt + ".dmeta");
                    using var f = new FileStream (dmetaPath, FileMode.Open);
                    int fileSz = (int)f.Length; // could be longer
                    const int lenCount = 4;
                    var buf = new byte[fileSz + lenCount];
                    BitConverter.TryWriteBytes(new Span<byte>(buf, 0, lenCount), System.Net.IPAddress.HostToNetworkOrder(fileSz));
                    int bytesRemaining = fileSz;
                    int pos = lenCount;
                    while (bytesRemaining > 0) {
                        int nread = await f.ReadAsync (new Memory<byte>(buf, pos, bytesRemaining), ct);
                        pos += nread;
                        bytesRemaining -= nread;
                    }
                    payloads[curPayload++] = new DeltaPayload(buf);
                };
                return new FixedPayloadsDeltaBackendSession (payloads, log) as DeltaBackendSession;
            }, ct);
        }

        internal class FixedPayloadsDeltaBackendSession : DeltaBackendSession {
            readonly DeltaPayload[] payloads;
            internal FixedPayloadsDeltaBackendSession (DeltaPayload[] payloads, ILogger log) : base (log) {
                this.payloads = payloads;
            }

            protected override void OnDisconnectServer () {}

            protected override IDeltaSource OnGetDeltaSource(Action readerDone)
            {
                return new FixedPayloadsDeltaSource (payloads, readerDone, _log);
            }

        }

        internal class FixedPayloadsDeltaSource : DeltaSource {
            readonly IReadOnlyCollection<DeltaPayload> payloads;
            internal FixedPayloadsDeltaSource (IReadOnlyCollection<DeltaPayload> payloads,  Action finished, ILogger log) : base (finished, log)
            {
                this.payloads = payloads;
            }

            public override async IAsyncEnumerable<DeltaPayload> GetPayloads ([EnumeratorCancellation] CancellationToken ct = default) {
                foreach (var p in payloads) {
                    if (ct.IsCancellationRequested)
                        yield break;
                    await Task.Delay (0, CancellationToken.None);
                    yield return p;
                }
            }
        }
    }
}