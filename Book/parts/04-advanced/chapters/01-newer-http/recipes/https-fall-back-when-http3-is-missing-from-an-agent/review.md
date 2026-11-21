<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-fall-back-when-http3-is-missing-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 5 of 5 cases pass on windows.
## Findings
1. `transfer` has no version field; the `curl` tool carries `--http3` and `--http3-only`. Failures are tool errors with `failure.text` `curl exit code 56 (RecvError)` and `curl exit code 28 (OperationTimedOut)`.
2. The swallowing TFTP server is started on the HTTPS server's port, with `stop_server` and a retry on refusal (port pairing can fail on Windows); `list_servers` shows it took the QUIC datagrams.
3. Same 300 s `--http3-only` wait as the console twin without `--connect-timeout`.
4. CI fix (run 44871788391): `--http3-only` against nothing on UDP timed out at 60 s on Linux and macOS (curl-mcp never sees the port unreachable; Curl BL-5659). The program now passes `--connect-timeout 2` there too; `insisting-fails` is `[windows]` 56, new `insisting-times-out` `[linux, macos]` 28.
