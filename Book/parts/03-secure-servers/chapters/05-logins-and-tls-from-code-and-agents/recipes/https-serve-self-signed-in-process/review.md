<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-serve-self-signed-in-process
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 7 of 7 cases pass on windows.
## Findings
1. Fixed: removed three claims with no case: "nothing is bound when it throws" (that is <<http-catch-a-refused-start>>'s case), "60 is the exit code curl gives for the same failure" (true on the console, but no case here), and "TLS keeps the password out of sight on the wire".
2. Fixed: a plain `http` listener beside the `https` one, showing Basic refused 403, was dropped. It would have needed `AllowPlaintextAuthentication` named and shown both ways; that refusal belongs to <<http-refuse-a-password-in-clear>>, which See Also points at.
3. No `setup:` and no openssl: the program makes its own certificate with .NET's `CertificateRequest`, in a temporary folder it deletes, so no key is committed and run.sh/run.ps1 need nothing but dotnet. The brief's "trust a certificate the program makes" is read literally.
4. Basic is named as the client's method because `CurlAuthenticationOptions.HttpAuthentication` defaults to `Basic` (package XML docs); no case distinguishes it from Digest, since both pass over TLS. Acceptable as a restatement of the documented default.
5. Reader friction: `Surl.Api` has no way to read the certificate `SelfSigned` made, so a client cannot trust it, only skip verification. A test that wants verification on has to make its own certificate, as the program does.
6. Reader friction: `CurlException` is in `Curl.Api.Errors` and `CurlCode` in `Curl.Api.Codes` (another assembly's namespace); the Curl.Api README's sample does not show the `using` lines. `VerifyPeer`/`VerifyHost` sit two levels down (`CurlRequest.Tls.Origin`), and the README never shows them; a reader looking for `curl -k` has to find `CurlTlsPeerOptions` in the XML docs.
7. The program exits 0 whatever the requests got; each case matches its printed line. If the no-certificate start ever succeeded, the server would be left undisposed until exit; acceptable for a demonstration.
