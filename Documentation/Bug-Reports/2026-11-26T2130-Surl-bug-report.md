<!-- Copyright (c) 2026 Stewart Rogers. -->
# Surl bug report, 2026-11-26 (batch 16 of the *Surly* cookbook)

From writing and verifying batch 16 (the book's last 10 recipes) against Surl `1e5204d` (the code of v0.1.9) and then v0.1.10 `654b8d2cb`, Curl v0.4.0-preview.2, Windows 11. Only findings not already filed.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Low | Accounts | A second add of the Bearer-token account (empty user name) is refused with a message that has a blank where a name would be: `user  already has an account` (two spaces). surl-mcp says `the Bearer-token account already exists`, which is clearer |
| 2 | Low | Faults, events | A `drop` fault's `FaultApplied` event does not say how the connection was closed: a rule with `Close = FaultClose.Graceful` and one with `FaultClose.Reset` give the same event (`RuleId f1, Kind Drop, Stage Accept`). Only the `-v` log shows which. A test that checks a cut was clean has nothing to assert on |

Re-run on 2026-11-26 against v0.1.10: 1. Item 2 was seen the same day by the recipe writer through Surl.Api on `1e5204d`, not re-run on v0.1.10.

## Repros

**1.** `surl --directory site --user app:s3cret --control http://127.0.0.1:18702 --control-token-file ct.txt http://127.0.0.1:18701/`, with `ct.txt` holding the control token.
- `curl -s -w " %{response_code}\n" --json '{"user":"","password":"t1"}' -H "Authorization: Bearer <control token>" http://127.0.0.1:18702/v1/accounts`, twice.
- Observed: ` 204`, then `{"error":"conflict","message":"user  already has an account"} 409`. Surl.Api's `Accounts.Add("", t)` a second time throws `User  already has an account.`
- Expected: the name of the account, as for any other, or surl-mcp's wording: `the Bearer-token account already exists`.

**2.** Surl.Api, an `http://` server with `Faults = { SurlFault.DropConnection() with { AfterBytes = 1_000_000, Close = FaultClose.Graceful } }`, a file larger than that, one fetch; then the same with `FaultClose.Reset`.
- `server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied })` returns one `FaultAppliedEvent` each time, with the same fields both ways: `RuleId f1, Kind Drop, Stage Accept`.
- Expected: the close in the event, such as a `Close` property (Graceful or Reset), and the same in surl-mcp's `fault_applied` event and the control endpoint's events.
- Recipe `http-tell-a-clean-close-from-a-reset` (all three versions) reads the difference from curl's exit code (18 against 56) instead.
