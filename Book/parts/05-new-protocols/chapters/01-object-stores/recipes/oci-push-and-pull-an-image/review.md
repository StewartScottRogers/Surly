<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: oci-push-and-pull-an-image

Verdict: accept with fixes

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 71807f05f), Curl port 8.22.0 (Curl commit 82913be53d): 15 of 15 cases pass on windows.

## Findings

1. Decision: the Solution is one `curl` with three `-T` pushes (empty config, layer, manifest) and a final pull, since a push of an image is three requests in order; `surl` refuses a manifest whose blobs it lacks (case `manifest-needs-its-blobs`). The digests are fixed in the fixtures and the YAML comment says which file each is.
2. A refused push (no `--allow-uploads`, or anonymous) is exit 67, not 22: the token endpoint grants no `push`, and `curl` reports the 403 DENIED as a login failure. The Discussion says so.
3. Needs a source: the empty config `{}` and the manifest format are the OCI Image Format Specification, which is not in the bibliography; the Discussion cites only <<oci-distribution>>. Proposed key `oci-image-spec` (https://github.com/opencontainers/image-spec/blob/v1.1.0/spec.md).
4. An anonymous push of a blob the registry already holds exits 0, because `curl` skips a blob the registry has; the anonymous-push case therefore pushes the config, which the `open` layout lacks.
5. The `https` listener in the Solution is there for the layout claims; the Discussion says so.
