<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mcp-call-a-tool
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 71807f05f), Curl port 8.22.0 (Curl commit 82913be53d): 15 of 15 cases pass on windows.
## Findings
1. Subject: this is the Curl port's `mcp://`, `mcps://` and `mcp+stdio://` client against `surl`'s `mcp` scheme, from the console; not `surl-mcp`/`curl-mcp` and not the C# SDK. `surl`'s MCP scheme offers `read_file` and `echo` by default, `list_directory` under `--list-directories` and `write_file` under `--allow-uploads`; all four are cased (`list-tools`, `more-tools`).
2. Fixed: the call bodies are fixtures (`-d @echo.json`), so the commands quote the same in cmd.exe, PowerShell and sh; the Solution shows `echo.json`, since the generated block does not print fixtures.
3. Fixed: `mcps` uses `--self-signed` and `curl -k`, not an `openssl` setup line, because the setup line would otherwise be printed first in a Solution that does not use it. The refusal without a certificate (exit 58) and without `-k` (exit 60) are cased, and the Discussion says what `--self-signed` gives up.
4. Login: `surl`'s MCP endpoint takes only OAuth Bearer tokens it issued itself; `-u` (HTTP Basic) is answered `400` (exit 8), by design in `McpHttpRequestAnswerer` ("Another Authorization scheme is 400"). The recipe shows the refusal (67), the loosening (`--allow-anonymous`), an unknown token (67) and the Basic refusal (8). A full login (get a token from `surl`'s `/token` with `client_credentials`, then `--oauth2-bearer`) cannot be one case, because the token must be copied from one command into the next; it would make a recipe of its own. Needs the author.
5. `over-stdio` and `more-tools` rely on `surl` being on `PATH` for the `curl` child process, as it is for a reader. verify.py puts the pinned `curl` first but finds `surl` through `SURLY_SURL` or `PATH`; on a machine where `surl` is only in `SURLY_SURL`, these two cases fail. Worth a note in build/README.md (lead).
6. Rubric 4: "`curl` opens the session (`initialize`, then `initialized`)" and "does not fetch a token by itself" rest on the Curl conformance documents through <<curl-protocols>>, not on a case.
7. The JSON-RPC error text (`-32602: Invalid params: unknown tool ...`) is surl's; the case checks it with `stderr-contains`.
