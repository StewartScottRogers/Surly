#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# The deploy step: pulls the manifest at REF from the registry, checks that it hashes to the
# digest the build pushed, then pulls each blob it names and checks each the same way.
# Usage: sh deploy.sh REGISTRY REF DIGEST, e.g. sh deploy.sh 127.0.0.1:5000 demo/app:1.0 sha256:...
repo=${2%%[:@]*}
curl -sS --cacert server.pem -u deploy:d3ploy -D pull.headers -o pulled.json "oci://$1/$2" || exit $?
got=sha256:$(openssl dgst -sha256 -r pulled.json | cut -d ' ' -f 1)
said=$(tr -d '\r' < pull.headers | grep -i '^docker-content-digest:' | tail -n 1 | sed 's/^[^:]*: *//')
echo "pulled $2: $got"
echo "the registry says: $said"
if [ "$got" != "$3" ]; then
  echo "not the manifest the build pushed ($3)"
  exit 1
fi
for blob in $(grep -o '"digest":"sha256:[0-9a-f]*"' pulled.json | cut -d '"' -f 4); do
  curl -sS --cacert server.pem -u deploy:d3ploy -o blob.bin "oci://$1/$repo@$blob" || exit $?
  test "sha256:$(openssl dgst -sha256 -r blob.bin | cut -d ' ' -f 1)" = "$blob" || { echo "blob $blob: hash differs"; exit 1; }
  echo "blob $(echo "$blob" | cut -c 1-19): $(wc -c < blob.bin | tr -d ' ') bytes, hash ok"
done
echo "deploy: image verified"
