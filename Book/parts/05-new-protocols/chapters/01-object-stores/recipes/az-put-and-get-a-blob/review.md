<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: az-put-and-get-a-blob

Verdict: accept with fixes

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 14 of 14 cases pass on windows.

## Findings

1. Decision: the Solution logs in with a Bearer token (`--user :t0ken-abc`, `--oauth2-bearer`), because the Curl port's `az` client does not sign with Shared Key (its library notes say so); a case shows `-u` sends nothing and is answered 401.
2. `--user devstoreaccount1:c2VjcmV0` is needed although its key is never used: `surl` strips the path-style account segment only when it names an account it holds. Without it the segment is read as the container (`404 ContainerNotFound`, the `noaccount` server). This is surprising to a reader and is in the Discussion; the author may want to ask Surl whether a bearer-only server should accept any account segment (see the report).
3. The `https` listener in the Solution is there for the shared-store claim; the Discussion says so.
4. Left out: listing a container (`az://.../photos/` answered `403 AuthorizationPermissionMismatch` with `--allow-uploads` and the token; `--list-directories` is not listed among the azure-storage options), `--az-plaintext`, `abfs`/`abfss`.
5. Updated 2026-11-19 for Surl 763754785 and Curl b9ca725077: the Curl port's `az` client now signs with Shared Key from `-u <account>:<base64 key>`, so `no-shared-key` (401) became `shared-key` (served) and `wrong-key` (403 AuthenticationFailed); a key that is not base64 is refused by curl, exit 67 (probed, not claimed). The Solution keeps the Bearer token (finding 1 now a choice, not a limit). Path-style needing a named account is kept on purpose (Surl ADR-0109) and the Discussion says so, cited to the Surl README.
