# Deep-dive polish research — gameplay and graphics

Second research pass, digging harder into all four reference games —
this time both gameplay *and* visual fidelity — and cross-checked
against what's actually in this project right now (not assumed). Builds
directly on [retention-research.md](retention-research.md) rather than
replacing it; the biggest single finding here is a direct answer to
that doc's central question.

## The big gameplay finding: Lethal Company proves escalating quota *can* work

Retention research flagged "escalating quota, nothing else changes" as
the likely cause of Rob Everyone's own drop-off, citing Gamble With
Your Friends' identical, well-documented complaint. Worth checking the
counter-example directly: **Lethal Company's quota also escalates
50–99% per cycle** (compounding, similar magnitude to Rob Everyone's
own 1.5x/batch) — and it's one of the most sustained multiplayer
co-op hits of the last few years, not a game people bounce off after
one session. Digging into *why* it holds up where Gamble With Friends
doesn't surfaces the actual mechanism:

1. **Variety comes from randomized *conditions* layered on a fixed
   roster, not a new map every time.** Every moon rolls random weather
   — light/heavy fog (caps visibility, changes how you have to play),
   rain (hidden quicksand), storms (lightning that can kill you,
   especially if holding metal), eclipses (total darkness), flooding.
   Same handful of moons, but which one you're on and what it's doing
   today changes the run completely. This is *much* cheaper to build
   than new geometry and is exactly the kind of "structural variety"
   retention-research.md called for — **and it's already a scoped,
   deferred idea in this project**: `docs/plan.md`'s own "Future
   ideas" section has "Time-of-day rounds — most rounds play in bright
   daytime, but occasionally a round is dusk or night instead, raising
   difficulty (lower visibility)," explicitly deferred pending its own
   scoping pass. Given this research, that idea deserves to move up the
   list — it's the single most direct, already-half-designed answer to
   the actual retention problem.
2. **The skill expression changes as the number grows.** "Quota
   management — not raw scavenging skill — is the core competency that
   separates crews that run long games from crews that flame out at
   cycle three." Early cycles are about learning to explore; late
   cycles are about risk/reward judgment (which moon, how much time
   left, is this worth the danger). The difficulty curve isn't just
   "the same actions, more of them" — it's asking a different question
   of the player over time.
3. **No ending, by design — which Rob Everyone already committed to.**
   Lethal Company has no win state; it's built to escalate
   indefinitely. `docs/plan.md`'s own Win Condition is identical:
   "none — deliberately endless score-attack/freeplay, no fixed round
   count." That's the *correct* structural choice per this comparison
   — Gamble With Friends' weaker retention correlates with its finite,
   run-based structure (12 in-game days, 3 endings) which puts a
   visible ceiling on "how much is there," where Lethal Company's (and
   Rob Everyone's own) open-endedness doesn't. Nothing to change here;
   just confirms the existing design decision was right, and the fix
   belongs in variety (#1 above), not in adding an ending or resetting
   the format.

A second, independent data point for the *failure* pattern: **How to
Fish** (also researched this pass) shows the identical complaint —
"doesn't introduce many new mechanics as you progress... falls into a
familiar power-creep cycle," "funny for the first ten minutes and then
flat: the same fish species, the same islands, the same shop
rotation." That's now three separate reference games converging on the
same lesson (two showing the failure, one showing the fix) — about as
strong a signal as this kind of research produces.

## Other gameplay-adjacent findings

- **Super Battle Golf's stated design value: "visual feedback for
  shots, impacts, and item effects is clear enough that players rarely
  feel confused about what happened."** Directly relevant to two
  things already flagged elsewhere: the [SFX plan](sfx-plan.md)'s
  sabotage-use-sound gap, and issue #26 (HUD result banner still plain
  text) — both are exactly this "make the important moment legible"
  problem.
- **Super Battle Golf stacks its biggest VFX density right at the
  payoff moment** — "up to eight players throw smoke bombs and orbital
  strikes" around the cup as a match resolves. Rob Everyone's own
  equivalent payoff moments (hitting quota, a successful extraction, a
  batch completing) are comparatively quiet right now — worth treating
  "the moment you actually win" as a deliberate VFX/audio climax, not
  just a UI panel.
- **How to Fish's progression is explicit and mechanical**: money from
  selling catches buys real power upgrades (better rods, then guns,
  shotguns, explosives) — not just cosmetic. Rob Everyone's Cash
  currently buys sabotage tools (gated by `unlockBatch`, per
  retention-research.md) but it's worth being clear-eyed that this is
  a *lateral* unlock (more options) rather than How to Fish's *vertical*
  one (objectively stronger) — not necessarily a problem, but worth
  knowing which kind of progression is actually being offered when
  tuning how satisfying it feels to hit a new batch.

## Graphics: what's actually true about the project right now

Checked directly rather than assumed:

- **No decal system exists at all** — no `DecalProjector` usage
  anywhere in the codebase. The user's own tire-marks example is a
  real, confirmed gap, not something already half-built.
- **The lawn is a flat procedural shader** (`LawnStripes.shadergraph` +
  a normal map), not real geometry — matches `art-info.md`'s own
  documented reasoning for why (a real per-blade grass system was
  "considered and deliberately deferred" as too big an undertaking for
  a 2-person art team, revisit only as late polish). Still the correct
  call — see priority order below for why this stays lower than it
  might seem given how much the reference games' own visual identity
  leans on dense foliage.
- **`ScreenSpaceAmbientOcclusion` is present in `PC_Renderer.asset`**,
  so AO is at least wired at the renderer level — worth confirming in
  the Editor whether it's actually *enabled and tuned* or just present
  as an unused Renderer Feature.
- **`Assets/Settings/DefaultVolumeProfile.asset` exists but its actual
  tuning couldn't be confirmed by reading the file alone** (it lists
  ~20 override components, which is consistent with Unity's own
  default full-profile template rather than necessarily meaning
  someone hand-tuned bloom/color grading/vignette) — genuinely worth an
  Editor look rather than assuming either way.

## Graphics: prioritized list

Ordered by (impact) ÷ (cost) — cheapest, most bang-for-buck items
first, matching the reference games' own "stylized, not photorealistic,
but *considered*" formula (none of these games are graphically
expensive; they're graphically *deliberate*).

1. **A real post-processing pass** (#64, walkthrough:
   [postprocessing-setup.md](postprocessing-setup.md)) — likely the single cheapest lever
   available. If `DefaultVolumeProfile`/the per-scene profiles turn out
   to be untuned defaults, even a modest pass (color grading for a
   consistent daytime "look," a touch of bloom on emissive/light
   sources, a subtle vignette, confirming AO is actually on) is pure
   Editor-side tuning — no new assets, no code, and URP's Volume system
   is built exactly for this. Directly addresses the "pre-build/alpha
   feel" complaint on its own more than almost anything else on this
   list.
2. **Tire marks / skid decals on the roads** (#65) — the user's own example,
   confirmed as a real gap. URP's Decal Projector (a Renderer Feature +
   a decal material, spawned/faded at wheel-contact points for
   `CarDriver`) is the standard, cheap way to do this — small, fades
   over time, no new geometry. Same technique generalizes to scorch
   marks (Dynamite), footprints, blood/impact marks, and ground-seam
   blending (#72 — the same Decal Renderer Feature this needs also
   covers blending grass into dirt/sidewalk at every hard seam, so the
   two are worth building together) — one system, several uses.
3. **Weather/atmosphere variety** (#62) — this is the same system as the
   *gameplay* fix above (Time-of-day/weather rounds), just also
   counted here because it's a major graphics win in its own right:
   fog, overcast lighting, a dusk/night pass are all straightforward
   URP lighting/skybox swaps (the project already has Kenney's
   day/night skyboxes imported and `NightModeVisuals` built for a
   night round) — this is genuinely two birds with one system.
4. **Small ambient-life details** (#27) — distant traffic sound/movement,
   wind-blown debris or leaves, birds, a subtly animated flag/sign —
   cheap per-item, and this exact category is what issue #27
   (environmental detail pass) and #51's own background-flythrough
   work were already gesturing toward. Doesn't need to be much; these
   reference games all lean on *a few* well-chosen ambient touches
   rather than dense systems.
5. **Impact/action VFX density** (#66) — dust kicked up while sprinting, a
   spark/flash on a landed sabotage hit, a cash-burst or confetti
   moment on hitting quota (ties directly to the "climactic payoff"
   gameplay finding above, and to the SFX plan's sabotage-use gap —
   these should probably be built together, sound and VFX layered onto
   the same use-moment in `SabotageUseController`).
6. **Real 3D grass** (#67) — stays lower priority *on purpose*, not
   forgotten. This research doesn't overturn the project's own
   original reasoning for deferring it (real technical undertaking,
   LOD/perf tuning across the whole map, a 2-person art team) — it
   just confirms *why* it would help (How to Fish and Super Battle
   Golf both lean on dense, colorful environments as part of their
   identity) without changing the cost side of that tradeoff. Worth
   revisiting once items 1–5 are done and there's a sense of how much
   headroom is left, not before.

## Suggested order across both lists

1. Post-processing pass (#64) — cheapest, no new assets, biggest single
   "stop looking pre-build" lever.
2. Weather/time-of-day variety (#62) — the one item that's
   simultaneously the graphics win in this doc *and* the direct fix
   for the retention-research.md drop-off finding. Highest combined
   value on this whole list.
3. SFX sabotage-use sounds (#22) + impact VFX (#66), built together
   (see [sfx-plan.md](sfx-plan.md)).
4. Tire marks / decal system (#65).
5. Surface the next batch's unlock explicitly at end-of-round-3 (#61,
   from retention-research.md) + a real payoff moment (VFX/audio) on
   hitting quota (#66).
6. Ambient-life detail pass (#27).
7. Real 3D grass (#67), once the above has landed and there's a sense
   of remaining headroom.

## Filed issues

All actionable items from this doc and retention-research.md were
filed as tracked issues:

- #60 — sabotage comeback reward
- #61 — surface next batch's unlock
- #62 — weather/time-of-day variety per round
- #63 — reconsider quota growth as the only difficulty lever
- #64 — post-processing pass
- #65 — tire-mark/decal system
- #66 — impact/action VFX pass + quota payoff moment
- #67 — real 3D grass

A few items overlapped with issues already open and were added as
cross-reference comments there instead of new issues: #59 (alternate
map layout, reconsidered as a rotation mechanic), #27 (ambient-life
detail specifics), #26 (HUD result banner, feedback-clarity tie-in),
#22 (sabotage SFX, paired with #66's VFX half).

## Where to look

- [retention-research.md](retention-research.md) — the gameplay
  problem this doc's #1 finding directly answers.
- [sfx-plan.md](sfx-plan.md) — where the impact-VFX item should be
  built alongside its matching sound.
- `docs/plan.md`'s "Future ideas (not yet scoped)" section —
  Time-of-day rounds, real 3D grass — both directly referenced above.
- `docs/art-info.md` — the lawn shader's own documented deferral
  reasoning, still valid.
- `Assets/Settings/DefaultVolumeProfile.asset`,
  `Assets/Settings/PC_Renderer.asset` — the post-processing/AO state
  that needs an actual Editor look to confirm.
- `Assets/Scripts/AI/CarDriver.cs` — where tire-mark decals would hook
  in.
- `Assets/Scripts/Sabotage/SabotageUseController.cs` — where impact
  VFX and the SFX plan's use-sounds should land together.
- Issues #21 ("Good House" visual tell), #26 (plain-text HUD banner),
  #27 (environmental detail pass), #40 (first-person viewmodel) — all
  already-filed, already-relevant pieces of this same picture.
