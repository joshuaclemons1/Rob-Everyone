# SFX plan (issues #22 / #23)

Scoping pass before implementing, grounded in reading the actual audio
code that already exists — the gap is narrower and more specific than
"no sound effects exist." Written alongside a broader look at why all
four of this project's own reference games (Lethal Company especially)
lean on audio as hard as they do: it's the cheapest way to make an
action that already works *feel* like it landed.

## What's already covered — don't re-do this

More is built than it might look like from the outside. All of this
already works and just needs real audio files dropped in (or already
has them):

| System | Where | What it covers |
|---|---|---|
| `SfxPlayer.PlayRandomAt` | `Assets/Scripts/Audio/SfxPlayer.cs` | Shared one-shot-at-position utility every other system below calls into |
| `PickupSfxLibrary` | `Assets/Scripts/Items/PickupSfxLibrary.cs` | Generic pickup sound, `Resources`-loaded, no per-item wiring |
| `ShopSfxLibrary` | `Assets/Scripts/Shop/ShopSfxLibrary.cs` | Purchase success / insufficient funds / item sold |
| `PlayerFootstepAudio` | `Assets/Scripts/Player/PlayerFootstepAudio.cs` | Footsteps |
| `PlayerImpactRelay.impactClips` | `Assets/Scripts/Player/PlayerImpactRelay.cs` | **One generic "got hit" sound**, shared indiscriminately by a car impact *and* every sabotage hit (Taser, Bat, Hammer, Tranq Gun, Dynamite blast) — see the gap below |
| `PoliceAI.chaseStartClips` | `Assets/Scripts/AI/PoliceAI.cs` | The "spotted you!" stinger the instant a chase starts — not a continuous siren, see below |
| `HomeownerAI.suspiciousClips` / `alertedClips` | `Assets/Scripts/AI/HomeownerAI.cs` | Suspicion/alert vocal stingers |
| `AlarmClockProjectile.ringClips` | `Assets/Scripts/Sabotage/AlarmClockProjectile.cs` | The alarm clock's own ring |
| `SabotageProjectile.explosionClips` | `Assets/Scripts/Sabotage/SabotageProjectile.cs` | Dynamite/thrown-item detonation |
| `JailState.jailedClips` / `rescuedClips` | `Assets/Scripts/Round/JailState.cs` | Jail & Bail stingers |
| `CarDriver.hornClip` / `yellClip` | `Assets/Scripts/AI/CarDriver.cs` | Traffic hazard honk/yell |

The established pattern for a shared pool (`PickupSfxLibrary`/
`ShopSfxLibrary`): a `[CreateAssetMenu]` `ScriptableObject`,
`Resources`-loaded once via a static `.Instance`, no per-prefab wiring.
Reuse this shape for anything genuinely shared across many objects;
don't reuse it for something that's really one item's own sound (see
below).

## The real gaps, in priority order

### 1. Sabotage item *use* sound — the single biggest gap

`SabotageUseController` (`Assets/Scripts/Sabotage/SabotageUseController.cs`)
is the one place every Taser zap, Bat/Hammer swing, Tranq Gun shot, and
Dynamite throw actually fires from (`TryUseMelee`/`TryUseRanged`/
`TryUseThrown` → `CmdUseMelee`/`CmdUseRanged`/`CmdUseThrown`) — and it
has **zero audio at all**. Right now every one of these items plays its
arm animation (`PlayUseAnimation`) completely silently. This is the
"cracked helmet HUD" category of fix from the reference-game research
below: a few seconds of sound stapled onto an action that already
works, not a new system.

Unlike Pickup/Shop sounds (generic across dozens of different items,
which is exactly why those use a shared library), a **use** sound is
inherently item-specific — a Taser shouldn't sound like a Bat. The
natural place for this is directly on `ItemDefinition`
(`Assets/Scripts/Items/ItemDefinition.cs`), mirroring the existing
`useAnimation` field exactly:

```csharp
[SerializeField] private AudioClip[] useSoundClips; // new, next to useAnimation
public AudioClip[] UseSoundClips => useSoundClips;
```

`SabotageUseController.PlayUseAnimation` is already the one place every
use path already funnels through — add a sibling `PlayUseSound(item)`
called alongside it, using the existing `SfxPlayer.PlayRandomAt`. Needs
to run on **every** client (not just the owner), same as the animation
itself already does via the Command/ClientRpc round trip already in
place — worth double-checking `PlayUseAnimation`'s own owner-vs-remote
handling before assuming the sound hookup is a 1:1 copy.

### 2. Differentiated hit sounds

`PlayerImpactRelay.impactClips` is one pool shared by *everything* that
calls `ServerApplyImpact`/`ServerApplyPvpImpact` — a car hitting you and
a Taser zapping you currently sound identical. Splitting this into a
generic "blunt impact" pool (car, Bat, Hammer) plus a couple of
source-specific pools (an electrical zap for Taser, a dart-thump for
Tranq Gun) would sell each item's identity a lot more cheaply than it
sounds — most of this is picking the right clips, not new code, since
`RpcApplyImpact` already knows what force/direction it was given and
`SabotageUseController` already knows which `ItemDefinition` triggered
it. Lower priority than #1 (this is "good" → "great," not "silent" →
"has a sound").

### 3. A continuous police siren during a chase

Issue #23. `PoliceAI.chaseStartClips` already covers the *moment* a
chase begins; there's nothing looping while the chase is actually
happening. A siren is a different audio shape than everything above —
a looping, positional `AudioSource` that turns on/off with
`PoliceState.Chase`/`PoliceState.Respond`, not a one-shot `SfxPlayer`
call — worth designing as its own small component
(`PoliceSirenAudio`, owning an `AudioSource` with `loop = true`,
enabled/disabled alongside the state machine) rather than trying to
force it through the one-shot pattern everything else here uses.

### 4. Ambient/music

Issue #24 — the bigger, separate effort.
[music-brief.md](music-brief.md) already exists as the composer-facing
spec (track list, looping/transition requirements) for this half;
nothing plays a track yet at all (`AudioMixerApplier`/`AudioSettings`
only handle the volume sliders). Out of scope for this SFX-specific
doc, flagged here only so it isn't forgotten as the other open half of
"the game plays in near silence right now."

## Where to actually find the sounds — CC0 only, same policy as art

This project's asset policy (`docs/art-info.md`) is CC0-first the same
way the art pipeline is. The equivalent sources for audio:

- **[Kenney's own audio packs](https://kenney.nl/assets?q=audio)** —
  CC0, same trusted source already used for most of this project's art
  (`Kenney-FurnitureKit`, `Kenney-SplatPack`, etc.). Worth checking
  first for exactly the reason it already won for art: zero attribution
  risk, and Kenney's UI/impact/explosion packs are a very plausible fit
  for #1/#2/#3 above.
- **[Freesound.org](https://freesound.org)**, filtered to CC0 — the
  largest general SFX library; most individual clips are CC-BY or
  CC-BY-NC rather than CC0, so the license filter matters here more
  than it did for 3D models (where CC0 sources dominated).
- **[OpenGameArt.org](https://opengameart.org)**, filtered to Public
  Domain/CC0 — smaller than Freesound but more consistently game-ready
  (looping-friendly, pre-trimmed).

Same caution as the paint can from #52: check each individual clip's
own license page, not just the pack/collection page — a collection can
mix CC0 and CC-BY items under one listing.

## Suggested order

1. Sabotage use sounds (#1) — single biggest gap, cheapest to wire
   (the `ItemDefinition` field + `SabotageUseController` hookup is
   pure code, no assets needed to write it — could be done today, then
   just needs real clips dropped in once sourced).
2. Source/import the actual clips (Kenney first, Freesound/OpenGameArt
   CC0-filtered as fallback) for #1, plus whatever's still empty in
   the already-built systems from the "already covered" table above.
3. Differentiated hit sounds (#2) — cheap once #1's clips already
   exist, since it's mostly clip selection.
4. Police siren (#3) — small new component, not urgent but a real gap.
5. Music (#24) — its own, larger effort, `music-brief.md` already
   scopes it.
