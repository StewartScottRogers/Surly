<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# oci facts
Checked against: surl 1.0.0 (Surl 5da5b3eaf), curl (Curl c84035f797), 2026-11-22, windows. Written for `oci-check-an-image-landed`; see also `oci-push-and-pull-an-image` (the console recipe, its own fixtures).

## Listen URLs and readiness
`oci://127.0.0.1:{{port}}/` is TLS from the first byte, TCP, `ready: port`. Needs `--cert`/`--key`: without, exit 58, `surl: (58) oci://127.0.0.1:0/ needs a certificate...`. Can share a store with an `https://` listener in the same surl.

## Server options that matter
- `--directory store`: each repository (`demo/app`) is a folder in OCI image layout: `oci-layout`, `index.json` (created by the first manifest push) and `blobs/sha256/<hex>`; the manifest is a blob like the rest. With no `--directory` the store is in memory and surl-mcp's `list_files`/`read_file` see the same layout.
- `--user alice:s3cret`: the account; curl gets a token from the registry's token endpoint with it.
- `--allow-uploads`: without it a push is 403 DENIED (curl exit 67). `--allow-anonymous` loosens pulls only; a push with no login is still 403 DENIED.

## Secure by default
No `-u` or wrong password: curl exit 67, `The token endpoint answered 401 without a token.` Unknown tag: 404 MANIFEST_UNKNOWN, exit 22. A manifest naming a blob the registry lacks: 400 MANIFEST_BLOB_UNKNOWN, exit **22** (not 67), nothing recorded (no `index.json` if it was the first manifest). Uploads off: exit 67, `403 (DENIED`.

## Client: curl
- `-T file oci://H:P/demo/app@sha256:<hex>` pushes a blob; `-T manifest.json oci://H:P/demo/app:1.0` pushes the manifest under the tag; no `-T` pulls (by digest or tag). Several in one command run in order, so one command is a whole push. `--cacert server.pem`, `-u alice:s3cret`.
- curl hashes the file before sending: a file whose SHA-256 is not the URL's digest is exit 25, `The upload's digest sha256:<mine> does not match the URL's sha256:<url>.`; nothing is stored.
- `index.json` after one tagged push: `{"schemaVersion":2,"mediaType":"application/vnd.oci.image.index.v1+json","manifests":[{"mediaType":"...manifest.v1+json","digest":"sha256:<manifest sha>","size":N,"annotations":{"org.opencontainers.image.ref.name":"1.0"}}]}`. The manifest digest is the SHA-256 of the pushed manifest file (479 bytes for the two-layer fixture of the recipe).

## MCP
- surl-mcp `start_server` `arguments` `--cert server.pem --key server.key --user alice:s3cret --allow-uploads`; `list_files` `path` `/demo/app/blobs/sha256` answers `entries[] {name, kind, byte_count}` (`kind` is `file` or `folder`; a missing folder is a tool error); `read_file` `/demo/app/index.json` answers `text`, a tool error when there is none. `put_file`/`delete_file` change the store with no login (used to show a lost or altered layer).
- curl-mcp `curl` tool with `-sS --cacert server.pem -u alice:s3cret -T layer1.txt oci://H:P/demo/app@sha256:... -T manifest.json oci://H:P/demo/app:1.0`: one call pushes the image; a failure is a tool error with curl's message in `standardError`; `-T` reads from `CURL_MCP_ROOT`. A pull by tag prints the manifest text.

## Gotchas
- Not probed: Curl.Api (C#) push to `oci://`; Curl.Api body upload to `az://` and `oci://` (the lead says they now work). Not probed: Docker-style clients.

## Recipes
oci-push-and-pull-an-image, oci-check-an-image-landed
