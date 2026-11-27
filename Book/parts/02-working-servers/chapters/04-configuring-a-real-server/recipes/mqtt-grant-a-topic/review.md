<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-grant-a-topic

Verdict: accept with fixes

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b): 9 of 9 cases pass on windows.

## Findings

1. Needs the author: the recipe uses `--allow-plaintext-auth` as an explicit test loosening, because an MQTT password over `mqtt://` is refused by default. A second server, `strict`, makes that refusal a case (`CONNACK` 5, curl exit 8) instead of a citation. The alternative, `mqtts://` with `--self-signed`, belongs to the secure-servers tier.
2. Needs the author: Bob's subscription reads Ann's publish only because surl keeps every PUBLISH as the topic's retained message, though curl sends RETAIN=0 (fixed header 0x30). That is documented in `surl --aihelp mqtt` but contradicts MQTT 3.1.1 section 3.3.1.3. The prose states it as surl's documented behaviour, cited to the README. Program finding in the chapter report.
3. Fixed: `--topic "shop/#;retain"` was dropped from the server: over MQTT, with every publish already retained, it had no effect a case could show. `--topic` stays, with a case for its flag check only. A case for an invalid filter was not possible: `--topic "shop/#/orders"` starts instead of exiting 2 (program finding).
4. `-m` was dropped: curl's MQTT subscribe exits after the first retained message.
5. The generated Solution text says "`curl` prints what it got", but the publish prints nothing.
6. Updated 2026-11-19 for Surl commit 993aa7072: surl now honours MQTT 3.1.1 RETAIN, which resolves finding 2: curl's RETAIN 0 publish is no longer kept. The main server now takes `--topic "shop/#;retain"` (restoring what finding 3 had dropped, now with an effect a case shows), and the prose says that is what keeps Ann's message. New case `not-kept-without-retain` on a third server, `unkept`, the same without `--topic`. The "every publish is retained" claim and its citation are gone.
7. Program finding, still open: `surl --aihelp messages` now says a filter that is not a valid topic filter exits 2 (`topic <filter> is not a valid topic filter`), but `surl --user ann:secret --topic "shop/#/orders" mqtt://127.0.0.1:0/` still starts and serves (probe stopped it after 60 s). So the invalid-filter case from finding 3 is still left out.
8. Updated 2026-11-19: finding 7 is closed, not a bug. `#` at any level of a `--topic` filter is Surl's deliberate behaviour (Surl ADR-0177; `surl --aihelp messages`: `shop/#/orders` is accepted, `shop/a#` exits 2), and the `--aihelp` line about invalid filters means filters that break that rule. The Discussion now says so, cited to <<surl-readme>>, and two cases show it: `hash-at-any-level` (fourth server, `anywhere`, `--topic "shop/#/orders;retain"` keeps `shop/eu/north/orders`, not `shop/eu/returns`) and `hash-must-fill-a-level` (`shop/a#` exits 2).
Re-checked 2026-11-27 on Surl v0.1.11 and Curl v0.4.0-preview.5: passes; the Discussion matches today's behaviour. A RETAIN 0 publish is no longer kept (Surl now follows MQTT 3.1.1 section 3.3.1.3); the recipe keeps messages with --topic "...;retain" and says why. The DISCONNECT that ends a curl subscription is still sent on purpose and explained. The retain findings above are settled.
