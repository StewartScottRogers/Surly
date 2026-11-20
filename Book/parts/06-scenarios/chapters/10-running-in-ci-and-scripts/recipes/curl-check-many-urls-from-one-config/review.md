<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: curl-check-many-urls-from-one-config
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 4 of 4 cases pass on windows.
## Findings
1. Ports reach the config through curl's --variable and expand-url, since fixtures are not substituted; {{http_port}} in urls.conf is curl syntax, not the build's.
2. --allow-anonymous (for curl's anonymous FTP login) has its refusal shown on the broken server (530, exit 67).
3. Not claimed: an unset variable expands to nothing (URL falls back to port 80); left out because port 80 may be open on a CI machine.
