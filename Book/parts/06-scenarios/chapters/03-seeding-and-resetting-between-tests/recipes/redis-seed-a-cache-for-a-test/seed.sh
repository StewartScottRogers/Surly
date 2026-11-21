#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Seeds the settings and a cached copy that lives three seconds, reads them as the app would,
# waits until the copy has expired, and reads again, then asks the origin over HTTP.
# Usage: sh seed.sh <redis port> <http port>
r=redis://127.0.0.1:$1
h=http://127.0.0.1:$2
curl -s -u alice:s3cret "$r/SET/settings:currency/EUR" "$r/SET/cache:products/cached-copy/PX/3000" \
  "$r/GET/settings:currency" "$r/GET/cache:products" | {
  read -r set1; read -r set2; read -r currency; read -r cached
  echo "settings:currency: $currency"
  echo "cache:products: $cached"
}
sleep 4
curl -s -u alice:s3cret "$r/GET/settings:currency" "$r/GET/cache:products" | {
  read -r currency; read -r cached
  echo "settings:currency: $currency"
  echo "cache:products: [$cached]"
}
echo "origin: $(curl -s -u alice:s3cret "$h/products.json")"
