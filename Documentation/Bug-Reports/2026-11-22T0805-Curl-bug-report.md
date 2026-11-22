<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-22 (CI on Linux, batch 10)

One finding from the *Surly* cookbook's CI on `ubuntu-latest` (Ubuntu 26.04, OpenSSL), Curl `c84035f797`, Surl `5da5b3eaf`. Windows and macOS pass the same commands.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | NATS over TLS (Linux) | `curl ... natss://` fails the TLS handshake with exit 35, `error:0A00010B:SSL routines::wrong version number`, before any login |

## Repro

On Linux:

```
surl --cert server.pem --key server.key --user alice:s3cret natss://127.0.0.1:4443/
curl --cacert server.pem -u alice:wrong -d hi natss://127.0.0.1:4443/orders; echo "exit $?"
```

Observed: exit 35, `TLS connect error: ... wrong version number`. Expected (as on Windows and macOS): exit 67, `NATS server refused the login: Authorization Violation`. A NATS server sends its `INFO` line in plain text and the client upgrades to TLS after reading it (unless both sides use TLS-first); the "wrong version number" suggests the OpenSSL path starts the handshake at once and reads that plain `INFO` as a TLS record. Recipes: `nats-log-in-with-a-token` and its agent twin (their natss refusal cases are now marked Windows and macOS).
