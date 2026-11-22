#!/bin/sh
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Runs curl with the arguments after the first two and says whether the whole command,
# every attempt and every wait, took from MIN to under MAX seconds.
# Usage: sh timed.sh MIN MAX CURL-ARGUMENTS...
min=$1; max=$2; shift 2
start=$(date +%s)
curl "$@"
code=$?
end=$(date +%s)
elapsed=$((end - start))
if [ "$elapsed" -ge "$min" ] && [ "$elapsed" -lt "$max" ]; then
  echo "exit $code within $min to $max seconds"
else
  echo "exit $code after $elapsed seconds"
fi
