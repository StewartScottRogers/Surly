// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Bridges AMQP 0-9-1 and AMQP 1.0 with Surl.Api and Curl.Api, in one process: a message put on a queue is
// received over either version, and once over one it is gone for the other.
// Run: dotnet run program.cs -- <scenario>
//   solution   a message on orders, received over 1.0, then looked for over 0-9-1
//   reverse    a message on orders, received over 0-9-1
//   order      two messages, taken one over each version, in the order sent
//   address    a different address holds nothing; the message is still on orders
//   wrong      a wrong password on each listener
//   locked     a server with no AllowPlaintextAuthentication
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using System.Text;
using Curl.Api;
using Curl.Api.Errors;
using Surl.Api;

string scenario = args.Length > 0 ? args[0] : "solution";

// One server, two listeners: AMQP 0-9-1 and AMQP 1.0 share its message bus. AllowPlaintextAuthentication is
// --allow-plaintext-auth, which lets alice log in with no TLS; the "locked" scenario leaves it off.
var options = new SurlServerOptions
{
    ListenUrls = { "amqp://127.0.0.1:0", "amqp1://127.0.0.1:0" },
    AllowPlaintextAuthentication = scenario != "locked",
};
options.Accounts.Add(new SurlAccount("alice", "s3cret"));
options.Queues.Add("orders");
await using SurlServer server = await SurlServer.StartAsync(options);
await using CurlClient client = CurlClient.Create();

Uri amqp091 = server.BoundUrls[0];
Uri amqp1 = server.BoundUrls[1];

switch (scenario)
{
    case "solution":
        await Put("hello");
        Console.WriteLine($"over 1.0: {await Receive("1.0", "orders")}");
        Console.WriteLine($"over 0-9-1 after: [{await Receive("0-9-1", "orders")}]");
        break;
    case "reverse":
        await Put("back");
        Console.WriteLine($"over 0-9-1: {await Receive("0-9-1", "orders")}");
        break;
    case "order":
        await Put("one");
        await Put("two");
        Console.WriteLine($"over 1.0: {await Receive("1.0", "orders")}, then over 0-9-1: {await Receive("0-9-1", "orders")}");
        break;
    case "address":
        await Put("only-orders");
        // An AMQP 1.0 receive from an address with nothing on it waits, so give it two seconds.
        using (var wait = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
        {
            Console.WriteLine($"refunds over 1.0: {await Receive("1.0", "refunds", wait.Token)}");
        }

        Console.WriteLine($"orders over 1.0: {await Receive("1.0", "orders")}");
        break;
    case "wrong":
        Console.WriteLine($"1.0: {await Receive("1.0", "orders", password: "wrong")}");
        Console.WriteLine($"0-9-1: {await Receive("0-9-1", "orders", password: "wrong")}");
        break;
    case "locked":
        Console.WriteLine($"1.0: {await Receive("1.0", "orders")}");
        Console.WriteLine($"0-9-1: {await Receive("0-9-1", "orders")}");
        break;
}

return 0;

// AMQP 0-9-1 names a virtual host and then the queue; %2F is the virtual host /. AMQP 1.0 has only the address.
string Target(string version, string name) =>
    version == "0-9-1" ? $"{amqp091}%2F/{name}" : $"{amqp1}{name}";

CurlRequest Request(HttpMethod method, string version, string name, string password) =>
    new(method, Target(version, name))
    {
        Authentication = new CurlAuthenticationOptions { UserName = "alice", Password = password },
    };

// The publisher, from the server's side: Curl.Api does not publish over AMQP, so the message is put on the
// queue directly, as a producer of either version would leave it.
Task Put(string text) => server.Messages.EnqueueAsync("orders", Encoding.UTF8.GetBytes(text));

// A GET takes one message and answers its body; an empty 0-9-1 queue answers nothing.
async Task<string> Receive(string version, string name, CancellationToken cancellationToken = default, string password = "s3cret")
{
    try
    {
        CurlResponse response = await client.SendAsync(Request(HttpMethod.Get, version, name, password), cancellationToken);
        await using Stream body = response.Body;
        return await new StreamReader(body).ReadToEndAsync(cancellationToken);
    }
    catch (CurlException failed)
    {
        return $"CurlException {(int)failed.Code} {failed.Code}: {failed.Message}";
    }
    catch (OperationCanceledException)
    {
        return "cancelled";
    }
}
