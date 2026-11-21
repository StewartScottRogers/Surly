#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Makes site/f1.bin to site/f50.bin, 100,000 random bytes each.
mkdir -p site
i=1
while [ $i -le 50 ]; do
  head -c 100000 /dev/urandom > site/f$i.bin
  i=$((i + 1))
done
