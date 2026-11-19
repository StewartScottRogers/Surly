<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: gs-put-and-get-an-object

Verdict: accept with fixes

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 71807f05f), Curl port 8.22.0 (Curl commit 82913be53d): 12 of 12 cases pass on windows.

## Findings

1. Decision: `--connect-to ::127.0.0.1:8080` instead of `STORAGE_EMULATOR_HOST`, for the same reason as in s3-put-and-get-an-object: the environment variable cannot be set in a one-line case for both shells. `curl` sends `gs://` to `storage.googleapis.com:443`, path style, so the certificate names that host.
2. The shared store is shown across two object stores: an object put over `gs` is got over `s3` from the same `surl`, the one HMAC key pair signing both ways. The certificate also names `photos.s3.us-east-1.amazonaws.com` for that.
3. Left out: `--oauth2-bearer` (probed with `--user :t0ken-abc`: works) and `--gs-plaintext`, to keep the Solution to one login; candidates for a later recipe.
4. Note for the author: an unauthenticated `gs` request is `401 AuthenticationRequired` where `s3` answers `403 AccessDenied`; each matches its own service, so the Discussion states both as they are.
