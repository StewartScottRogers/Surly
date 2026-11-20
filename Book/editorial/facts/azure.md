<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# azure facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-19, windows.

## Listen URLs and readiness
`az://127.0.0.1:{{port}}/` is TLS from the first byte, TCP, `ready: port`. Needs `--cert`/`--key`: without, exit 58, `surl: (58) az://127.0.0.1:0/ needs a certificate...`. Can share a store with an `https://` listener in the same surl: the blob `photos/note.txt` is `/photos/note.txt` there, same token.

## Server options that matter
- `--directory store`: each top-level folder a container, the path under it the blob name; files already there are blobs.
- `--user devstoreaccount1:<base64 key>`: a storage account; its password is the Shared Key. Path style (`/<account>/<container>/<blob>`) strips the first segment only when it names an account surl holds; otherwise it is the container (404 ContainerNotFound). Kept on purpose (Surl ADR-0109; cite the Surl README).
- `--user :<token>`: an empty name makes the password a Bearer token.
- `--allow-uploads`: without it a put is 403 AuthorizationPermissionMismatch; gets still served.
- `--allow-anonymous` (loosens, warns): gets with no token served; a put with no token is still 401 NoAuthenticationInformation.
- Listing a container needs `--list-directories` (kept on purpose, ADR-0109); without it, 403 AuthorizationPermissionMismatch.

## Secure by default
No token: 401 NoAuthenticationInformation. Unknown token: 401 InvalidAuthenticationInfo. Wrong Shared Key: 403 AuthenticationFailed. Missing blob: 404 BlobNotFound. For each, curl writes nothing on stdout, exits 22, stderr `Azure Storage returned error <status>: <Code>`.

## Client: curl
- `az://<IP or localhost>:<port>/<account>/<container>/<blob>` is emulator path style; curl speaks HTTPS; `--cacert server.pem`.
- `--oauth2-bearer <token>` sends Bearer. `-u <account>:<base64 key>` signs with Shared Key (new in b9ca725077; before, it sent nothing and got 401). A key that is not base64: exit 67, `The account key is not base64.`
- `-T file` puts; a URL with no `-T` gets to stdout, so one command can put then get.

## MCP
Not probed.

## Gotchas
- probe.py has no setup step: pass a certificate through PROBE_FILES (newlines escaped) or case it in recipe.yml and run verify.
- `--az-plaintext`, `abfs`/`abfss`: not probed.

## Recipes
az-put-and-get-a-blob
