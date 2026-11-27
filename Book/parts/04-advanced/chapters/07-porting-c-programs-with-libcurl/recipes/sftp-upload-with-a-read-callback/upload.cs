// Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
// upload.c, which streams a local file to an SFTP server from a read callback after checking the
// server's host key against a pinned SHA-256 hash, ported call for call to C# with Libcurl.
// Each line names the C call it stands for.
// Run: dotnet run upload.cs -- <url> <user:password> <local file> <pin file>
#:package Surly.Libcurl@0.4.0-preview.6

using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;

if (args.Length < 4)
{
    Console.Error.WriteLine("usage: dotnet run upload.cs -- <url> <user:password> <local file> <pin file>");
    return 2;
}

string pin = File.ReadLines(args[3]).First().Trim();                     // fgets(pin, sizeof(pin), fopen(argv[4], "r"))
using FileStream file = File.OpenRead(args[2]);                          // FILE *file = fopen(argv[3], "rb")
long reads = 0;

// static size_t read_cb(char *buffer, size_t size, size_t nitems, void *userdata): fread from the file
ReadCallback readCallback = buffer =>
{
    int n = file.Read(buffer);                                           // n = fread(buffer, 1, size * nitems, file)
    if (n > 0)
    {
        reads++;
    }

    return n;                                                            // 0 is the end of the upload
};

await using CurlLibrary library = CurlGlobal.Init(CurlGlobalFlags.Default); // curl_global_init(CURL_GLOBAL_DEFAULT)
using CurlEasy curl = library.EasyInit();                               // curl = curl_easy_init()
curl.SetOption(CurlOption.Url, args[0]);                                 // curl_easy_setopt(curl, CURLOPT_URL, argv[1])
curl.SetOption(CurlOption.Userpwd, args[1]);                             // CURLOPT_USERPWD, argv[2]
curl.SetOption(CurlOption.SshHostPublicKeySha256, pin);                  // CURLOPT_SSH_HOST_PUBLIC_KEY_SHA256, pin
curl.SetOption(CurlOption.Upload, 1L);                                   // CURLOPT_UPLOAD, 1L
curl.SetOption(CurlOption.Readfunction, readCallback);                   // CURLOPT_READFUNCTION, read_cb
curl.SetOption(CurlOption.InfilesizeLarge, file.Length);                 // CURLOPT_INFILESIZE_LARGE, (curl_off_t)size

CurlCode res = curl.Perform();                                           // res = curl_easy_perform(curl)
if (res != CurlCode.Ok)
{
    // fprintf(stderr, "curl_easy_perform() failed: %s\n", curl_easy_strerror(res))
    Console.Error.WriteLine($"curl_easy_perform() failed: {CurlErrorText.Easy(res)}");
    return (int)res;
}

curl.GetInfo(CurlInfo.SizeUploadT, out long sent);                       // curl_easy_getinfo(curl, CURLINFO_SIZE_UPLOAD_T, &sent)
Console.WriteLine($"uploaded {sent} bytes{(reads > 1 ? ", the read callback called more than once" : "")}");
return 0;                                                                // curl_easy_cleanup and curl_global_cleanup run as the usings end
