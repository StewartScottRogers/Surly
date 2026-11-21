<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: grpc-test-a-failing-call-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0: 6 of 6 cases pass on windows.
## Findings
1. Program finding (see the proto recipe): Curl.Api 0.2.0 cannot send a request message to a gRPC method, so it cannot ask for a status. The program uses the framework's `HttpClient` and drops the Curl.Api `#:package` line, which it would not use; `api-types` lists the Surl.Api types alone.
2. The status is read from the trailers, or from the headers when the call fails before any reply (a trailers-only response); `grpc-message` is percent-decoded with `Uri.UnescapeDataString`.
3. The exit codes printed (`curl exit 22`) are the program's own mapping, said so in the Discussion.
4. Checked for the `grpc-timeout` race of its twins (CI run 44835381862): this program sends no `grpc-timeout` and cancels on its own token, so the server cannot answer first; unchanged.
