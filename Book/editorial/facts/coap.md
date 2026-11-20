<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# coap facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-19, windows.

## Listen URLs and readiness
`coap://`, `coaps://` (DTLS 1.2); UDP only, `ready: none`. Start the main server last and give the first case `curl --retry 5 --retry-all-errors --retry-delay 1`: on Windows, curl to an unbound UDP port exits 7 at once; on Linux and macOS it keeps retransmitting (over 60 s in CI), so a nothing-listening case is `platforms: [windows]` (`platforms.md`).

## Server options that matter
- `--directory site`: files are resources.
- `--user alice:secret`: with any account, plain `coap` reads are 4.01 (NoSec carries no login; `-u` changes nothing). On `coaps` an account is a pre-shared key.
- `--allow-anonymous` (loosens, warns): serves reads despite the account.
- `--self-signed` or `--cert`/`--key` for coaps. None of these and no account: exit 58, `surl: (58) coaps://127.0.0.1:0/ needs a certificate or an account: give --cert <file>, --self-signed for a throwaway one, or --user <name:key> for a pre-shared key` (wording new in 763754785).

## Secure by default
An account on plain coap: `CoAP/1.0 4.01`, curl exit 0 (22 with `-f`).

## Client: curl
- Confirmable GET, payload to stdout. `-i` prints `CoAP/1.0 2.05`, a blank line, the payload. 4.04: exit 0, nothing written; with `-f`, exit 22, `The requested URL returned error: 4.04`.
- coaps: certificate mode only, no pre-shared key (`-u` against a PSK-only surl exits 35, re-probed on b9ca725077). `-k` accepts a throwaway certificate; without it, exit 60. `--cacert` also works.

## MCP
Not probed.

## Gotchas
- `%{response_code}` is 000 after CoAP; use `-i` or `-f`. `-I` sends a GET. Plain coap to a coaps port hangs over 20 s.

## Recipes
coap-get-a-resource
