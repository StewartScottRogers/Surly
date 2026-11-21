# Runs the command given as arguments, then prints how many whole seconds it took.
start=$(date +%s)
"$@" || exit
echo "$(( $(date +%s) - start )) s"
