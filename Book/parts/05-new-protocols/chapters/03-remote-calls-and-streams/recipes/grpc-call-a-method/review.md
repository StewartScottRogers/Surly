<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: grpc-call-a-method

Verdict: accept with fixes

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 71807f05f), Curl port 8.22.0 (Curl commit 82913be53d): 14 of 14 cases pass on windows.

## Findings

1. Fixed in drafting (Truth 4): removed "on the same connection" (reflection and call) and "works against a server without reflection"; neither had a case. Added `describe-service` so the sentence on base64 file descriptors is a claim.
2. Finding the methods: reflection's `listServices` (a case) names the services; the methods and fields come from the `.proto` file, written here as the fixture `echo.proto` from Surl ADR-0116. ADR-0116 says `surl --aihelp grpc` prints that file; this build does not (it names the methods only in prose). Reported to the lead.
3. Left out on purpose (Truth 5): with no account at all, this build serves an anonymous call even without `--allow-anonymous`, while the SignalR and Socket.IO servers refuse one, and `surl --aihelp grpc` gives `--allow-anonymous` as the form "for a test". The solution keeps `--allow-anonymous` (Fitness 3: it is not needed on this build with no account); the refusal is shown on `locked`, a server with an account. Reported as a possible program finding.
4. Loosening: `--allow-plaintext-auth` has its refusal (`locked-password-in-clear`, PERMISSION_DENIED) and its effect (`loose-password`, `loose-wrong-password`). `grpcs` is named, not shown; a TLS case belongs to a later recipe.
5. Sources wanted, not cited (not in the bibliography): gRPC server reflection (grpc/grpc doc/server-reflection.md) and the Protocol Buffers ProtoJSON format (protobuf.dev/programming-guides/json). The Discussion's sentence on field names and default-value omission rests on the latter.
6. `not-http1`: curl's "HTTP/0.9" wording is how it reads the server's HTTP/2 frame; the claim says so and no more.
