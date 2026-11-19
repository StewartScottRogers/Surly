<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-put-and-delete-files-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f), surl-mcp 1.0.0, curl-mcp 0.1.0.0: 8 of 8 cases pass on windows.
## Findings
1. Fixed: "as the store would refuse it from any client" (hidden paths) had no case; removed.
2. Needs the author (program finding): `put_file` with a relative path (`reports/b.txt`) or a `..` segment (`/../x.txt`) answers a JSON-RPC `Internal error` (-32603) rather than a tool error with a reason. verify cannot express a protocol error as a case, so the Discussion says only that `path` is a request path.
3. The client refusal is shown with `-X DELETE -s -w "%{http_code}"` and no `-o`, because curl-mcp refuses `-o NUL` and `-o /dev/null` (known Curl finding); honest only because the 401 body is empty.
4. Absence of `reports/a.txt` after `delete_file` is proven by the 404, not by `list_files` (result-contains cannot prove an item missing from a list).
5. <<http-accept-uploads>> and <<http-reset-and-snapshot-from-an-agent>> are cross-references to Part 2 recipes.
