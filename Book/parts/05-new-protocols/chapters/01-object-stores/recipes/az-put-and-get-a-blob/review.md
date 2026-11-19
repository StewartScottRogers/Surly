<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: az-put-and-get-a-blob

Verdict: accept with fixes

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 71807f05f), Curl port 8.22.0 (Curl commit 82913be53d): 13 of 13 cases pass on windows.

## Findings

1. Decision: the Solution logs in with a Bearer token (`--user :t0ken-abc`, `--oauth2-bearer`), because the Curl port's `az` client does not sign with Shared Key (its library notes say so); a case shows `-u` sends nothing and is answered 401.
2. `--user devstoreaccount1:c2VjcmV0` is needed although its key is never used: `surl` strips the path-style account segment only when it names an account it holds. Without it the segment is read as the container (`404 ContainerNotFound`, the `noaccount` server). This is surprising to a reader and is in the Discussion; the author may want to ask Surl whether a bearer-only server should accept any account segment (see the report).
3. The `https` listener in the Solution is there for the shared-store claim; the Discussion says so.
4. Left out: listing a container (`az://.../photos/` answered `403 AuthorizationPermissionMismatch` with `--allow-uploads` and the token; `--list-directories` is not listed among the azure-storage options), `--az-plaintext`, `abfs`/`abfss`.
