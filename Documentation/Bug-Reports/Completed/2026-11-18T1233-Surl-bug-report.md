<!-- Copyright (c) 2026 Stewart Rogers. -->
# Surl bug report, 2026-11-18 12:33 (UTC-7)

From the *Surly* cookbook (`Z:\repos\Surly\Book`), found while writing and verifying Part 1. Each finding was reproduced again just before this report was written.

**Build tested:** Surl at commit `3c5b39474` (2026-11-17), built `dotnet build Application/Surl.Console -c Release` and `Application/Surl.Mcp`; packages packed `-p:BundledProjectReferencesPrivateAssets=all` as `release.yml` does. `surl 1.0.0 (win-x64)`, Windows 11, .NET SDK 11.0.100.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | Console, logging | `-s` does not silence the exit-1 "Protocol not supported" message |
| 2 | Medium | DICT server | DEFINE matches a headword case-insensitively on Windows; MATCH exact does not; help says case matters |
| 3 | Low | `Surl.Api` README | `SurlExitCode`'s namespace and the `(58) ` prefix on `SurlServerStartException.Lines` are undocumented |

---

## 1. `-s` does not silence the exit-1 message

**Repro**
```
surl -s nosuch://127.0.0.1:0/
surl --log-level none nosuch://127.0.0.1:0/
```

**Observed:** both print `surl: (1) Protocol "nosuch" not supported` on stderr and exit 1. The same happens for `file://`.

**Expected:** nothing on stderr, exit 1. `surl --aihelp exit-codes` says: "With `-s` and without `-S`, a failure found once the command line is read writes nothing, and only the exit code says what failed; a refused command line is always written." Upstream `curl -s` behaves that way. The other start failures do follow the rule. For example, `surl -s https://127.0.0.1:0/` prints nothing and exits 58, but without `-s` it prints `surl: (58) https://127.0.0.1:0/ needs a certificate: ...`. Exits 23, 45 and 124 are silenced too.

**Note:** if an unsupported scheme counts as "a refused command line", then the code is right and `--aihelp exit-codes` should say that exit 1 is always written.

## 2. DICT: DEFINE and MATCH disagree on case (Windows)

**Repro.** The folder `words/` holds one file, `surly`, containing `bad-tempered and unfriendly\n`:
```
surl --directory words dict://127.0.0.1:2628/
curl -s dict://127.0.0.1:2628/d:SURLY
curl -s dict://127.0.0.1:2628/m:SURLY:*:exact
```

**Observed:**
- `d:SURLY` returns `150 1 definitions retrieved`, then `151 "SURLY" surl "Files served by surl"` and the definition. The 151 line echoes the client's spelling, not the stored headword `surly`.
- `m:SURLY:*:exact` returns `552 no match`.
- `m:surly:*:exact` returns `152 1 matches found` with `surl "surly"`.

**Expected:** the two commands should agree. `surl --aihelp dict` says "Headwords are compared case and all", so `d:SURLY` should be `552 no match`. DEFINE probably opens the file by name, and the Windows file system ignores case. Linux and macOS (case-sensitive) will likely say `552` for both, so the server answers differently on different platforms.

## 3. `Surl.Api` README gaps (documentation)

- `SurlExitCode` is in namespace `Surl.Protocol.Abstractions`, not `Surl.Api`. The README uses `SurlExitCode.Ok` in its first example but never names the namespace, so a reader's program does not compile until they find it.
- Each item of `SurlServerStartException.Lines` starts with the exit code in parentheses, for example `(58) https://127.0.0.1:0/ needs a certificate ...`. The README describes the lines as "the lines `surl` writes after `surl: `" and does not show that prefix.

---

**Not Surl bugs:** an earlier packaging problem (NU1101 for `*.UnitLibrary`) came from packing without `-p:BundledProjectReferencesPrivateAssets=all`, which was our mistake, not Surl's.
