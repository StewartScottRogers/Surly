<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-24 (batch 14 of the 1,000-recipe plan)

From writing and verifying batch 14 (60 recipes) of the *Surly* cookbook against Curl `2145a49eae`, Surl v0.1.5 `d64faa4bb`, Windows 11. Only findings not already filed.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | DNS over TLS | `--dns-over-tls` ignores `--cacert` and `-k`. It rejects a test server's certificate and exits 6. `--doh-url` honours `--cacert` |
| 2 | Medium | S3 | `-r 0-9` on `s3://` sends no `Range` header and returns the whole object, 200. `-C -` sends none either |
| 3 | Low | Exit codes | `--dns-transport tcp` against a port that speaks only TLS exits 2 (`Unable to read data...`). Expected 56 |
| 4 | Low | Exit codes, S3 | A connection dropped mid-transfer on `s3://` exits 2 with a .NET message (`forcibly closed...`). Expected 56, as over `https://` |
| 5 | Low | OCI | `-w "%header{docker-content-digest}"` prints nothing over `oci://`, though `-D -` shows the header. Over `https://` it works |

Re-run on 2026-11-24 against the same pins: 1, 3 and 5. Items 2 and 4 were seen the same day by the recipe writer on the same pins and not re-run separately.

## Repros

**1.** `surl -v --cert server.pem --key server.key --dns-transport tls dns://127.0.0.1:18853/`
- `curl -sS -m 10 --dns-over-tls 127.0.0.1:18853 --cacert server.pem http://api.example.test:18080/` gives exit 6 `Could not resolve host`. The same happens with `-k` in place of `--cacert`.
- surl's log for each attempt: `TLS handshake failed: Received an unexpected EOF or 0 bytes from the transport stream.` The client hangs up during the handshake, so it rejected the certificate.
- Expected: `--cacert` trusted and `-k` honoured, as for `--doh-url`.
- Recipes `dns-resolve-names-over-tls` (and its C# and agent versions) look the name up over `dns://` instead and then call with `--resolve`.

**2.** `surl --directory store --cert server.pem --key server.key --user AKIAIOSFODNN7EXAMPLE:s3cret s3://127.0.0.1:P/`, `store/media/big.bin` present, and a certificate naming `*.s3.us-east-1.amazonaws.com`.
- `curl -sS --cacert server.pem --connect-to ::127.0.0.1:P -u AKIAIOSFODNN7EXAMPLE:s3cret -r 0-9 -o part.bin -w "%{http_code} %{size_download}\n" s3://media/big.bin` prints `200` and the whole object's size.
- Expected: `206 10`.
- `-C -` on a partial file also sends no `Range`.
- The same request over `https://media.s3.us-east-1.amazonaws.com/big.bin` with `--aws-sigv4 aws:amz:us-east-1:s3` gives 206. Recipes `s3-resume-a-ranged-download` (all three versions) use that.

**3.** Same server as 1.
- `curl -sS -m 10 --dns-transport tcp dns://127.0.0.1:18853/api.example.test` gives exit 2: `curl: (2) Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host..`
- Expected: 56 (failure receiving network data). Exit 2 is a failed initialisation.

**4.** Same server as 2 plus `--fault "drop;after-bytes=1500000"`.
- `curl -sS --cacert server.pem --connect-to ::127.0.0.1:P -u AKIAIOSFODNN7EXAMPLE:s3cret -o big.bin s3://media/big.bin` gives exit 2 with the .NET text `...forcibly closed by the remote host`.
- Expected: 56, as the same drop gives over `https://` with `--aws-sigv4`.

**5.** `surl --directory store --cert server.pem --key server.key --user build:b1ld --allow-uploads oci://127.0.0.1:18500/`. Push a config blob, a layer and a manifest tagged `demo/app:1.0` with `-T` (exit 0).
- `curl -sS --cacert server.pem -u build:b1ld -o /dev/null -w "%header{docker-content-digest}" oci://127.0.0.1:18500/demo/app:1.0` prints nothing, exit 0.
- `curl -sS --cacert server.pem -u build:b1ld -o /dev/null -D - oci://127.0.0.1:18500/demo/app:1.0` shows `Docker-Content-Digest: sha256:5367...0e66`.
- Expected: `-w` prints `sha256:5367...0e66`.
- Recipe `oci-check-a-pushed-image-manifest` reads the header from `-D` instead.
