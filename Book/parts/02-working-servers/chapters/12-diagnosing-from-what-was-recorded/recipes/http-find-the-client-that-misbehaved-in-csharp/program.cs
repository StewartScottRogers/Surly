// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Three clients use one in-process server at once; one sends a malformed request. The test walks
// the server's recorded exchanges for the answer 400 and prints the client endpoint and the bytes
// of the request that caused it.
// Run: dotnet run program.cs -- <billing port> <search port> <reports port> [record]
//   the three ports are the local ports each client connects from, so the record can be matched to them
//   record   what the record holds for each connection, instead of the search for the 400
#:package Surly.Surl.Api@0.1.12
#:package Surly.Curl.Api@0.4.0-preview.6

using System.Text;
using Curl.Api;
using Surl.Api;

var ports = new Dictionary<string, int> { ["billing"] = int.Parse(args[0]), ["search"] = int.Parse(args[1]), ["reports"] = int.Parse(args[2]) };
string which = args.Length > 3 ? args[3] : "find";

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" }, RecordExchanges = true };
options.Files["hello.txt"] = "hello from surl\n"u8.ToArray();
await using SurlServer server = await SurlServer.StartAsync(options);

// The clients, side by side. Each has its own CurlClient, so its own connection, from its own local port.
// The search client's last request carries a header whose name has a space in it.
string[][] results = await Task.WhenAll(
    Client("billing", 5, bad: false),
    Client("search", 3, bad: true),
    Client("reports", 5, bad: false));
Console.WriteLine($"statuses: {string.Join("; ", ports.Keys.Zip(results, (name, codes) => $"{name} {string.Join(" ", codes)}"))}");

// The server's record: one SurlExchangeRecord per connection, its chunks in the order they crossed.
IReadOnlyList<SurlExchangeRecord> exchanges = server.Exchanges;
if (which == "record")
{
    Console.WriteLine($"exchanges: {exchanges.Count}");
    foreach (SurlExchangeRecord exchange in exchanges.OrderBy(e => ports.First(p => Port(e) == p.Value).Key))
    {
        int requests = Encoding.ASCII.GetString(exchange.Received).Split("\r\n").Count(l => l.StartsWith("GET "));
        Console.WriteLine($"{ports.First(p => Port(exchange) == p.Value).Key}: one exchange from 127.0.0.1, {requests} requests");
    }
    return 0;
}

// Walk each exchange: a received chunk that starts with a request line is a request; a sent chunk
// that starts with "HTTP/1.1 400" is the answer to the last one.
int found = 0;
foreach (SurlExchangeRecord exchange in exchanges)
{
    byte[] request = [];
    foreach (SurlExchangeChunk chunk in exchange.Chunks)
    {
        string text = Encoding.ASCII.GetString(chunk.Bytes.Span);
        if (chunk.Direction == SurlExchangeDirection.Received && text.StartsWith("GET ", StringComparison.Ordinal))
        {
            request = chunk.Bytes.ToArray();
        }
        else if (chunk.Direction == SurlExchangeDirection.Sent && text.StartsWith("HTTP/1.1 400 ", StringComparison.Ordinal))
        {
            found++;
            Console.WriteLine($"answered 400: exchange {exchange.ExchangeId}, client {exchange.Client}");
            Console.WriteLine($"that is the {ports.First(p => Port(exchange) == p.Value).Key} client's port");
            Console.WriteLine($"request, {request.Length} bytes:");
            foreach (string line in Encoding.ASCII.GetString(request).Split("\r\n").TakeWhile(l => l.Length > 0))
            {
                Console.WriteLine($"  {line}");
            }
        }
    }
}
Console.WriteLine($"answers of 400 in {exchanges.Count} exchanges: {found}");
return 0;

int Port(SurlExchangeRecord exchange) => int.Parse(exchange.Client.ToString()!.Split(':')[^1]);

async Task<string[]> Client(string name, int count, bool bad)
{
    await using CurlClient client = CurlClient.Create(new CurlClientOptions { Network = new CurlNetworkOptions { LocalPort = ports[name] } });
    var codes = new List<string>();
    for (int i = 1; i <= count; i++)
    {
        var request = new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}hello.txt");
        if (bad && i == count)
        {
            request = request with { Headers = [new("X-Request ID", "7")] };
        }
        CurlResponse response = await client.SendAsync(request, CancellationToken.None);
        await using (Stream body = response.Body)
        {
            await body.CopyToAsync(Stream.Null);
        }
        codes.Add(response.StatusCode.ToString());
    }
    return [.. codes];
}
