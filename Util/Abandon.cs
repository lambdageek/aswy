using System;
using System.Threading;
using System.Threading.Tasks;

namespace DeltaForwarder.Util {
    public class Abandon {

        enum AbandonResult {
            Ready,
            Canceled
        }
        /// Wait for the given task to finish or for the cancellation token to be canceled,
        /// whichever happens first.
        public static async Task Await (Task task, CancellationToken ct)
        {
            if (ct == CancellationToken.None) {
                await task;
                return;
            }
            var readyWhenCancelled = new TaskCompletionSource();
            ct.Register( (tcs) => {
                ((TaskCompletionSource)tcs!).TrySetResult();
            }, readyWhenCancelled);
            var outcome = await Task.WhenAny(task.ContinueWith((_t) => AbandonResult.Ready),
                                             readyWhenCancelled.Task.ContinueWith((_t) => AbandonResult.Canceled));
            switch (outcome.Result) {
                case AbandonResult.Ready:
                    return;
                case AbandonResult.Canceled:
                    ct.ThrowIfCancellationRequested ();
                    break;
                default:
                    throw new Exception ("should not be possible");
            }
        }

        /// Wait for the result of the given task or for the cancellation token to be canceled,
        /// whichever happens first.
        public static async Task<TResult> Await<TResult> (Task<TResult> task, CancellationToken ct)
        {
            if (ct == CancellationToken.None) {
                return await task;
            }
            var readyWhenCancelled = new TaskCompletionSource();
            ct.Register( (tcs) => {
                ((TaskCompletionSource)tcs!).TrySetResult();
            }, readyWhenCancelled);
            var outcome = await Task.WhenAny(task.ContinueWith((_t) => AbandonResult.Ready),
                                             readyWhenCancelled.Task.ContinueWith((_t) => AbandonResult.Canceled));
            switch (outcome.Result) {
                case AbandonResult.Ready:
                    return task.Result;
                case AbandonResult.Canceled:
                    ct.ThrowIfCancellationRequested ();
                    goto default;
                default:
                    throw new Exception ("should not be possible");
            }
        }
    }
}