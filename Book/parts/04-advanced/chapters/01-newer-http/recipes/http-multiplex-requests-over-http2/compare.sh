# Fetches the URLs given with -Z and then without, prints the whole seconds each run took,
# and says whether the run with -Z took less.
start=$(date +%s)
curl --http2-prior-knowledge -Z -s -o "got/s#1.txt" --create-dirs "$1" || exit
middle=$(date +%s)
curl --http2-prior-knowledge -s -o "got/s#1.txt" --create-dirs "$1" || exit
end=$(date +%s)
together=$(( middle - start ))
apart=$(( end - middle ))
echo "with -Z: $together s"
echo "without -Z: $apart s"
if [ "$together" -lt "$apart" ]; then echo "with -Z took less: yes"; else echo "with -Z took less: no"; fi
