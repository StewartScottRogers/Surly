<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-26 (re-pin to Curl v0.4.0-preview.2)

From verifying the *Surly* cookbook's 1,000 recipes in CI against Curl `v0.4.0-preview.2` (`170d579`) and, before it, `f2331ebe`, with Surl v0.1.9 `13e930ac6`. Only findings not already filed.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | Curl.Api, macOS | Cancelling a `CancellationToken` while reading a streaming gRPC response body sometimes aborts the whole process with exit 134 (SIGABRT), instead of throwing `OperationCanceledException`. Seen only on macOS (GitHub `macos-latest`, arm64), in 2 of the 3 runs that reached it; never on Windows or Linux |

## Repro

**1.** Recipe `grpc-read-a-server-stream-in-csharp`, case `replies-as-they-arrive` (`Book/parts/.../grpc-read-a-server-stream-in-csharp/read-a-stream.cs`, mode `arrival`).
- Surl serves a gRPC server-streaming method that sends 10 replies, each held back 300 ms.
- The program: `using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(1));`, then `CurlResponse response = await client.SendAsync(request, stop.Token);`, then `await using Stream body = response.Body;` and `while ((read = await body.ReadAsync(buffer, stop.Token)) > 0) ...`, inside `try { } catch (OperationCanceledException) { }`.
- Observed on macOS: exit 134 and no output at all, so the catch never ran. CI runs 45753694267 (shard macos-latest 3) and 45889681981 (shard macos-latest 6), each failing verify's one retry too; it passed in run 45758308654.
- Expected: `OperationCanceledException` from `ReadAsync` after about 1 s, as on Windows and Linux, where the program prints `cancelled with some of 10 replies`.
- Exit 134 with nothing written suggests an exception on a thread nobody awaits (a native callback or a background read loop) when the token fires mid-frame. The case is marked `platforms: [windows, linux]` until a fix lands.
