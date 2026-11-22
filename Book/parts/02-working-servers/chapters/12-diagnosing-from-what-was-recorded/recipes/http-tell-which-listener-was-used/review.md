<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-tell-which-listener-was-used
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 11 of 11 cases pass on windows.
## Findings
1. Finding: the log and trace line `Exchange N opened: <scheme> from <client>` never names the listen URL, so two listeners of one scheme cannot be told apart from them (expected: the listen URL or its port on that line). Only the Host header hints, and it is the client's word. The recipe says so and uses the control endpoint (`/v1/events?kind=exchange_started`, `/v1/exchanges?listen_url=`), which does name it.
2. `ready: none` plus a seed line waiting on the control endpoint (it listens last) keeps verify's readiness probe from opening exchanges; exchange numbers are then 1, 2, 3.
3. The generated Solution listing guesses a `Listening on` line for the control port; surl actually writes `Control endpoint listening on ...`. Build cosmetics, not a claim.
