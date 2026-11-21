<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-build-an-environment-from-a-spec
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 3 of 3 cases pass on windows.
## Findings
1. The spec's fields are passed to start_server unchanged (a null field is refused by the schema, so absent fields are left out).
2. Found while writing: an MQTT QoS 0 publish through curl-mcp returns before the broker keeps it; read_retained_message straight after answered {"retained":false} six times in six. The program waits on wait_for_event (store bus) first. Recorded in facts/mqtt.md.
3. All-or-nothing: a refused entry stops the servers already started; list_servers then shows none.
