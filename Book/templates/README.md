# Templates

To add a recipe: copy `recipe-template/` to `parts/<part>/chapters/<chapter>/recipes/<key>/`, rename every `KEY` to the key (the folder name), fill `recipe.yml` (one case per claim the Discussion will make; exactly one with `role: solution`), write the four sections in `recipe.adoc`, then run lint, verify and build (see `../build/README.md`). The build refuses a recipe whose `key` differs from its folder name, and lint refuses one whose Discussion claims are not cases.
