<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: amqps-log-in-over-tls-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797, dotnet 11.0.100: 9 of 9 cases pass on windows.

## Findings
1. Only Windows run. A refused curl login is a tool error, so those cases exit 1.
