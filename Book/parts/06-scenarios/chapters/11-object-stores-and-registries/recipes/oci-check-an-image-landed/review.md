<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: oci-check-an-image-landed
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797: 11 of 11 cases pass on windows.
## Findings
1. The check reads the store, not the registry: repositories are folders in OCI image layout, so `list_files` and `read_file` show `index.json`, the manifest blob and `blobs/sha256/<hex>`. Sizes come from `list_files` `byte_count`; hashes from `read_file` text (the layers are text).
2. A manifest that names an unpushed layer is curl exit 22 (400 MANIFEST_BLOB_UNKNOWN); uploads off and a wrong password are exit 67; a digest mismatch is exit 25 before anything is sent. facts/oci.md added.
