<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-publish-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f), surl-mcp 1.0.0, curl-mcp 0.1.0.0: 6 of 6 cases pass on windows.
## Findings
1. Title "Publishing a Retained Message from an Agent" avoids the acronym in the title; MQTT is spelled out on first use in the Problem.
2. Needs the author (program finding): surl's MQTT listener keeps a PUBLISH without the RETAIN flag as the retained message. `curl -s -d 22.0 mqtt://.../sensors/3` sends first byte 0x30 (seen in get_exchanges), and `read_retained_message` then answers `{"topic":"sensors/3","qos":0,"text":"22.0"}`; MQTT 3.1.1 section 3.3.1.3 says a server must not store it. `publish_message` without `retain` behaves correctly. Not claimed.
3. The default server refuses curl's connection with no login (curl exit 8; curl -v shows CONNACK code 5); the loosening, `--allow-anonymous`, is its own server and case, with its warning matched in `log_lines`.
4. The subscriber case matches curl's output byte for byte, including the two-byte topic length (`\0\t`); the book shows it as JSON escapes. Not claimed: that curl exits after the first message.
