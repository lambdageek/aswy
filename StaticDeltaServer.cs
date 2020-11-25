using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            foreach (var filePath in Directory.GetFiles (basePath, "*.name")) {
                var noExt = Path.GetFileNameWithoutExtension (filePath);
                var dil = Path.Combine (basePath, noExt + ".dil");
                if (File.Exists(dil))
                    usableContent.Add (noExt);
            }
            log.LogInformation ($"Will serve: {String.Join(", ", usableContent)}");
            return Task.CompletedTask;
        }

        private static bool WriteNet32 (int n, byte[] buf, ref int pos) {
            const int lenCount = 4;
            var res = BitConverter.TryWriteBytes(new Span<byte>(buf, pos, lenCount), System.Net.IPAddress.HostToNetworkOrder(n));
            pos += lenCount;
            return res;
        }

        private static async Task<int> WriteAllFromStream (Stream f, byte[] buf, int bytesRemaining, int pos, CancellationToken ct = default) {
            while (bytesRemaining > 0) {
                int nread = await f.ReadAsync (new Memory<byte>(buf, pos, bytesRemaining), ct);
                pos += nread;
                bytesRemaining -= nread;
            }
            return pos;
        }
        private static async Task<DeltaPayload> MakePayloadFromPath (string basePath,string noExt, CancellationToken ct = default) {
            var paths = new string[] {
                Path.Combine(basePath, noExt + ".name"),
                Path.Combine(basePath, noExt + ".dmeta"),
                Path.Combine(basePath, noExt + ".dil")
            };
            // Payload is    [ totalSize | dmetaSize | dmeta | dilSize | dil ]
            // where the sizes are int32 in network order and don't count themselves
            var sizes = paths.Select((f) => (int)new FileInfo (f).Length);
            const int lenCount = 4;
            var totalSize = sizes.Sum () + sizes.Count() * lenCount;
            
            var buf = new byte[lenCount + totalSize];
            int pos = 0;

            WriteNet32 (totalSize, buf, ref pos);
            foreach ((var path, var size) in paths.Zip(sizes)) {
                WriteNet32 (size, buf, ref pos);
                using var f = new FileStream (path, FileMode.Open);
                pos = await WriteAllFromStream (f, buf, size, pos, ct);
            }
            return new DeltaPayload(buf);
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
                    payloads[curPayload++] = await MakePayloadFromPath (basePath, noExt, ct);
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