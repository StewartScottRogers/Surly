// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test of the timeout path, with no sleeping: the server runs on a clock the test moves, so the
// test's wait for an app's upload times out when the test says so, not after a minute of real time.
// Run: dotnet run wait.cs [-- case]
//   (no argument)  the app never uploads; the clock moves 59 s, then 1 s more
//   arrives        the app uploads the report; the wait ends with the clock where it was
//   wrong-path     the app uploads under another name; the wait for the report times out
//   no-advance     the app never uploads and the clock is never moved; only the real-time guard ends the wait
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Diagnostics;
using Curl.Api;
using Curl.Api.Options.Authentication;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "missing";

// The server runs on the test's clock. RecordExchanges keeps the events the wait looks for;
// the app logs in as export to upload, so uploads are allowed and export has an account.
var clock = new FakeClock();
var options = new SurlServerOptions
{
    ListenUrls = { "http://127.0.0.1:0" },
    TimeProvider = clock,
    RecordExchanges = true,
    AllowUploads = true,
};
options.Accounts.Add(new SurlAccount("export", "exportpass"));
options.Files["reports/README.txt"] = "Nightly reports land here.\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);

// The wait: the report written, sixty seconds of the test's clock at most. The token is real time, a
// guard, so a wait the clock does not end fails after a few seconds instead of hanging the test.
using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(which == "no-advance" ? 2 : 20));
var report = new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Content, Name = "/reports/daily.csv" };
var real = Stopwatch.StartNew();
Task<SurlEvent> wait = server.WaitForEventAsync(report, TimeSpan.FromSeconds(60), guard.Token);

// The app: the nightly export, which uploads its report with a Digest login.
if (which is "arrives" or "wrong-path")
{
    string name = which == "arrives" ? "daily.csv" : "daily-old.csv";
    await using CurlClient app = CurlClient.Create();
    CurlResponse put = await app.SendAsync(new CurlRequest(HttpMethod.Put, $"{server.BoundUrl}reports/{name}")
    {
        Authentication = new CurlAuthenticationOptions { UserName = "export", Password = "exportpass", HttpAuthentication = HttpAuthenticationMethods.Digest },
        Body = "region,total\nnorth,1200\n"u8.ToArray(),
    }, CancellationToken.None);
    await put.Body.DisposeAsync();
    Console.WriteLine($"app uploaded /reports/{name}: {put.StatusCode}, Date {put.Headers.First(h => h.Key == "Date").Value}");
}

if (which == "arrives")
{
    var written = (StoreChangedEvent)await wait;
    Console.WriteLine($"the wait ended: {written.Change} {written.Name}");
    Console.WriteLine($"fake time spent waiting: {(clock.GetUtcNow() - FakeClock.Start).TotalSeconds} s");
    return 0;
}

if (which == "no-advance")
{
    try
    {
        await wait;
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine($"no Advance: the wait was still open after {Math.Floor(real.Elapsed.TotalSeconds)} s of real time, and the guard cancelled it");
    }

    return 1;
}

// Nothing has come for the report, so the wait stays open however long real time runs.
await Task.Delay(300);
Console.WriteLine($"after 0 s of fake time: {(wait.IsCompleted ? "ended" : "waiting")}");
clock.Advance(TimeSpan.FromSeconds(59));
await Task.Delay(300);
Console.WriteLine($"after 59 s of fake time: {(wait.IsCompleted ? "ended" : "waiting")}");
clock.Advance(TimeSpan.FromSeconds(1));
try
{
    await wait;
    Console.WriteLine("the report arrived");
    return 0;
}
catch (TimeoutException timedOut)
{
    Console.WriteLine($"after 60 s of fake time: TimeoutException: {timedOut.Message}");
}

Console.WriteLine($"real time spent: under ten seconds: {real.Elapsed < TimeSpan.FromSeconds(10)}");
return 1;

// A clock the test moves. Advance sets the time forward and fires every timer that has come due,
// which is what ends a wait: the server asks its TimeProvider for the timer behind the timeout.
sealed class FakeClock : TimeProvider
{
    public static readonly DateTimeOffset Start = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly object gate = new();
    private readonly List<FakeTimer> timers = [];
    private DateTimeOffset now = Start;

    public override DateTimeOffset GetUtcNow()
    {
        lock (gate)
        {
            return now;
        }
    }

    public override long GetTimestamp() => GetUtcNow().UtcTicks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new FakeTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        FakeTimer[] due;
        lock (gate)
        {
            now += by;
            due = timers.Where(timer => timer.Due <= now).ToArray();
        }

        foreach (FakeTimer timer in due)
        {
            timer.Fire();
        }
    }

    internal void Arm(FakeTimer timer)
    {
        lock (gate)
        {
            if (!timers.Contains(timer))
            {
                timers.Add(timer);
            }
        }
    }

    internal void Disarm(FakeTimer timer)
    {
        lock (gate)
        {
            timers.Remove(timer);
        }
    }
}

sealed class FakeTimer(FakeClock clock, TimerCallback callback, object? state) : ITimer
{
    private TimeSpan period = Timeout.InfiniteTimeSpan;

    public DateTimeOffset Due { get; private set; }

    public bool Change(TimeSpan dueTime, TimeSpan newPeriod)
    {
        if (dueTime == Timeout.InfiniteTimeSpan)
        {
            clock.Disarm(this);
            return true;
        }

        period = newPeriod;
        Due = clock.GetUtcNow() + dueTime;
        clock.Arm(this);
        return true;
    }

    public void Fire()
    {
        if (period == Timeout.InfiniteTimeSpan || period == TimeSpan.Zero)
        {
            clock.Disarm(this);
        }
        else
        {
            Due = clock.GetUtcNow() + period;
        }

        callback(state);
    }

    public void Dispose() => clock.Disarm(this);

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
