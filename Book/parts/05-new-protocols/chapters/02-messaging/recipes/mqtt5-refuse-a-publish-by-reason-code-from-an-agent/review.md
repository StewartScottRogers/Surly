<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt5-refuse-a-publish-by-reason-code-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): surl-mcp 1.0.0 (Surl commit 5da5b3eaf) and curl-mcp (Curl commit c84035f797): 9 of 9 cases pass on windows.

## Findings

1. Carries all nine console claims. A refusal is an exitCode and a message in the curl tool's answer, not a tool error.
2. Publishes wait with wait_for_event exchange_ended, bounded at 10 s; the dropped publishes are also shown by read_retained_message.
