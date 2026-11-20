# Plan proposal C: Part 6, Scenarios

**Part 6, Scenarios** (`06-scenarios`): end-to-end use cases across several protocols or steps as tests, CI, operators and agents use them: seed, run, inject a fault, check the server's record.

300 rows: 87 console, 107 csharp, 106 mcp. 70 triplets (210 rows) and 90 singles (17 console, 37 csharp, 36 mcp). Twins whose suffixed title would reach eight words keep the console title.

## Chapters
1. Testing an App That Sends Mail: recipients, aliases, attachments, temporary refusals.
2. Standing Up a Test Environment: one fixture, bound ports, config files.
3. Seeding and Resetting Between Tests: seeds, snapshots, resets, kept state.
4. Logins and Tokens End to End: token exchange, expiry, rotation, SigV4.
5. Moving Files Through a Pipeline: FTP, HTTP and S3 hops, resume, ETags, full disk.
6. Retries, Back-off and Faults: transient and permanent failures, budgets, chaos.
7. Moving a Client to Another Protocol: FTP to SFTP, POP3 to IMAP, Basic to Digest.
8. Messages Between Services: MQTT, AMQP, Kafka, NATS, STOMP, Redis.
9. Diagnosing a Failing Client: an agent reading exchanges and events.
10. Running in CI and Scripts: shell, Pester, Actions, parallel workers.
11. Object Stores and Registries: S3, Azure, GCS, OCI.
12. Directories, Names and Caches: LDAP logins, test zones, caches.
13. Proxies, Certificates and Host Keys: proxies, a test CA, client certificates, SOCKS.

## Wanted, not yet possible
- **Control endpoint:** `surl --aihelp control` says this build does not serve it; the `Surl.Api` README says it does. No row uses it.
- **surl-mcp:** has no `version` tool.
- **Console:** has no reset or snapshot without the control endpoint. The console rows copy a clean data directory instead.
- **curl-mcp `transfer`:** has no retry or resume fields; the agent rows use the `curl` tool.
- **Not yet checked:** whether S3 clock skew follows `TimeProvider`, and whether an override may answer 401.
