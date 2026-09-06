namespace Weft.Tests;

public sealed class ScheduleTests
{
    [Fact]
    public async Task Gates_and_logical_time_control_order_without_wall_clock_sleeps()
    {
        var schedule = new ControlledSchedule();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var worker = Worker();
        await schedule.Wait("started", timeout.Token);
        Assert.False(worker.IsCompleted);
        schedule.Advance(9);
        Assert.False(worker.IsCompleted);
        schedule.Advance(1);
        await worker.WaitAsync(timeout.Token);
        schedule.Record("disposed");
        schedule.AssertBefore("started", "completed");
        schedule.AssertBefore("completed", "disposed");
        Assert.Throws<InvalidOperationException>(() => schedule.AssertBefore("disposed", "completed"));

        async Task Worker()
        {
            schedule.Record("started");
            var delay = schedule.Delay(10, timeout.Token);
            schedule.Release("started");
            await delay;
            schedule.Record("completed");
        }
    }

    [Fact]
    public async Task Canceling_one_waiter_does_not_cancel_shared_gate()
    {
        var schedule = new ControlledSchedule();
        using var cancellation = new CancellationTokenSource();
        var first = schedule.Wait("work", cancellation.Token);
        var second = schedule.Wait("work");
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.False(second.IsCompleted);
        schedule.Release("work");
        await second;
    }
}
