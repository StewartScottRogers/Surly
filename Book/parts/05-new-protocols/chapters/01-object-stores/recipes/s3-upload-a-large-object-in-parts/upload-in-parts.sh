#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Usage: sh upload-in-parts.sh FILE URL PART-BYTES [curl options...]
# Uploads FILE to the object URL as an S3 multipart upload: starts the upload, puts FILE in parts of
# PART-BYTES (the last may be shorter), then completes it with each part's number and ETag. Prints
# each step's status. When a step is refused it prints the S3 error code, aborts the upload so the
# server drops the parts it holds, and exits 1.
file=$1 url=$2 size=$3
shift 3
work=$file.parts
rm -rf "$work" && mkdir "$work"
code() { sed -n 's/.*<Code>\([^<]*\)<\/Code>.*/\1/p' "$work/answer"; }
abort() {
  echo "refused: $(code)"
  echo "abort: $(curl -s "$@" -X DELETE -o /dev/null -w "%{http_code}" "$url?uploadId=$id")"
  exit 1
}
status=$(curl -s "$@" -X POST -o "$work/answer" -w "%{http_code}" "$url?uploads")
echo "initiate: $status"
id=$(sed -n 's/.*<UploadId>\([^<]*\)<\/UploadId>.*/\1/p' "$work/answer")
if [ "$status" != 200 ]; then echo "refused: $(code)"; exit 1; fi
split -b "$size" "$file" "$work/part."
n=0
printf '<CompleteMultipartUpload>' > "$work/complete.xml"
for part in "$work"/part.*; do
  n=$((n + 1))
  got=$(curl -s "$@" -T "$part" -o "$work/answer" -w "%{http_code} %header{etag}" "$url?partNumber=$n&uploadId=$id")
  echo "part $n: ${got%% *}"
  case "$got" in 200*) ;; *) abort "$@" ;; esac
  printf '<Part><PartNumber>%s</PartNumber><ETag>%s</ETag></Part>' "$n" "${got#* }" >> "$work/complete.xml"
done
printf '</CompleteMultipartUpload>' >> "$work/complete.xml"
status=$(curl -s "$@" --data-binary "@$work/complete.xml" -o "$work/answer" -w "%{http_code}" "$url?uploadId=$id")
echo "complete: $status"
if [ "$status" != 200 ]; then abort "$@"; fi
