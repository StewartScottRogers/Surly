<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: redis-test-a-session-that-expires-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 6 of 6 cases pass on windows.
## Findings
1. set_key expire_ms 6000 after a warm-up call; read_key is a tool error No such key. once expired and has no expires_at for a key with no expiry.
