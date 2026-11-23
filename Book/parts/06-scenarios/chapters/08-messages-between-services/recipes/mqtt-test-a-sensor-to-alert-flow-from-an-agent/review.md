<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-test-a-sensor-to-alert-flow-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 10 of 10 cases pass on windows.
## Findings
1. Carries the console's claims plus the delivery event and one refusal: surl-mcp list_messages and read_message refuse the anonymous mailbox's empty owner (schema minLength 1, McpProtocolException invalid params), so an agent cannot read mail kept under --allow-anonymous with the store tools; the agent reads it over POP3 with the curl tool. Reported as a program finding.
2. wait_for_event exchange_ended after every MQTT call, store_changed on mail before reading the mail.
Re-pinned to Surl v0.1.5 (d64faa4bb) / Curl 2145a49eae: surl-mcp list_messages takes the empty owner (the --allow-anonymous mailbox); empty-owner-refused became empty-owner-listed (1 message, UID 1); the mail is still read over POP3. Finding 1 no longer holds. 10 of 10 pass on windows.
