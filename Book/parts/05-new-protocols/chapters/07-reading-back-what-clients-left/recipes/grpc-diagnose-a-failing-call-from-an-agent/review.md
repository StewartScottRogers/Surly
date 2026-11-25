<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: grpc-diagnose-a-failing-call-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 4 of 4 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. get_exchanges holds raw HTTP/2 with HPACK (Huffman) headers and no decoded layer; the program carries a small HPACK decoder, the Huffman code rebuilt from its 257 code lengths (canonical, checked against the Python hpack table).
3. Program length is above the model's because of the decoder; the lead may prefer a shorter recipe if surl-mcp ever gains decoded headers.
