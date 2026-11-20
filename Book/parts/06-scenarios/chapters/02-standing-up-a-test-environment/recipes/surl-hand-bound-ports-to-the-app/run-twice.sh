#!/bin/sh
# Runs the tests twice at once, the first serving directory $1 and the second $2.
SITE=$1 sh run-tests.sh > run1.txt 2>&1 &
first=$!
SITE=$2 sh run-tests.sh > run2.txt 2>&1 &
second=$!
wait $first; a=$?
wait $second; b=$?
if [ $a -le $b ]; then echo "exit codes: $a $b"; else echo "exit codes: $b $a"; fi
if [ $a -eq 0 ] && [ $b -eq 0 ] && [ "$(head -n 1 run1.txt)" != "$(head -n 1 run2.txt)" ]; then
  echo "each run got its own ports"
fi
grep -h -o 'is in use by another surl process' run1.txt run2.txt || true
