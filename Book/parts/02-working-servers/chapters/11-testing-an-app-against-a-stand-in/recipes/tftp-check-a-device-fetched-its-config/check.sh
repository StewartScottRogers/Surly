#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# The test's check: did the device read FILE over TFTP, whole, and at what block size?
# Usage: sh check.sh LOG FILE
log=$1
name=$2
for try in 1 2 3 4 5 6 7 8 9 10; do
  # surl logs one "Read of" line per read request: #N * Read of "r1.cfg" (octet): 27 bytes of <path> in blocks of 1024
  read=$(grep -a "^#[0-9]* \* Read of \"$name\" " "$log" 2>/dev/null | tail -n 1)
  if [ -n "$read" ]; then
    id=${read%% *}
    case $read in
      *"no file at"*) echo "$name: asked for, not on the server"; exit 1 ;;
    esac
    # The read is over once the device has acknowledged every block.
    if grep -a -q "^$id \* All .* were acknowledged" "$log"; then
      bytes=$(echo "$read" | sed 's/.*): \([0-9]*\) bytes of .*/\1/')
      echo "$name: read, $bytes bytes in blocks of ${read##* }"
      # The option acknowledgement surl sent (opcode 6), if the device asked for options.
      agreed=$(grep -a "^$id > .x00.x06" "$log" | grep -a -o "blksize.x00[0-9]*" | sed 's/blksize.x00//')
      if [ -n "$agreed" ]; then
        echo "agreed: blksize $agreed"
      else
        echo "agreed: no options, 512 by default"
      fi
      exit 0
    fi
  fi
  sleep 1
done
echo "$name: never read"
exit 1
