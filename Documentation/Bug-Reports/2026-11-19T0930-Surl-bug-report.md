<!-- Copyright (c) 2026 Stewart Rogers. -->
# Surl bug report, 2026-11-19 09:30 (UTC-7)

These findings come from writing and verifying Part 5 (New Protocols) of the *Surly* cookbook (`Z:\repos\Surly\Book`), which covers Surl's extended servers. The report comes on top of `2026-11-19T0642-Surl-bug-report.md`.

**Build tested:** Surl at commit `71807f05f` (2026-11-19 06:29): `surl 1.0.0 (win-x64)`, with `surl-mcp` built from it. Windows 11, .NET SDK 11.0.100. The client was the Curl port at `82913be53d`. Finding 1 was reproduced again just before this report was written; the others are proved by passing recipe cases or come from the chapter writers' exact commands.

**Fixed since the last report:** the repeated-token message now reads `a Bearer token (empty user name) is given twice` (2026-11-18T2025 #1). The cookbook recipe was updated to match.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | **High** | gRPC | With no account and no `--allow-anonymous`, the gRPC server answers anyone, which breaks secure by default |
| 2 | Medium | XMPP | `--allow-anonymous` never offers SASL ANONYMOUS, and logs a `-u` login in as an anonymous JID (ADR-0114 decision 3) |
| 3 | Medium | XMPP | `xmpp://` with an account and no certificate starts and sends empty `<stream:features/>`; ADR-0114 decision 2 says exit 58 |
| 4 | Medium | Kafka | Over `kafka://`, SaslHandshake offers no mechanism, not even SCRAM, which exposes no password |
| 5 | Low | Docs | `--aihelp amqp` omits CRAM-MD5, which `amqp://` offers; `--aihelp grpc` does not print the `.proto` file (ADR-0116); `--aihelp webtransport` implies sessions are refused without `--allow-anonymous`, but with no account they open; `--aihelp coap` says `coaps` needs "a certificate or an account" but exit 58 names only certificates |
| 6 | Low | Redis | A right password sent in clear without `--allow-plaintext-auth` gets `WRONGPASS`, the same reply as a wrong one |
| 7 | Low | `surl-mcp` | `set_key` answers the key as `"0/greeting"`, while `read_key` and `list_keys` say `"greeting"`; `set_key` and `add_dns_zone` succeed on a server with no Redis or DNS listener |
| 8 | Question | Azure | Path-style `az://host/devstoreaccount1/...` needs an account named `devstoreaccount1` to be recognised; a container listing with a valid token is 403 even with `--allow-uploads`, and no option is documented to allow it |

---

### 1. gRPC serves anonymous calls by default (high)
**Repro** (the cookbook's `echo.proto`; `surl.test.Echo/Echo` is surl's test service)
```
surl grpc://127.0.0.1:50051/
curl -s -d "{\"message\":\"hello\"}" --grpc-proto echo.proto grpc://127.0.0.1:50051/surl.test.Echo/Echo
```
**Observed:** exit 0 and the reply `{"message": "hello"}`. No login was asked for, and no `--allow-anonymous` was given.
**Expected:** a refusal, as the other extended servers give with no account. SignalR answers 401 and Socket.IO `Not authorized` in the same setup. `surl --aihelp grpc` presents `--allow-anonymous` as the way "for a test", which implies calls are refused without it. The cookbook keeps `--allow-anonymous` in its gRPC Solution, and states nothing about the default.

### 2. XMPP and `--allow-anonymous`
`surl --allow-anonymous ... xmpp://127.0.0.1:5222/`, then `curl -d hi xmpp://127.0.0.1:5222/127.0.0.1`, gives `(67) XMPP server does not offer SASL ANONYMOUS`. The `-v` log shows only `PLAIN` offered, before and after STARTTLS. Under `--allow-anonymous`, a `-u alice:s3cret` login is bound as `anon-<hex>@127.0.0.1`, not as alice. ADR-0114 decision 3 says ANONYMOUS should be offered.

### 3. XMPP without a certificate
`surl --user alice:s3cret xmpp://127.0.0.1:0/` starts, and sends an empty `<stream:features/>` (no STARTTLS, no SASL), so no client can log in. ADR-0114 decision 2 says the start should fail with exit 58, as `https` without a certificate does.

### 4. Kafka SASL over plain `kafka://`
`surl --user alice:s3cret -v kafka://127.0.0.1:9092/` with `curl -u alice:s3cret kafka://127.0.0.1:9092/orders`: the SaslHandshake answer is error 33 with an empty mechanism list. `surl --aihelp kafka` says SCRAM-SHA-256 and SCRAM-SHA-512 log in. SCRAM never sends the password, so like CRAM-MD5 on the mail servers it could be offered without TLS. This may be intended policy; if so, the help should say so.

### 5 to 8
These are as summarised in the table, with the commands in the cookbook recipes `amqp-send-and-receive`, `grpc-call-a-method`, `webtransport-open-a-stream`, `coap-get-a-resource`, `redis-set-and-get-a-key`, `redis-seed-keys-from-an-agent` and `az-put-and-get-a-blob` (each `review.md` lists the exact call and answer).
