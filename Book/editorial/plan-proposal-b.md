# Plan proposal B: secure servers, advanced, new protocols

300 rows: 90 console, 105 C#, 105 MCP. 88 triplets; singles: 2 console, 17 C# (9 Libcurl, 8 Surl.Api), 17 MCP.

## Chapters

Reused: 03/01-04, 04/01-02, 04/04, 05/01-04.

New:
- 03/06 Managing Accounts: one account file for every protocol, LDAP-backed logins, groups, rotation.
- 03/07 Logging In to Mail: CRAM-MD5, OAuth tokens, APOP, mailboxes limited to some protocols.
- 04/05 Testing How a Client Copes: delays, drops, try-later replies, a full disk, partial downloads, retries.
- 04/06 Hooks and Custom Servers: overrides, event callbacks, failing hooks, your own protocol (Surl.Api).
- 04/07 Porting C Programs with Libcurl: multi, share, mime, headers, getinfo, callbacks, the URL API.
- 04/08 Proxies and Tunnels: HTTP, HTTPS and SOCKS proxies, proxy logins, a failed CONNECT.
- 04/09 Diagnosing from Recorded Exchanges: an agent finds the slow path, the bad request, the fault.
- 04/10 Checking What a Download Delivered: checksums, segments, record and replay, HAR.
- 05/05 One Bus, Many Protocols: STOMP to MQTT, Kafka to MQTT 5, topic maps.
- 05/06 Securing the New Protocols: TLS and logins for gRPC, SignalR, Socket.IO, MCP, Redis, STOMP, AMQP, CoAP.
- 05/07 Reading Back What Clients Left: queues, logs, retained messages, keys, objects.

## Gaps for Surl and Curl

1. `surl --aihelp control` says `--control` is not served yet; the Surl.Api README shows `ControlUrl` answering. Until it is served, the console cannot change accounts or faults at run time.
2. The auth topic says nothing uses `--group`. The messages topic accepts `--topic-access @group`. One of them is wrong.
3. curl has no way to pick the Digest SHA-256 algorithm, or to log in with SCRAM for mail or Kafka.
4. Left out: Kerberos; not yet probed: MCP for the extended protocols, `abfs`, and DNS truncation.
