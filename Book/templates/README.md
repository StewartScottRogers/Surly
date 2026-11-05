# Templates

To add a recipe: copy `recipe-template/` to `parts/<part>/chapters/<chapter>/recipes/<key>/`, rename every `KEY` to the key (the folder name), fill the metadata and the four sections, put the real commands between the `tag::solution[]` markers in `run.sh`, record the real output in `expected.txt`, then run `python3 build/build.py`. The build refuses a recipe whose `recipe-key` differs from its folder name.
