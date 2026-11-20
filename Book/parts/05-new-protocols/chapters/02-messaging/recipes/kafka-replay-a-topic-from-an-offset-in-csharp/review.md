<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: kafka-replay-a-topic-from-an-offset-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 7 of 7 cases pass on windows.
## Findings
1. Program finding for the author: Curl.Api 0.2.0 cannot produce to Kafka. A `CurlRequest` with `HttpMethod.Post` (or Put, or Get) and a byte or stream body to `kafka://127.0.0.1:port/orders` returns status 0 and no error, and no record appears (`server.Messages.ReadLogAsync` shows nothing); the console `curl -d` produces. The program therefore seeds with `server.Messages.PublishAsync` and consumes with Curl.Api `GET` (offset in the query works). Not stated in the recipe: no case shows the failed produce.
2. Curl.Api maps broker error 1 to `CurlException` code 8 `WeirdServerReply` (message `Kafka broker answered error 1 (OFFSET_OUT_OF_RANGE) ...`) and a bad offset to code 3 `UrlMalformat`, the same numbers as the console exits.
3. `ReadLogAsync` from an offset past the end throws `SurlReadRefusedException`, where the console read of an offset equal to the count is an empty answer; the program shows the offsets the client never sees.
