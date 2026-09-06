namespace Weft.Tests;

/// <summary>Conformance controls, independent of the production task implementation.</summary>
public sealed class ControlledSchedule
{
    private readonly object sync = new();
    private readonly Dictionary<string, TaskCompletionSource> gates = new(StringComparer.Ordinal);
    private readonly List<string> events = [];
    private readonly SortedDictionary<long, List<TaskCompletionSource>> timers = [];
    public long Ticks { get; private set; }

    public Task Wait(string gate, CancellationToken cancellation = default)
    {
        Task task;
        lock (sync)
        {
            if (!gates.TryGetValue(gate, out var source)) gates.Add(gate, source = new(TaskCreationOptions.RunContinuationsAsynchronously));
            task = source.Task;
        }
        return task.WaitAsync(cancellation);
    }
    public void Release(string gate)
    {
        lock (sync)
        {
            if (!gates.TryGetValue(gate, out var source)) gates.Add(gate, source = new(TaskCreationOptions.RunContinuationsAsynchronously));
            if (!source.TrySetResult()) throw new InvalidOperationException($"Gate '{gate}' already released.");
        }
    }
    public Task Delay(long ticks, CancellationToken cancellation = default)
    {
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
        lock (sync)
        {
            if (ticks == 0) return Task.CompletedTask;
            var due = checked(Ticks + ticks);
            var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!timers.TryGetValue(due, out var pending)) timers.Add(due, pending = []);
            pending.Add(source);
            return source.Task.WaitAsync(cancellation);
        }
    }
    public void Advance(long ticks)
    {
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
        lock (sync)
        {
            Ticks = checked(Ticks + ticks);
            foreach (var due in timers.Keys.Where(due => due <= Ticks).ToArray())
            {
                foreach (var timer in timers[due]) timer.TrySetResult();
                timers.Remove(due);
            }
        }
    }
    public void Record(string name) { lock (sync) events.Add(name); }
    public string[] Trace { get { lock (sync) return events.ToArray(); } }
    public void AssertBefore(string before, string after)
    {
        var trace = Trace;
        var left = Array.IndexOf(trace, before);
        var right = Array.IndexOf(trace, after);
        if (left < 0 || right < 0 || left >= right) throw new InvalidOperationException($"Expected '{before}' before '{after}'; trace: {string.Join(", ", trace)}");
    }
}
