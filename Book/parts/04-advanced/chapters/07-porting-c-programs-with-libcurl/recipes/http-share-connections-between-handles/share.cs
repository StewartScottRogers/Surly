// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// share.c, which fetches two URLs on two easy handles that share one connection cache and one
// cookie store through a share handle, ported call for call to C# with Libcurl. Each line names
// the C call it stands for. The first handle is given a session cookie, as after a login.
// Run: dotnet run share.cs -- <url1> <url2> [both|connect|none|cleanup-early]
//   both           share connections and cookies (the default)
//   connect        share connections only
//   none           no share handle attached
//   cleanup-early  as both, but call curl_share_cleanup while the handles are still attached
#:package Surly.Libcurl@*

using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Share;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;
using Libcurl.Share;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: dotnet run share.cs -- <url1> <url2> [both|connect|none|cleanup-early]");
    return 2;
}

string mode = args.Length > 2 ? args[2] : "both";
await using CurlLibrary library = CurlGlobal.Init(CurlGlobalFlags.Default);  // curl_global_init(CURL_GLOBAL_DEFAULT)
CurlShare share = library.ShareInit();                                       // share = curl_share_init()
if (mode != "none")
{
    share.SetOption(CurlShareOption.Share, CurlLockData.Connect);           // curl_share_setopt(share, CURLSHOPT_SHARE, CURL_LOCK_DATA_CONNECT)
    if (mode != "connect")
    {
        share.SetOption(CurlShareOption.Share, CurlLockData.Cookie);        // curl_share_setopt(share, CURLSHOPT_SHARE, CURL_LOCK_DATA_COOKIE)
    }
}

var handles = new List<CurlEasy>();
for (int i = 0; i < 2; i++)                                                  // for(i = 0; i < 2; i++)
{
    CurlEasy easy = library.EasyInit();                                      // easy[i] = curl_easy_init()
    WriteCallback discard = data => data.Length;                             // static size_t discard(...): return size * nmemb
    easy.SetOption(CurlOption.Url, args[i]);                                // curl_easy_setopt(easy[i], CURLOPT_URL, argv[i + 1])
    easy.SetOption(CurlOption.Writefunction, discard);                       // CURLOPT_WRITEFUNCTION, discard
    easy.SetOption(CurlOption.Cookiefile, "");                               // CURLOPT_COOKIEFILE, "": turn the cookie engine on
    if (mode != "none")
    {
        easy.SetOption(CurlOption.Share, share);                             // curl_easy_setopt(easy[i], CURLOPT_SHARE, share)
    }

    handles.Add(easy);
}

// The session cookie, given to the first handle only, as its login would have set it.
handles[0].SetOption(CurlOption.Cookielist, "Set-Cookie: session=abc123; Domain=127.0.0.1; Path=/"); // CURLOPT_COOKIELIST

if (mode == "cleanup-early")
{
    CurlShareCode early = share.Cleanup();                                   // curl_share_cleanup(share), handles attached
    Console.WriteLine($"curl_share_cleanup: {(int)early} {early}: {CurlErrorText.Share(early)}");
}

int failed = 0;
for (int i = 0; i < 2; i++)
{
    CurlCode result = handles[i].Perform();                                  // res = curl_easy_perform(easy[i])
    handles[i].GetInfo(CurlInfo.ResponseCode, out long status);              // CURLINFO_RESPONSE_CODE
    handles[i].GetInfo(CurlInfo.NumConnects, out long connects);             // CURLINFO_NUM_CONNECTS
    Console.WriteLine($"handle {i + 1}: {(result == CurlCode.Ok ? status.ToString() : result.ToString())}, new connections {connects}");
    failed += result == CurlCode.Ok ? 0 : 1;
}

foreach (CurlEasy easy in handles)
{
    if (mode != "none")
    {
        easy.SetOption(CurlOption.Share, null!);                             // curl_easy_setopt(easy[i], CURLOPT_SHARE, NULL)
    }

    easy.Dispose();                                                          // curl_easy_cleanup(easy[i])
}

CurlShareCode code = share.Cleanup();                                        // curl_share_cleanup(share)
if (mode == "cleanup-early")
{
    Console.WriteLine($"curl_share_cleanup after detaching: {(int)code} {code}");
}

return failed == 0 ? 0 : 1;
