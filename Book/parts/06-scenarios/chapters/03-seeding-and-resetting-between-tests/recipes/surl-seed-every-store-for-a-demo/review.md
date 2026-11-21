<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-seed-every-store-for-a-demo
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 5 of 5 cases pass on windows.
## Findings
1. Program finding (wording): add_ldap_entries refusal text reads 'The LDIF entries was refused'; matched as given.
2. Redis, LDAP and MQTT clients are shown refused without a login rather than logged in; the demo's logins are other recipes' subject (linked).
