<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: az-test-an-azure-upload-locally-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797: 12 of 12 cases pass on windows.
## Findings
1. One program, twelve cases by argument, one per console claim; a refused start is a tool error whose structured content has `exit_code` 58.
2. `transfer` with a `JsonArray` of headers (the SDK's serializer refuses anonymous types and List<object>).
