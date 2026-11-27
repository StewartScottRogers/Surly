// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// Two independent Surl.Api servers in one program, standing in for two services, each with its
// own files and its own free port; one client asks each for both services' files.
// Run: dotnet run two-servers.cs [-- same-port]
//   (no argument) start both on port 0 and fetch from each
//   same-port     start the second on the address and port the first one bound
#:package Surly.Surl.Api@*
#:package Surly.Curl.Api@0.4.0-preview.5

using Curl.Api;
using Surl.Api;

// Two services: each server gets its own options, its own files and a free port.
var ordersOptions = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
ordersOptions.Files["orders.json"] = """{"orders":3}"""u8.ToArray();
var stockOptions = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
stockOptions.Files["stock.json"] = """{"items":12}"""u8.ToArray();

SurlServer orders = await SurlServer.StartAsync(ordersOptions);
if (args is ["same-port"])
{
    // A fixed port is how two servers collide: the second cannot bind the first one's.
    var clash = new SurlServerOptions { ListenUrls = { orders.BoundUrl.ToString() } };
    try
    {
        await using SurlServer second = await SurlServer.StartAsync(clash);
    }
    catch (SurlServerStartException e)
    {
        Console.WriteLine($"second server refused: {e.ExitCode}");
    }

    Console.WriteLine($"orders stopped: {await orders.StopAsync()}");
    return 0;
}

SurlServer stock = await SurlServer.StartAsync(stockOptions);
Console.WriteLine($"orders on {orders.BoundUrl}");
Console.WriteLine($"stock on {stock.BoundUrl}");
Console.WriteLine(orders.BoundUrl.Port != stock.BoundUrl.Port ? "two ports" : "one port");

// One client for both: each server answers only for its own files.
await using (CurlClient client = CurlClient.Create())
{
    foreach ((string name, SurlServer server) in new[] { ("orders", orders), ("stock", stock) })
    {
        foreach (string path in new[] { "orders.json", "stock.json" })
        {
            CurlResponse response = await client.SendAsync(
                new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}{path}"), CancellationToken.None);
            await using Stream body = response.Body;
            // surl's 404 body, "404 Not Found", ends in a newline; trim it to keep one line a request.
            string text = (await new StreamReader(body).ReadToEndAsync()).TrimEnd('\n');
            Console.WriteLine($"{name} /{path}: {response.StatusCode} {text}");
        }
    }
}

// Each stops on its own, with its own exit code.
Console.WriteLine($"orders stopped: {await orders.StopAsync()}");
Console.WriteLine($"stock stopped: {await stock.StopAsync()}");
return 0;
