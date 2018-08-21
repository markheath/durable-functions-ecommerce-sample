using Microsoft.Azure.WebJobs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DurableECommerceWorkflow
{
    public static class DurableOrchestrationContextExtensions
    {
        public static Task<T> WaitForExternalEvent<T>(this DurableOrchestrationContext ctx, string name, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<T>();
            var cts = new CancellationTokenSource();

            var timeoutAt = ctx.CurrentUtcDateTime + timeout;
            var timeoutTask = ctx.CreateTimer(timeoutAt, cts.Token);
            var waitForEventTask = ctx.WaitForExternalEvent<T>(name);

            waitForEventTask.ContinueWith(t =>
            {
                using (cts)
                {
                    if (t.Exception != null)
                    {
                        tcs.TrySetException(t.Exception);
                    }
                    else
                    {
                        tcs.TrySetResult(t.Result);
                    }
                    cts.Cancel();
                }
            }, TaskContinuationOptions.ExecuteSynchronously);

            timeoutTask.ContinueWith(t =>
            {
                using (cts)
                {
                    //tcs.TrySetCanceled();
                    tcs.TrySetResult(default(T));
                }
            }, TaskContinuationOptions.ExecuteSynchronously);

            return tcs.Task;
        }
    }
}
