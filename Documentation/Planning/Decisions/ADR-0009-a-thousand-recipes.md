# ADR-0009: A thousand recipes, evenly across the console, C# and agents

## Status

Accepted, 2026-11-19, by the author ("use your picks"). Extends ADR-0004 (structure that grows) and ADR-0008 (the three surfaces).

## Context

The book has 100 recipes (76 console, 11 C#, 13 MCP), each mostly one feature. The author wants about 1,000, with more detailed and complex use cases, spread evenly across the three ways of driving Surl and Curl. Writing cost was measured on a pilot batch after the token-efficiency changes (writer guide, fact sheets, plan, scaffold, quiet tools): about 26,000 tokens a recipe, against about 40,000 before.

## Decision

1. **About 1,000 recipes, about a third on each surface**: console, C# (`Surl.Api`, `Curl.Api`, `Libcurl`), and agents (C# on the MCP SDK). The count serves coverage, not the reverse: a recipe solves a reader's real task or it is not planned.
2. **Triplets where they fit.** A use case that reads well on all three surfaces is planned as three recipes of its own (ADR-0008 item 1), linked by a shared `triplet` id in `plan.yml` and by See Also. A task that belongs to one surface is planned once.
3. **Part 6, Scenarios** (tier `scenarios`): end-to-end use cases across several protocols or steps, the way a test suite or an operator really uses the programs. Harder single features stay in the difficulty tiers.
4. **The plan comes first.** `editorial/plan.yml` gets every planned row (key, title, part, chapter, surface, protocols, task, triplet, batch) before writing starts, and the author approves it as he approved the 100-recipe chapter list.
5. **Batches of about 60**, 20 a surface, each ending in the author's sample review and merge. A batch's checks run on its own keys; the full book runs nightly and on release.
6. **Model per step.** The strongest model plans and writes new scenarios; a cheaper model (Sonnet) writes triplet twins and refreshes fact sheets, tried on the first batch and kept only if lint, verify and the sample review hold.

## Consequences

- The writer guide, fact sheets, plan and scaffold (token efficiency) are the way every batch is written.
- The website's load-on-demand switches on past 300 recipes; filters by protocol and surface are wanted before 1,000.
- More coverage finds more program defects; they go to the Surl and Curl sessions as dated reports.
