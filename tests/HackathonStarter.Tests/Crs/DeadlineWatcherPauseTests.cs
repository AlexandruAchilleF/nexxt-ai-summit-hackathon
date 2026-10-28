using System.Reflection;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Deadlines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HackathonStarter.Tests.Crs;

/// <summary>
/// Settings → "Reset demo data" holds DeadlineWatcher off through <see cref="DemoMaintenance"/> (review of PR #43): the
/// reset's own clock change must not tick between its deletes and its seed, and the end of the pause ticks once, at the
/// simulated now, so the deadlines catch up as after a new start.
/// </summary>
public class DeadlineWatcherPauseTests
{
    private const string Skipped = "paused for a demo reset";
    private static readonly DateTime Base = R2WorkflowHost.Base;

    // ---- DemoMaintenance ----

    [Fact]
    public void Pauses_nest_and_only_the_end_of_the_last_one_resumes()
    {
        var maintenance = new DemoMaintenance();
        var resumed = 0;
        maintenance.Resumed += () => resumed++;
        Assert.False(maintenance.IsPaused);

        var outer = maintenance.Pause();
        var inner = maintenance.Pause();
        inner.Dispose();
        inner.Dispose(); // the same handle twice: it must not end the outer pause
        Assert.True(maintenance.IsPaused);
        Assert.Equal(0, resumed);

        outer.Dispose();
        Assert.False(maintenance.IsPaused);
        Assert.Equal(1, resumed);
        outer.Dispose();
        Assert.Equal(1, resumed);

        using (maintenance.Pause()) Assert.True(maintenance.IsPaused); // it pauses again after a resume
        Assert.False(maintenance.IsPaused);
        Assert.Equal(2, resumed);
    }

    [Fact]
    public async Task Pauses_from_many_threads_balance_out()
    {
        var maintenance = new DemoMaintenance();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 5_000; i++)
            {
                using var pause = maintenance.Pause();
                Assert.True(maintenance.IsPaused);
            }
        })));

        Assert.False(maintenance.IsPaused);
        using (maintenance.Pause()) Assert.True(maintenance.IsPaused); // the count is back at zero, not below it
        Assert.False(maintenance.IsPaused);
    }

    // ---- DeadlineWatcher ----

    [Fact]
    public async Task While_paused_a_clock_change_does_not_tick()
    {
        await using var w = await Watched.StartAsync();

        using (w.Maintenance.Pause())
        {
            w.Clock.Advance(TimeSpan.FromDays(85)); // as the reset's clock.Reset(): it wakes the watcher at once
            await w.Log.WaitForAsync(Skipped);
            Assert.Single(w.Ticks.Times); // the tick at start only

            Assert.Null(await w.Watcher.RunOnceAsync()); // a pass asked for directly is skipped too
            Assert.Single(w.Ticks.Times);
        }

        var skipped = w.Log.Lines.Where(l => l.Message.Contains(Skipped, StringComparison.Ordinal)).ToList();
        Assert.Equal(2, skipped.Count);
        Assert.All(skipped, l => Assert.Equal(LogLevel.Debug, l.Level));
    }

    [Fact]
    public async Task The_end_of_the_pause_ticks_once_at_the_simulated_now()
    {
        await using var w = await Watched.StartAsync();

        using (w.Maintenance.Pause())
        {
            w.Clock.Advance(TimeSpan.FromDays(85));
            await w.Log.WaitForAsync(Skipped); // the clock's wake-up is spent while paused, as during a reset
        }

        await w.Ticks.WaitForAsync(2);
        Assert.Equal(Base.AddDays(85), w.Ticks.Times[1]); // caught up at once, not 30 seconds later
        await Task.Delay(TimeSpan.FromMilliseconds(250));
        Assert.Equal(2, w.Ticks.Times.Count); // one tick: the next one is 30 seconds away
    }

    [Fact]
    public async Task Nested_pauses_tick_only_after_the_last_one_ends()
    {
        await using var w = await Watched.StartAsync();

        var outer = w.Maintenance.Pause();
        var inner = w.Maintenance.Pause();
        w.Clock.Advance(TimeSpan.FromDays(1));
        await w.Log.WaitForAsync(Skipped, count: 1);

        inner.Dispose();
        w.Clock.Advance(TimeSpan.FromDays(1)); // the outer pause still holds: skipped again, no tick
        await w.Log.WaitForAsync(Skipped, count: 2);
        Assert.Single(w.Ticks.Times);

        outer.Dispose();
        await w.Ticks.WaitForAsync(2);
        Assert.Equal(Base.AddDays(2), w.Ticks.Times[1]);
    }

    [Fact]
    public async Task The_hosted_watcher_obeys_the_DemoMaintenance_of_AddDeadlines()
    {
        var (cases, ticks) = TickRecorder.Create();
        var log = new RecordingLogger();
        await using var host = await R2WorkflowHost.CreateAsync(s =>
        {
            s.AddScoped<ICaseService>(_ => cases);
            s.AddSingleton<ILogger<DeadlineWatcher>>(log);
        });
        var maintenance = host.Root.GetRequiredService<DemoMaintenance>();
        Assert.Same(maintenance, host.Root.GetRequiredService<DemoMaintenance>()); // one for the app: the reset pauses this one
        var watcher = Assert.Single(host.Root.GetServices<IHostedService>().OfType<DeadlineWatcher>());

        await watcher.StartAsync(CancellationToken.None);
        try
        {
            await ticks.WaitForAsync(1);
            using (maintenance.Pause())
            {
                host.AdvanceDays(85);
                await log.WaitForAsync(Skipped);
                Assert.Single(ticks.Times);
            }
            await ticks.WaitForAsync(2);
            Assert.Equal(Base.AddDays(85), ticks.Times[1]);
        }
        finally
        {
            await watcher.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>A started watcher over a recording ICaseService, a simulated clock over a fixed real time and a recording logger.</summary>
    private sealed class Watched : IAsyncDisposable
    {
        private readonly ServiceProvider _services;

        private Watched()
        {
            var (cases, ticks) = TickRecorder.Create();
            Ticks = ticks;
            _services = new ServiceCollection().AddScoped<ICaseService>(_ => cases).BuildServiceProvider();
            Watcher = new DeadlineWatcher(_services.GetRequiredService<IServiceScopeFactory>(), Clock, Log, Maintenance);
        }

        public SimClock Clock { get; } = new(new ManualTime(Base));
        public DemoMaintenance Maintenance { get; } = new();
        public RecordingLogger Log { get; } = new();
        public TickRecorder Ticks { get; }
        public DeadlineWatcher Watcher { get; }

        /// <summary>Returns after the tick at start: by then the watcher listens to the clock and to the pause.</summary>
        public static async Task<Watched> StartAsync()
        {
            var watched = new Watched();
            try
            {
                await watched.Watcher.StartAsync(CancellationToken.None);
                await watched.Ticks.WaitForAsync(1);
                return watched;
            }
            catch
            {
                await watched.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Watcher.StopAsync(CancellationToken.None);
            Watcher.Dispose();
            await _services.DisposeAsync();
        }
    }

    /// <summary>
    /// The ICaseService the watcher sees: it records TickAsync and refuses every other member. A DispatchProxy, not a
    /// hand-written fake, so a member added to ICaseService needs no change here (not sealed: the proxy derives from it).
    /// </summary>
    private class TickRecorder : DispatchProxy
    {
        private readonly List<DateTime> _times = [];
        private readonly SemaphoreSlim _called = new(0);

        public IReadOnlyList<DateTime> Times { get { lock (_times) return [.. _times]; } }

        public static (ICaseService Cases, TickRecorder Recorder) Create()
        {
            var cases = DispatchProxy.Create<ICaseService, TickRecorder>();
            return (cases, (TickRecorder)cases);
        }

        public async Task WaitForAsync(int ticks)
        {
            while (true)
            {
                lock (_times) if (_times.Count >= ticks) return;
                Assert.True(await _called.WaitAsync(TimeSpan.FromSeconds(10)), $"The watcher did not tick {ticks} time(s).");
            }
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(ICaseService.TickAsync))
                throw new NotSupportedException($"The watcher only calls TickAsync, not {targetMethod?.Name}.");
            lock (_times) _times.Add((DateTime)args![0]!);
            _called.Release();
            return Task.FromResult(new DeadlineTick(0, 0, 0));
        }
    }

    /// <summary>Keeps every line with its level and wakes the test on each one.</summary>
    private sealed class RecordingLogger : ILogger<DeadlineWatcher>
    {
        private readonly List<(LogLevel Level, string Message)> _lines = [];
        private readonly SemaphoreSlim _written = new(0);

        public IReadOnlyList<(LogLevel Level, string Message)> Lines { get { lock (_lines) return [.. _lines]; } }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_lines) _lines.Add((logLevel, formatter(state, exception)));
            _written.Release();
        }

        /// <summary>Waits until <paramref name="count"/> lines contain <paramref name="text"/>.</summary>
        public async Task WaitForAsync(string text, int count = 1)
        {
            while (true)
            {
                lock (_lines) if (_lines.Count(l => l.Message.Contains(text, StringComparison.Ordinal)) >= count) return;
                Assert.True(await _written.WaitAsync(TimeSpan.FromSeconds(10)), $"Fewer than {count} log line(s) with \"{text}\".");
            }
        }
    }
}
