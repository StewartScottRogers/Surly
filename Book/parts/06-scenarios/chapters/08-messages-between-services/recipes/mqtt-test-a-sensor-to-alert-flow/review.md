<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-test-a-sensor-to-alert-flow
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 8 of 8 cases pass on windows.
## Findings
1. The service is alert.sh, a stand-in for the reader's own; it polls the retained reading (five tries a second apart) because a QoS 0 publish can return before the broker keeps it (platforms.md, macOS).
2. The alert mail is waited for with curl --retry 10 --retry-all-errors --retry-delay 1 on pop3://.../1; no fixed sleep.
3. Failure paths on a second server: under and at the limit (no mail, empty POP3 list) and a sensor that never published (exit 1).
4. curl -T alert.eml to smtp://host:port/ sends EHLO alert.eml (the tool appends the file name to a URL with no path, as upstream does); not claimed.
