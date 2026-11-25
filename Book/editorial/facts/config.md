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
  Since Surl a423807a5d (BL-1439) ListenUrls may be empty when a ConfigFile has url lines: the
  server listens on those (before, `ArgumentException` `ListenUrls names no listen URL. (Parameter
  'options')`). File url lines are ADDED after ListenUrls. `BoundUrls` are `Uri`s.
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

## Layering: which setting wins (surl-split-options-across-config-files, Surl 9ad60d80c, 2026-11-20, windows)
- Options are read in command-line order, `-K` at its place; a single-value option read last wins (`-K base.conf -K test.conf` -> test's `max-filesize`; swapped -> base's; `--max-filesize 5` after the files beats both; before the files it loses; `--directory` and `--no-allow-uploads` after the files win likewise). `user =` lines ADD accounts across files; any account makes every HTTP request need a login (401). HTTP upload over the limit is 413 (FTP: curl exit 70); uploads off HTTP 405 (FTP 550/25). An 8-byte upload with limit 10 is 201 (or 204 when the name exists: use a new name per case).
- Unknown option in the second file: `surl: bad.conf:2: option --max-filesise: is unknown` exit 2 (names file and line).
- Surl.Api: `ConfigFiles` (-K, in order) then properties are read BEFORE the files (`MaxFileSize = 5` loses to a file's 10), `AdditionalArguments` AFTER (win). Servers need `--directory` per surl: two share nothing; a dotnet run left in the same folder still locks `site` (exit 124 `DataDirectoryInUse`).
- Agent: surl-mcp has no -K; layers are argument lists concatenated, later wins like the console. A refusal of `-K`/`--max-filesise` is a tool error (see the recipe's expected text).

- Decided by Surl (2026-11-20): surl leaves its lock file after a clean stop on purpose (ADR-0031 decision 7); deleting it would race a second process. Refusals found while reading the command line have no "(2)" prefix and add a try-help line; refusals found at start get "(2)" (ADR-0101 decision 5). surl-mcp messages are limited to 4 MiB (ADR-0103): split large seeds across `put_file` calls. `Files.DeleteAsync` leaves an emptied folder, as a client delete would.

## --random-seed (surl-repeat-a-failure-with-a-fixed-seed and twins, Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- `--random-seed <n>` (0 to 9223372036854775807; Surl.Api `RandomSeed` long?; surl-mcp takes it in `arguments`) seeds identifiers that grant nothing; loosens nothing, no warning. Without it nothing is seeded and surl prints no seed: a run that wants to repeat must choose one and log it.
- Draws are per exchange: seed plus exchange number (1, 2, ... in connection order; a readiness probe that connects is exchange 1) give the same bytes on every platform (SHA-256 counter, Surl `SeededIdentifierRandom`). Requests on one connection continue that exchange's draws. Some extended servers draw per server name instead (Kafka cluster ID, NATS server_id, TLS-SRP).
- Who draws (Surl source): S3 `UploadId` and `x-amz-request-id`, Azure request IDs, OCI upload session IDs, SignalR connection IDs, AMQP 1.0, Kafka, MQTT 5, NATS, STOMP, CoAP, XMPP identifiers. Plain HTTP, FTP, mail and the other upstream-curl schemes draw nothing, so a seed changes nothing there. Keys, nonces, challenges, salts and tokens never come from it: two HTTP servers with the same seed send different Digest nonces.
- Measured (S3 over `--s3-plaintext`, the POST `?uploads` as exchange 1): seed 1 -> UploadId `a3f0bca96a94bddcbbdee7353d87db57`, seed 3 -> `5639b40740dd9a241e86ed60a3306b6a` (exchange 2: `bb73347a1372eccb4997db86ac06e8c4`), seed 7 -> `8e70afa3ea491a08d18753912a4e07af`, seed 8 -> `fa2684c73821955509950d386451c264`. The same on the console, Surl.Api and surl-mcp.
- Refusals: `--random-seed soon` (or -1) exit 2 `surl: option --random-seed: expected a proper numerical parameter`; surl-mcp tool error `exit_code` 2, `lines[0]` without `surl: `; Surl.Api `RandomSeed = -1` -> `SurlServerStartException`.

## One server per parallel worker (surl-run-tests-in-parallel-without-clashes and twins, 2026-11-22, windows)
- A second surl on a served directory: exit 124 `surl: (124) Directory <dir> is in use by another surl process`; Surl.Api `DataDirectoryInUse`; surl-mcp tool error `exit_code` 124 `exit_code_name` DataDirectoryInUse. A held port: 45 BindFailed everywhere.
- Four surl-mcp servers started by parallel `start_server` calls on one McpClient, and parallel curl-mcp `curl` calls, run side by side with no clash; each in-memory store is its own.
- Git Bash: a surl backgrounded by one `sh` can be stopped by `kill <pid>` from another `sh` after the first has exited (pid kept in a file), with `kill -0` polled until it has gone (GitHub Actions stop step, surl-run-a-fixture-in-github-actions).

## Beside an ASP.NET Core app; reset (surl-run-beside-a-web-application-factory, surl-reset-keys-directory-and-zones; Surl 5da5b3eaf, 2026-11-22, windows)
- `Microsoft.AspNetCore.Mvc.Testing@*` IS usable from a file-based program, but only with three workarounds, and it is not a pinned package (lint refuses it): `#:sdk Microsoft.NET.Sdk.Web`; the program is its own entry point, so `WebApplicationFactory<Program>` re-runs the test unless a static flag (`public partial class Program { public static bool UnderFactory; }`) sends the second run down the app branch; and the content root cannot be found (`Solution root could not be located`), fixed by writing `MvcTestingAppManifest.json` (`{"<assembly FullName>": "<cwd>"}`, built with `JsonObject` since reflection serialisation is off) in the working directory before `CreateClient`. The recipe uses a plain `WebApplication` instead (`#:sdk Microsoft.NET.Sdk.Web` only, no extra package): `builder.Logging.ClearProviders()` (console logs go to stdout), `UseUrls("http://127.0.0.1:0")`, `AddInMemoryCollection(settings)` after the defaults, `await web.StartAsync()`, address from `IServer.Features.Get<IServerAddressesFeature>().Addresses`.
- `SnapshotAsync()` / `RestoreAsync(snapshot)` cover keys, directory and zones as well as files, mail and queues, and a snapshot can be restored again and again. `ResetAsync()` empties keys, the directory (`ExportLdifAsync` is only `version: 1`) and zones (`ListAsync` empty; the same apex can then be added again): their contents come from calls, not options, so a reset leaves them empty. After a reset an LDAP search of the old root is curl 39, a DNS lookup REFUSED, `DBSIZE` 0.

## Your own protocol server in Surl.Api (surl-serve-your-own-protocol-in-process; Surl v0.1.8 9f8005dff / Curl 2145a49eae, 2026-11-25, windows)
- `options.ProtocolServers.Add(new SurlProtocolServer("echo", services => new EchoServer()))`, `ListenUrls = { "echo://127.0.0.1:0" }`; the server implements `Surl.Protocol.Abstractions.IConnectionProtocolServer` (`Schemes`, `ServeAsync(IConnection, ExchangeContext)`; `IConnection.ReadAsync/WriteAsync`). Surl records the exchange itself (do NOT call `context.Log.BytesReceived/Sent`: it doubles the log); `server.Exchanges[0].Scheme` is `echo`; `ExchangeEndedEvent` (its `Scheme` prints empty, but a `SurlEventQuery { Scheme = "echo" }` wait finds it). Accept-stage faults (`DropConnection() with { Listener = "echo://127.0.0.1:0", Times = 1 }`, `Delay`) and `IdleTimeout` apply with no server code; `AdditionalArguments` `--max-line 16` arrives as `context.Limits.MaxLineBytes` (the server enforces it). Command-stage rules come back from `context.Faults.FindAsync(new FaultPoint(FaultStage.Command) { Command = ... })` for the server to act on (no fault_applied event). Scheme `http`: `ArgumentException` `ProtocolServers may only add a scheme: http is served by Surl or given twice.`
