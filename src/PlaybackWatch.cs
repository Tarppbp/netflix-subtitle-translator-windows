using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;

internal sealed class PlaybackWatch : IDisposable
{
    private readonly BlockingCollection<Action> jobs = new BlockingCollection<Action>();
    private readonly Action changed;
    private readonly StructureChangedEventHandler structureChanged;
    private readonly AutomationPropertyChangedEventHandler nameChanged;
    private AutomationElement document;
    private IntPtr window;
    private bool watchStructure, watchName;
    private volatile bool disposed;
    private int lastSignal = unchecked(Environment.TickCount - 25);
    private int signals;
    public int Signals { get { return Volatile.Read(ref signals); } }

    public PlaybackWatch(Action changed)
    {
        this.changed = changed;
        structureChanged = delegate { Signal(); };
        nameChanged = delegate { Signal(); };
        var worker = new Thread(Run) { IsBackground = true, Name = "Netflix UI Automation" };
        worker.SetApartmentState(ApartmentState.MTA);
        worker.Start();
    }

    private void Run()
    {
        foreach (Action job in jobs.GetConsumingEnumerable()) job();
        Unsubscribe();
        jobs.Dispose();
    }

    public Task<NetflixReader.Sample> ReadAsync()
    {
        var result = new TaskCompletionSource<NetflixReader.Sample>(TaskCreationOptions.RunContinuationsAsynchronously);
        jobs.Add(delegate
        {
            try
            {
                var sample = NetflixReader.Read(window, document);
                result.SetResult(sample);
                UpdateSubscription(sample != null && sample.Window == NetflixReader.GetForegroundWindow() ? sample : null);
            }
            catch (Exception error) { result.TrySetException(error); }
        });
        return result.Task;
    }

    public void Pause() { jobs.Add(Unsubscribe); }

    private void UpdateSubscription(NetflixReader.Sample next)
    {
        if (Equals(document, next == null ? null : next.Document)) return;
        Unsubscribe();
        if (next == null) return;
        document = next.Document;
        window = next.Window;
        WithProvider(delegate
        {
            Automation.AddStructureChangedEventHandler(document, TreeScope.Subtree, structureChanged);
            watchStructure = true;
        });
        WithProvider(delegate
        {
            Automation.AddAutomationPropertyChangedEventHandler(document, TreeScope.Descendants,
                nameChanged, AutomationElement.NameProperty);
            watchName = true;
        });
    }

    private void Unsubscribe()
    {
        if (document == null) return;
        if (watchStructure)
            WithProvider(() => Automation.RemoveStructureChangedEventHandler(document, structureChanged));
        if (watchName)
            WithProvider(() => Automation.RemoveAutomationPropertyChangedEventHandler(document, nameChanged));
        document = null;
        window = IntPtr.Zero;
        watchStructure = watchName = false;
    }

    private static void WithProvider(Action action)
    {
        try { action(); }
        catch (ElementNotAvailableException) {}
        catch (COMException) {}
        catch (InvalidOperationException) {}
    }

    private void Signal()
    {
        int before = Volatile.Read(ref lastSignal);
        int now = Environment.TickCount;
        if (!disposed && unchecked(now - before) >= 20 &&
            Interlocked.CompareExchange(ref lastSignal, now, before) == before)
        {
            Interlocked.Increment(ref signals);
            changed();
        }
    }

    public void Dispose()
    {
        disposed = true;
        jobs.CompleteAdding();
    }
}
