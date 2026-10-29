using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ThinkWind.Services;
internal sealed class StaWorker : IDisposable
{
    private readonly BlockingCollection<Action> queue = new();
    private bool disposed;
    internal StaWorker(string name)
    {
        var thread = new Thread(() => { foreach (var action in queue.GetConsumingEnumerable()) action(); }) { IsBackground = true, Name = name };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
    }
    internal Task<T> Run<T>(Func<T> action, CancellationToken cancellation = default)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (disposed) { completion.SetCanceled(); return completion.Task; }
        try { queue.Add(() => { if (cancellation.IsCancellationRequested) { completion.TrySetCanceled(cancellation); return; } try { completion.TrySetResult(action()); } catch (Exception error) { completion.TrySetException(error); } }); }
        catch (InvalidOperationException) { completion.TrySetCanceled(); }
        return completion.Task;
    }
    public void Dispose() { disposed = true; queue.CompleteAdding(); }
}
