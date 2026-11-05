using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace OrbyLauncher.Services;
internal sealed class StaWorker : IDisposable
{
    private readonly BlockingCollection<Action> queue = new();
    private int disposed;
    internal StaWorker(string name)
    {
        var thread = new Thread(() =>
        {
            try { foreach (var action in queue.GetConsumingEnumerable()) action(); }
            finally { queue.Dispose(); }
        }) { IsBackground = true, Name = name };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
    }
    internal Task<T> Run<T>(Func<T> action, CancellationToken cancellation = default)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (Volatile.Read(ref disposed) != 0) { completion.SetCanceled(); return completion.Task; }
        try { queue.Add(() =>
        {
            if (Volatile.Read(ref disposed) != 0) { completion.TrySetCanceled(); return; }
            if (cancellation.IsCancellationRequested) { completion.TrySetCanceled(cancellation); return; }
            try { completion.TrySetResult(action()); }
            catch (OperationCanceledException error) { completion.TrySetCanceled(error.CancellationToken); }
            catch (Exception error) { completion.TrySetException(error); }
        }); }
        catch (InvalidOperationException) { completion.TrySetCanceled(); }
        return completion.Task;
    }
    public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) queue.CompleteAdding(); }
}
