<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# config facts (surl -K, Listening on lines; any protocol)
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), Surl.Api 1.0.0, surl-mcp 1.0.0, 2026-11-19, windows.

## Listen URLs and readiness
- Port 0 listen URLs: surl writes `Listening on <scheme>://<host>:<port>/` to stdout, one per URL, in
  command-line order, once ALL have bound (none if any bind fails). `-v` logs go to stderr only.
- No servers: block can read them; recipes start surl from a `sh` script fixture, poll the stdout file for
  the lines, and `kill -0`/`wait` to catch a surl that exits first. verify runs `sh x.sh` through cmd.exe
  on Windows (Git's sh on PATH). Under `set -e`, write the EXIT trap as `kill $pid 2>/dev/null || true`,
  or a dead pid turns every exit code into 1.
- Fixtures are not printed by the build; show a script or config file with `include::<fixture>[]`
  inside a source block in recipe.adoc (the build writes fixtures beside it).

## Server options that matter
- `-K file` reads curl config syntax in place; later options override (`--no-allow-plaintext-auth`,
  `--no-allow-uploads`, a second `--directory` wins). `url = ...` lines are listen URLs.
- Refusals: unknown option `surl: f.conf:2: option --x: is unknown` exit 2; `directory = a b`
  -> `text follows the value of --directory; quote a value that holds a space` exit 2; unreadable file
  `surl: (37) Could not read config file f.conf`. Nothing listens.
- Quirk: a `#` comment after a valueless flag (`allow-uploads  # x`) is refused like text after a value
  (exit 2). After a value (`mail-domain = example.com  # x`) it is a comment.
- Loosenings read from a file still warn on stderr at start.

## Secure by default
- `--directory` is locked: a second surl on it exits 124 `... is in use by another surl process`.
- With any `--user` account, every HTTP request needs a login (401); a `--mailbox` alone does not.

## MCP
- start_server refuses `--config`/`-K`: tool error `refused: -K/--config is not accepted: a config file
  could name paths this host never checks`. Paths in `arguments` are confined: `--directory ../x` ->
  `refused: --directory names ../x, which is not inside the root`. Unknown option -> tool error with
  surl's lines. `listen_urls` is required (minItems 1).
- list_servers lists only its own host's servers; a host's servers end when the host ends.

## .NET
- `SurlServerOptions.ConfigFiles` = `-K`; `AdditionalArguments` are read after the files (override).
  ListenUrls must be non-empty even if the file has url lines: else `ArgumentException`
  `ListenUrls names no listen URL. (Parameter 'options')`. File url lines are ADDED after ListenUrls.
- Refusal: `SurlServerStartException` `ExitCode` FailedInit (2) / CouldNotReadFile (37), `Lines[0]`
  is surl's line without `surl: `.
- Bind clash: `SurlServerStartException` BindFailed (45), `(45) Could not bind <url>: Address already in use`.

## Recipes
surl-hand-bound-ports-to-the-app (+ twins), surl-run-a-fixture-from-a-config-file (+ twins),
surl-clean-up-leftover-servers.

## Surl.Api AdditionalArguments (surl-pass-any-option-through-in-process, Surl 9ad60d80c, 2026-11-20)
- Options with no property go in `AdditionalArguments`, one string per argument (`"--max-request-head", "1000"`): a 2000-character header is then 431 Request Header Fields Too Large, 200 without. Read after the properties: `--no-allow-uploads` there overrides `AllowUploads = true` (PUT 201 -> 405).
- Host options throw `ArgumentException` at `StartAsync`: `--log-file, --trace and --trace-ascii belong to a host; write the log to SurlServerOptions.Log. (Parameter 'options')`; `--help` (and `--aihelp`, `--manual`, `--version`): `--help, --aihelp, --manual and --version start no server.` surl's own refusals: `SurlServerStartException` FailedInit, `Lines[0]` `option --bogus: is unknown` / `option --max-request-head: expected a proper numerical parameter`.
- `Mailboxes` entry `;protocols=...,redis` with `AreExtensionsTurnedOn = false`: FailedInit `option --mailbox: unknown scheme redis`. A `redis://` listen URL with extensions off: `UnsupportedProtocol`, `(1) Protocol "redis" not supported`. `AreExtensionsTurnedOn` null with no `SURL_EXTENSIONS` set served redis on Windows.
- In-process `StartAsync` timing: ~0.4 s for the first server in a process, ~5 ms after; `ResetAsync` 0-20 ms; seeding 200 files with `Files.PutAsync` ~20 ms, `SnapshotAsync`/`RestoreAsync` ~15 ms.
