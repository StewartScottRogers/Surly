// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// A test waits for the mail an app sends, and the wait runs on a fake clock: the test moves the clock, so a mail
// that never comes fails at once and not after thirty real seconds. Run: dotnet run program.cs [-- case]
//   (no argument)    the app mails ann; the wait ends with the clock where it was
//   missing          the app mails no one; the clock moves 29 s, then 1 s more
//   wrong-recipient  the app mails bob; the wait for ann times out, and bob's mail is there
//   no-plaintext     the server keeps its default and refuses the app's password; the wait times out
#:package Surly.Surl.Api@*

using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using Surl.Api;
using Surl.Protocol.Abstractions;

string which = args.Length > 0 ? args[0] : "arrives";

// The server is built on the test's clock. RecordExchanges keeps the events the wait looks for.
var clock = new FakeClock();
var options = new SurlServerOptions
{
    ListenUrls = { "smtp://127.0.0.1:0" },
    TimeProvider = clock,
    RecordExchanges = true,
    AllowPlaintextAuthentication = which != "no-plaintext",
};
options.MailDomains.Add("example.com");
options.Mailboxes.Add("app@example.com:apppass");
options.Mailboxes.Add("ann@example.com:annpass");
options.Mailboxes.Add("bob@example.com:bobpass");
await using var server = await SurlServer.StartAsync(options);

// The wait: mail delivered to ann, thirty seconds of the test's clock at most. The token is real time, a
// guard so that a wait the clock does not govern fails after twenty seconds and does not hang the test.
using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(20));
var ann = new SurlEventQuery { Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail, Name = "ann@example.com" };
var real = Stopwatch.StartNew();
Task<SurlEvent> wait = server.WaitForEventAsync(ann, TimeSpan.FromSeconds(30), guard.Token);

if (which != "missing")
{
    string to = which == "wrong-recipient" ? "bob@example.com" : "ann@example.com";
    Uri smtp = server.BoundUrl;
    await Task.Run(() =>
    {
        try
        {
            using var app = new SmtpClient(smtp.Host, smtp.Port) { Credentials = new NetworkCredential("app@example.com", "apppass") };
            app.Send("app@example.com", to, "Welcome", "Thanks for signing up.");
        }
        catch (SmtpException refused)
        {
            Console.WriteLine($"app could not send: {refused.StatusCode}");
        }
    });
}

if (which == "arrives")
{
    SurlEvent delivered = await wait;
    Console.WriteLine($"mail for ann: {((StoreChangedEvent)delivered).Change}");
    Console.WriteLine($"fake time spent waiting: {(clock.GetUtcNow() - FakeClock.Start).TotalSeconds} s");
    return 0;
}

// Nothing has arrived for ann, so the wait is still open however long real time runs.
await Task.Delay(300);
Console.WriteLine($"after 0 s of fake time: {(wait.IsCompleted ? "ended" : "waiting")}");
clock.Advance(TimeSpan.FromSeconds(29));
await Task.Delay(300);
Console.WriteLine($"after 29 s of fake time: {(wait.IsCompleted ? "ended" : "waiting")}");
clock.Advance(TimeSpan.FromSeconds(1));
try
{
    await wait;
    Console.WriteLine("mail arrived");
    return 0;
}
catch (TimeoutException)
{
    Console.WriteLine("after 30 s of fake time: TimeoutException");
}

Console.WriteLine($"real time spent: under ten seconds: {real.Elapsed < TimeSpan.FromSeconds(10)}");
if (which == "wrong-recipient")
{
    SurlMessagePage bob = await server.Mail.ListMessagesAsync("bob@example.com", "INBOX");
    Console.WriteLine($"bob's inbox: {bob.Messages.Count} message");
}

return 1;

// A clock the test moves. Advance sets the time forward and fires every timer that has come due, which is
// what ends a wait: the server asks TimeProvider for the timer behind its timeout.
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
