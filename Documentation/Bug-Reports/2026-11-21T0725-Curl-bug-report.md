<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-21 (CI on macOS, batches 8-9)

One finding from the *Surly* cookbook's CI on `macos-latest`, Surl `9ad60d80c`, Curl `7d167fa10a`. It is sent to both Surl and Curl, since which side fails is not yet known. On Windows and Linux the same commands pass.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | Unix sockets (macOS) | A fetch over a Unix socket fails: curl exits 55 (send failure), curl-mcp fails `CurlExit`, and a Surl.Api + Curl.Api program aborts (134) |

## Repro

On macOS, in a folder holding `site/hello.txt`:

```
surl --directory site --unix-socket surl.sock http://localhost:8080/
curl -s --unix-socket surl.sock http://localhost:8080/hello.txt; echo "exit $?"
```

Observed: surl prints `Listening on http://localhost:8080/ over unix://surl.sock`; curl prints nothing and exits 55. Expected: `hello from surl`, exit 0, as on Windows and Linux. The socket path is relative and short, so it is not the 104-byte `sun_path` limit. Recipes: `http-listen-on-a-unix-socket` and its C# and agent twins (their macOS cases now expect 55).
