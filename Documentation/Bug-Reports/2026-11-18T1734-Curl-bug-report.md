<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-18 17:34 (UTC-7)

These findings come from writing and verifying Part 2 (Working Servers) of the *Surly* cookbook (`Z:\repos\Surly\Book`).

**Build tested:** Curl at commit `f4b30d981f` (2026-11-18 16:52), from `dotnet build -c Release` of `Curl.Console` and `Curl.Mcp`. `Curl.Api` and `Libcurl` were packed with `-p:BundledProjectReferencesPrivateAssets=all`. Windows 11, .NET SDK 11.0.100. Upstream `curl.exe` 8.22.0 (Windows) was used for comparison.

## Earlier report (`2026-11-18T1233-Curl-bug-report.md`): status on this build

| # | Finding | Status |
|---|---|---|
| 1 | `curl-mcp` refuses `-o NUL` and `-o /dev/null` | **Fixed for `NUL`**: `-o NUL` now runs and discards the body. `-o /dev/null` now gets past the root check and fails with exit 23 (`WriteError`). Upstream `curl.exe` on Windows does the same (exit 23), so this is not a defect. |
| 2 | `Curl.Api` README usings sample lacks `Curl.Api.Codes` | Not rechecked |

## New findings

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Low | `Curl.Api` README | `HttpAuthenticationMethods` is in `Curl.Api.Options.Authentication`, which the README never names |
| 2 | Low | `Curl.Api` | Setting a request `Body` to a null `byte[]` throws `ArgumentNullException` at run time |

---

### 1. `Curl.Api` README: `Curl.Api.Options.Authentication` is not named
A reader who copies the README's samples and sets an authentication method, for example Digest, which plain-HTTP servers need because Basic is refused there, uses `HttpAuthenticationMethods`. That type lives in `Curl.Api.Options.Authentication`, which neither the usings sample nor the text names, so the program does not compile until the reader finds it. This is the same kind of gap as earlier finding 2.

### 2. `Curl.Api`: a null body throws at run time
**Repro** (reported by the chapter writer; not re-run for this report):
```csharp
byte[]? bytes = upload ? data : null;
var request = new CurlRequest(HttpMethod.Put, url) { Body = bytes };   // ArgumentNullException
```
**Observed:** `ArgumentNullException` from the implicit conversion from `byte[]` to the body type. The code compiles without a warning, because the conversion accepts a nullable `byte[]`.
**Expected:** either no body when the value is null, or a compile-time signal. That could be a conversion that rejects nullable input under nullable annotations, or a documented `CurlRequestBody.None`. As it stands, the failure comes only at run time.
