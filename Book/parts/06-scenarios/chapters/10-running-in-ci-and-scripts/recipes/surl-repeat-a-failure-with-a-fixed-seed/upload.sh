#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# The app under test, as a script: a multipart upload of big.bin into the photos bucket.
# Usage: sh upload.sh <S3 endpoint URL>
sign="--aws-sigv4 aws:amz:us-east-1:s3 -u AKIAIOSFODNN7EXAMPLE:s3cret"
id=$(curl -sS $sign -X POST "${1}photos/big.bin?uploads" |
  sed -n 's|.*<UploadId>\([0-9a-f]*\)</UploadId>.*|\1|p')
echo "upload $id"

# The bug the test run trips over, standing in for your app's: it uses the upload ID as a
# name, and a name may not begin with a digit. Only some IDs bring it out.
case $id in
  [0-9]*) echo "failed: upload $id cannot be named" >&2; exit 1 ;;
esac

printf 'part one\n' > part1.bin
etag=$(curl -sS $sign -D - -o /dev/null -T part1.bin "${1}photos/big.bin?partNumber=1&uploadId=$id" |
  tr -d '\r' | sed -n 's/^[Ee][Tt]ag: //p')
printf '<CompleteMultipartUpload><Part><PartNumber>1</PartNumber><ETag>%s</ETag></Part></CompleteMultipartUpload>' \
  "$etag" > complete.xml
curl -sS -f $sign -o /dev/null --data-binary @complete.xml "${1}photos/big.bin?uploadId=$id" || exit
echo "stored big.bin"
