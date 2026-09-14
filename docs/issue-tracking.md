# Issue tracking

This project's status tracking lives in **GitHub Issues**, not a doc in
this repo — `docs/todo.md` and `docs/completed.md` used to serve that
role and have been retired (migrated into issues #1, #17–#40). Don't
recreate a status-tracking markdown file; if you're a Claude agent
working on this repo, this doc replaces that instinct.

This applies to everyone working on the repo — Chayton, Zach, Brian,
Goodson, and any Claude session (this one or a future one).

## The two labels that matter

- **`bug`** — something that used to work (or should work per the
  design docs) and doesn't. Use the **Bug report** issue template
  (`.github/ISSUE_TEMPLATE/bug_report.md`) — it collects branch/commit,
  build type, scene, multiplayer context, repro steps, expected/actual,
  frequency, and logs, because a report missing that context is often
  unreproducible later by whoever picks it up.
- **`enhancement`** — everything else genuinely open: unbuilt features,
  deferred polish, follow-ups noted during a playtest, milestones not
  yet started. This is also the label used on **closed** issues that
  record a shipped, playtested piece of work (see below) — it covers
  both "not done yet" and "done, here's the record," which reads oddly
  at first but keeps one label meaning "this is planned/tracked work"
  regardless of which side of done it's on.

Other default labels (`documentation`, `question`, `wontfix`, etc.)
exist and can be used normally; they just aren't part of this
open/closed convention.

## Open vs. closed is the status

- **Open** = genuinely not done. This is what `docs/todo.md` used to be.
- **Closed** = done, playtested, confirmed working. This is what
  `docs/completed.md` used to be — closed issues are kept, not deleted,
  as the historical record. Close with a comment saying what actually
  shipped (or, for a bug, what fixed it and where) rather than closing
  silently — a bare "closed" with no comment loses exactly the
  information `docs/completed.md` used to carry inline.

A handful of issues (#30–#38) were filed **already closed** during the
`docs/completed.md` migration, titled `[Done] <system>` — these are
retroactive historical records for large, already-finished bodies of
work, grouped by system rather than one-per-bullet (the open items got
one issue per genuinely actionable line item instead — see
"Granularity" below). New closed-on-arrival issues like this are fine
for the same situation (a big finished pass that never had its own
tracking issue), but the normal flow going forward is: open an issue
when work starts (or when a gap is noticed), close it when the work
ships.

## Granularity

One issue per genuinely distinct, individually-workable item — not a
mega-issue bundling unrelated things, and not a separate issue for
every one-line tweak. If you're unsure, look at #17–#29 (the open items
migrated from `docs/todo.md`) as the calibration: each is something you
could pick up and finish on its own.

## Cross-referencing

- Link related issues to each other with `#N` — GitHub auto-links these
  and shows the connection on both issues. Several existing issues do
  this already (e.g. #2/#3/#4 for a shared root cause, #9/#12 for the
  same "works same-machine, breaks over real Steam" pattern).
- Link to the relevant `docs/stages/*.md` setup doc where one exists,
  using a repo-root-relative path (`docs/stages/stage6-sabotage-items-setup.md`)
  — GitHub resolves these correctly in issue bodies, same as in a repo
  file. Prefer this over restating a doc's content inline.
- When a setup doc's own text needs to reference status (done/open,
  or a specific known bug), link the issue number rather than writing
  free-text status that can drift out of sync — that drift is exactly
  what happened to `docs/todo.md`/`docs/completed.md` over time and is
  why they were replaced.

## Titles

- Bug reports: the template pre-fills `[Bug] ` — keep it.
- Retroactive "this big thing is already done" issues: `[Done] <system
  name>`, matching #30–#38.
- Everything else: a plain, specific, descriptive title. No prefix
  needed.

## Reporting something

- **Directly on GitHub**: New Issue → pick **Bug report** for a defect,
  or a blank issue with the `enhancement` label for a feature/todo.
- **In conversation with Claude**: just describe it. `gh` is
  authenticated on this machine — Claude will run `gh issue create`
  with the right label, fill in what's actually known, and leave a
  field blank rather than guess at it (per the bug template's own
  instruction). Tell Claude when something's fixed and it'll close the
  issue (or ask it to check first if you're not sure which issue).

## For a Claude agent picking up this repo

- Check the [Issues tab](https://github.com/joshuaclemons1/Rob-Everyone/issues)
  before assuming something is or isn't built — don't trust a stage
  doc's own inline status line over it (several drifted stale before
  this migration; that's exactly the failure mode this process
  replaces).
- When the user confirms a piece of work is tested and working, offer
  to file it as a closed issue (or close an existing open one with a
  comment) rather than writing status into a markdown file. Don't
  create a new `todo.md`/`completed.md`-shaped doc even temporarily.
- `docs/plan.md` (the build order and stage-doc index) is unaffected by
  this — it's a plan/reference doc, not a status tracker, and stays a
  markdown file.
