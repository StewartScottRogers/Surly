<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: az-test-an-azure-upload-locally
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797: 12 of 12 cases pass on windows.
## Findings
1. The planned "plain-HTTP stand-in" is `--az-plaintext` with `--allow-plaintext-auth`; curl's own `az://` is TLS only (exit 35 against it), so the console client speaks the REST API over `http://` with `-H x-ms-version`, `x-ms-blob-type` and an `Authorization: Bearer` header. That is what an SDK pointed at an emulator sends.
2. Probed: without `--allow-plaintext-auth` the right token is 401 InvalidAuthenticationInfo; both headers are required (400 MissingRequiredHeader); the put answer carries Content-MD5.
3. Not claimed: Shared Key over plain HTTP, `abfs`, `--cert` on the same store.
