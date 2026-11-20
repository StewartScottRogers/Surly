<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: redis-inspect-keys-an-app-left

Verdict: accept with fixes

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows.

## Findings
1. Scenario: the app is played by the curl tool (five SET runs); the check of what the server recorded is get_events (store_changed, login_judged); the failure path is a wrong password (WRONGPASS, exit 67, a refused login in the record, no keys).
2. list_keys gives expires_at only for a key with an expiry, so flagging a key without one needs no per-key read; the program reads values with read_key anyway.
3. Chapter 12's chapter.adoc had its placeholder line; two sentences written.
