<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-reset-and-snapshot-in-process
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 7 of 7 cases pass on windows.
## Findings
1. Fixed: "PutAsync writes through the same store a client's upload does" was a README phrase with no case. It now says only what the listing shows.
2. Not claimed here: surl takes a PUT only with a login, and refuses Basic over `http://`. The client logs in with Digest because of this, but the refusals are cases in <<http-wait-for-an-upload-in-process>>, not here. The Discussion says only that Digest works with no loosening option.
3. Reader friction: `HttpAuthenticationMethods` is in `Curl.Api.Options.Authentication`, which the Curl.Api README never names. Also, `Body = cond ? null : bytes` throws `ArgumentNullException` at run time: the implicit `byte[]` to `CurlContent` conversion rejects null. Cast to `CurlContent` first.
4. Reader friction: HTTP DELETE is 405 even under `--allow-uploads`, so a test cannot delete over HTTP. The program overwrites a file to show a restore instead.
5. The `DataDirectory` refusal is a `ComposedStoreException` from `Surl.Composition`. That type derives from `InvalidOperationException`, so the program catches the base type the README names.
6. Cross-reference <<http-reset-and-snapshot-from-an-agent>> points at a recipe not written yet (chapter 11).
