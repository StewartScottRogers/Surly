// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// urls.c, which parses, joins and builds URLs with libcurl's URL API (curl_url_set, curl_url_get)
// and fetches a built one through CURLOPT_CURLU, ported call for call to C# with Libcurl.
// Run: dotnet run urls.cs -- parse <url>
//      dotnet run urls.cs -- join <base> <relative>
//      dotnet run urls.cs -- build <host> <port> <path> [<query>...]
#:package Surly.Libcurl@0.4.0-preview.7

using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;
using Libcurl.Url;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: dotnet run urls.cs -- parse <url> | join <base> <relative> | build <host> <port> <path> [<query>...]");
    return 2;
}

await using CurlLibrary library = CurlGlobal.Init(CurlGlobalFlags.Default); // curl_global_init(CURL_GLOBAL_DEFAULT)
using CurlUrlHandle h = library.UrlInit();                              // h = curl_url()
CurlUrlCode uc;

switch (args[0])
{
    case "parse":
        uc = h.Set(CurlUrlPart.Url, args[1], CurlUrlFlags.None);          // curl_url_set(h, CURLUPART_URL, argv[2], 0)
        if (uc != CurlUrlCode.Ok)
        {
            return Fail("curl_url_set", uc);
        }

        foreach (CurlUrlPart part in new[] { CurlUrlPart.Scheme, CurlUrlPart.Host, CurlUrlPart.Port, CurlUrlPart.Path, CurlUrlPart.Query })
        {
            uc = h.Get(part, CurlUrlFlags.None, out string? value);      // curl_url_get(h, part, &value, 0)
            Console.WriteLine(uc == CurlUrlCode.Ok ? $"{part}: {value}" : $"{part}: ({(int)uc}) {CurlUrlHandle.ErrorText(uc)}");
        }

        h.Get(CurlUrlPart.Port, CurlUrlFlags.DefaultPort, out string? port); // curl_url_get(h, CURLUPART_PORT, &port, CURLU_DEFAULT_PORT)
        Console.WriteLine($"Port with CURLU_DEFAULT_PORT: {port}");
        break;

    case "join":
        uc = h.Set(CurlUrlPart.Url, args[1], CurlUrlFlags.None);          // curl_url_set(h, CURLUPART_URL, base, 0)
        if (uc == CurlUrlCode.Ok)
        {
            uc = h.Set(CurlUrlPart.Url, args[2], CurlUrlFlags.None);      // curl_url_set(h, CURLUPART_URL, relative, 0): resolved against the URL already set
        }

        if (uc != CurlUrlCode.Ok)
        {
            return Fail("curl_url_set", uc);
        }

        h.Get(CurlUrlPart.Url, CurlUrlFlags.None, out string? joined);    // curl_url_get(h, CURLUPART_URL, &url, 0)
        Console.WriteLine(joined);
        break;

    case "build":
        h.Set(CurlUrlPart.Scheme, "http", CurlUrlFlags.None);             // curl_url_set(h, CURLUPART_SCHEME, "http", 0)
        h.Set(CurlUrlPart.Host, args[1], CurlUrlFlags.None);              // curl_url_set(h, CURLUPART_HOST, argv[2], 0)
        uc = h.Set(CurlUrlPart.Port, args[2], CurlUrlFlags.None);         // curl_url_set(h, CURLUPART_PORT, argv[3], 0)
        if (uc != CurlUrlCode.Ok)
        {
            return Fail("curl_url_set", uc);
        }

        h.Set(CurlUrlPart.Path, args[3], CurlUrlFlags.UrlEncode);         // curl_url_set(h, CURLUPART_PATH, argv[4], CURLU_URLENCODE)
        foreach (string query in args[4..])
        {
            h.Set(CurlUrlPart.Query, query, CurlUrlFlags.AppendQuery | CurlUrlFlags.UrlEncode); // CURLU_APPENDQUERY | CURLU_URLENCODE
        }

        h.Get(CurlUrlPart.Url, CurlUrlFlags.None, out string? built);     // curl_url_get(h, CURLUPART_URL, &url, 0)
        Console.WriteLine(built);

        // Fetch it: CURLOPT_CURLU hands the easy handle the parsed URL, not a string.
        using (CurlEasy curl = library.EasyInit())                        // curl = curl_easy_init()
        {
            Stream stdout = Console.OpenStandardOutput();
            WriteCallback write = data =>
            {
                stdout.Write(data);                                       // fwrite(data, size, nmemb, stdout)
                return data.Length;
            };
            curl.SetOption(CurlOption.Curlu, h);                          // curl_easy_setopt(curl, CURLOPT_CURLU, h)
            curl.SetOption(CurlOption.Writefunction, write);              // curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, write_cb)
            CurlCode res = curl.Perform();                                // res = curl_easy_perform(curl)
            if (res != CurlCode.Ok)
            {
                Console.Error.WriteLine($"curl_easy_perform() failed: {CurlErrorText.Easy(res)}");
                return (int)res;
            }

            curl.GetInfo(CurlInfo.ResponseCode, out long code);           // curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &code)
            Console.WriteLine($"response code {code}");
        }

        break;

    default:
        Console.Error.WriteLine($"unknown command {args[0]}");
        return 2;
}

return 0;                                                                // curl_url_cleanup and curl_global_cleanup run as the usings end

// fprintf(stderr, "%s: (%d) %s\n", call, (int)uc, curl_url_strerror(uc)); return 1;
static int Fail(string call, CurlUrlCode uc)
{
    Console.Error.WriteLine($"{call}: ({(int)uc}) {CurlUrlHandle.ErrorText(uc)}");
    return 1;
}
