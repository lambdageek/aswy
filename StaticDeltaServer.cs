using System;
using System.Collections.Generic;
using System.IO;
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
        public StaticDeltaStreamServer (ILogger<StaticDeltaStreamServer> log) : base (log) {
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
        protected override Task<Stream> OnListenForInjectorConnection (CancellationToken ct = default) {
            if (served)
                return (new TaskCompletionSource<Stream>()).Task;
            served = true;
            // Would this be easier if we used a DeltaBackendSession subclass instead of a Stream?
            return Task.Run (async () => {
                var stream = new MemoryStream(1024);
                foreach (var noExt in usableContent) {
                    ct.ThrowIfCancellationRequested();
                    var dmetaPath = Path.Combine(basePath, noExt + ".dmeta");
                    using var f = new FileStream (dmetaPath, FileMode.Open);
                    int fileSz = (int)f.Length; // could be longer
                    const int lenCount = 4;
                    var szBuf = new byte[lenCount];
                    BitConverter.TryWriteBytes(new Span<byte>(szBuf, 0, lenCount), System.Net.IPAddress.HostToNetworkOrder(fileSz));
                    await stream.WriteAsync (szBuf, ct);
                    await f.CopyToAsync (stream, ct);
                };
                stream.Seek (0, SeekOrigin.Begin);
                return stream as Stream;
            }, ct);
        }
    }
}