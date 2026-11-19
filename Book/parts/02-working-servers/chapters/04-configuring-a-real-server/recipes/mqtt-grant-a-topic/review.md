<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-grant-a-topic

Verdict: accept with fixes

Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 8 of 8 cases pass on windows.

## Findings

1. Needs the author: the recipe uses `--allow-plaintext-auth` as an explicit test loosening, because an MQTT password over `mqtt://` is refused by default. A second server, `strict`, makes that refusal a case (`CONNACK` 5, curl exit 8) instead of a citation. The alternative, `mqtts://` with `--self-signed`, belongs to the secure-servers tier.
2. Needs the author: Bob's subscription reads Ann's publish only because surl keeps every PUBLISH as the topic's retained message, though curl sends RETAIN=0 (fixed header 0x30). That is documented in `surl --aihelp mqtt` but contradicts MQTT 3.1.1 section 3.3.1.3. The prose states it as surl's documented behaviour, cited to the README. Program finding in the chapter report.
3. Fixed: `--topic "shop/#;retain"` was dropped from the server: over MQTT, with every publish already retained, it had no effect a case could show. `--topic` stays, with a case for its flag check only. A case for an invalid filter was not possible: `--topic "shop/#/orders"` starts instead of exiting 2 (program finding).
4. `-m` was dropped: curl's MQTT subscribe exits after the first retained message.
5. The generated Solution text says "`curl` prints what it got", but the publish prints nothing.
