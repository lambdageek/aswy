using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DeltaForwarder
{
    public enum DeltaServerState {
            /* Server is not ready, IDeltaStreamServer.GetDeltaSource may return a task that isn't completed */
            NotReady,
            /* Server is ready, IDeltaStreamServer.GetDeltaSource will return a completed Task */
            Connected,
            /* Server will not return any more delta sources */
            Disconnected
    }

    public interface IDeltaStreamServer {

        DeltaServerState PeekState {get; }
        Task<IDeltaSource> GetDeltaSource (CancellationToken ct = default);

    }

    public interface IDeltaSource {
        IAsyncEnumerable<DeltaPayload> GetPayloads (CancellationToken ct = default);
        /* Client should call this to tell the server that it is done with this source.
         * FIXME: should we just make the source IAsyncDisposable?
         */
        Task ClientDone();
    }

}