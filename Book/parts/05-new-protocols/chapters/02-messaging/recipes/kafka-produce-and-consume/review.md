<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: kafka-produce-and-consume
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 18 of 18 cases pass on windows.
## Findings
1. Fitness (1): a `curl` consume reads to the end of the log and exits by itself, so the recipe needs no time limit and no surl option to end; the Discussion says so.
2. Truth (5): the log is cased: records stay after a consume, `?offset=1`, the end offset (empty, exit 0), past the end (error 1, exit 8), partition 0 only (partition 1 is error 3), invalid topic name (error 17), no topic (exit 3).
3. Truth (7): refusals and loosening are cases: no login (connection closed, exit 56), `-u` over `kafka://` (no SASL mechanism offered, exit 67), `--allow-plaintext-auth` (accepted; wrong password 67), `kafkas://` with `--cacert` (accepted; exit 60 without it).
4. Cross-protocol: `bus` listens on `kafka://` and `mqtt5://`; MQTT 5 `shop/orders` is Kafka `shop.orders` both ways. The Kafka-to-MQTT direction needs `--topic "shop/#;retain"` (curl's MQTT subscriber arrives after the produce) and `--mqtt5-disconnect-after-retained` (so it ends); both explained.
5. Fixed: lint wanted `-u` in backticks on its own; the account sentence was reworded. "Only letters, digits, dots, underscores and hyphens" was cut to "a character Kafka does not allow, such as a space", since only the space is cased and the Kafka protocol guide does not list the characters.
6. Truth, for the author: `surl --aihelp kafka` says SCRAM-SHA-256 and -512 log in as an account, but over `kafka://` without `--allow-plaintext-auth` surl answers SaslHandshake with error 33 and an empty mechanism list, SCRAM included, though SCRAM does not expose the password. The recipe says only what curl sees ("offers no mechanism"); curl speaks only PLAIN, so the recipe is right either way.
7. Updated 2026-11-19 for Surl 763754785 and Curl b9ca725077: under Surl's new Kafka SCRAM ADR, `kafka://` with an account enables SCRAM-SHA-256 and SCRAM-SHA-512 (they never send the password) and PLAIN only with `--allow-plaintext-auth`; curl speaks only PLAIN and now names what the broker enables (exit 67 unchanged). `clear-password-refused` and the Discussion say so; finding 6 is settled.
