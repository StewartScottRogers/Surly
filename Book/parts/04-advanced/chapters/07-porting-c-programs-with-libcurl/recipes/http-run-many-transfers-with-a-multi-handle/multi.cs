// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// multi.c, which downloads every URL on its command line at once on one multi handle and reports
// each result as it finishes, ported call for call to C# with Libcurl. Each line names the C call it stands for.
// Run: dotnet run multi.cs -- <url>...
#:package Surly.Libcurl@*

using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.ErrorText;
using Libcurl.Multi;
using Libcurl.Options;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: dotnet run multi.cs -- <url>...");
    return 2;
}

await using CurlLibrary library = CurlGlobal.Init(CurlGlobalFlags.Default); // curl_global_init(CURL_GLOBAL_DEFAULT)
await using CurlMulti multi = library.MultiInit();                      // multi = curl_multi_init()

var handles = new List<CurlEasy>();
var sizes = new Dictionary<CurlEasy, long>();
foreach (string url in args)                                             // for(i = 1; i < argc; i++)
{
    CurlEasy easy = library.EasyInit();                                  // easy[i] = curl_easy_init()
    sizes[easy] = 0;
    // static size_t count(char *data, size_t size, size_t nmemb, void *userdata): add to *(curl_off_t *)userdata
    WriteCallback count = data =>
    {
        sizes[easy] += data.Length;
        return data.Length;
    };
    easy.SetOption(CurlOption.Url, url);                                 // curl_easy_setopt(easy[i], CURLOPT_URL, argv[i])
    easy.SetOption(CurlOption.Writefunction, count);                     // CURLOPT_WRITEFUNCTION, count (CURLOPT_WRITEDATA, &sizes[i])
    multi.AddHandle(easy);                                               // curl_multi_add_handle(multi, easy[i])
    handles.Add(easy);
}

var clock = System.Diagnostics.Stopwatch.StartNew();
int failed = 0;
int running;
do
{
    CurlMultiCode mc;
    (mc, running) = await multi.PerformAsync(CancellationToken.None);    // mc = curl_multi_perform(multi, &running)
    if (mc == CurlMultiCode.Ok && running > 0)
    {
        (mc, _) = await multi.PollAsync(Memory<CurlWaitSocket>.Empty, 1000, CancellationToken.None); // curl_multi_poll(multi, NULL, 0, 1000, NULL)
    }

    if (mc != CurlMultiCode.Ok)
    {
        Console.Error.WriteLine($"curl_multi failed: {CurlErrorText.Multi(mc)}"); // curl_multi_strerror(mc)
        return 1;
    }

    // while((msg = curl_multi_info_read(multi, &left)))
    for (CurlMessage? msg = multi.InfoRead(out int left); msg is not null; msg = multi.InfoRead(out left))
    {
        if (msg.Type != CurlMultiMessageType.Done)                       // if(msg->msg == CURLMSG_DONE)
        {
            continue;
        }

        CurlEasy easy = msg.Easy;                                        // msg->easy_handle
        easy.GetInfo(CurlInfo.EffectiveUrl, out string? url);            // CURLINFO_EFFECTIVE_URL
        easy.GetInfo(CurlInfo.ResponseCode, out long code);              // CURLINFO_RESPONSE_CODE
        string path = new Uri(url!).AbsolutePath;
        if (msg.Result == CurlCode.Ok)                                   // msg->data.result
        {
            Console.WriteLine($"{path} {code} {sizes[easy]} bytes");
        }
        else
        {
            failed++;
            Console.WriteLine($"{path} failed: {(int)msg.Result} {CurlErrorText.Easy(msg.Result)}"); // curl_easy_strerror
        }

        multi.RemoveHandle(easy);                                        // curl_multi_remove_handle(multi, easy)
        easy.Dispose();                                                  // curl_easy_cleanup(easy)
    }
}
while (running > 0);

Console.WriteLine($"all done in {clock.Elapsed.TotalSeconds:F1} s, {failed} failed");
return failed;                                                           // curl_multi_cleanup and curl_global_cleanup run as the usings end
