# Music brief

A composer-facing spec for every music cue "Rob Everyone" needs: what to
write, how long, what format to deliver it in, and — the part that
actually matters for a game rather than a soundtrack album — how to make
each piece loop and transition cleanly once it's in-engine. Companion to
[art-info.md](art-info.md)'s existing "Musical identity"/"Ambient music
states" bullets and [gameplay-design.md](stages/gameplay-design.md) for
the design context behind each state below.

## Where things stand right now

No music playback system exists yet — confirmed by reading the code.
`Assets/Scripts/Audio/AudioSettings.cs`/`AudioMixerApplier.cs` only
handle the Master/Music/SFX/Voice volume sliders; nothing actually plays
a track yet. Plain Unity audio throughout (`com.unity.modules.audio` in
`Packages/manifest.json`) — no FMOD/Wwise, no music middleware. That's a
programming task for later (me), not something you need to build — but
it means **how you deliver these files determines how simple that system
can be**, which is why the Looping/Transitions sections below are as
detailed as they are. Follow them and the in-engine wiring is close to
trivial; ignore them and it needs custom beat-matching code to fake a
clean transition.

## Track list

| Cue | Plays during | Priority | Loops? | Target length |
|---|---|---|---|---|
| Main Menu theme | `MainMenu.unity` | Must-have | Yes | 1:30–3:00 |
| Lobby theme | `Lobby.unity` (shop/ready-up, between rounds) | Must-have | Yes | 1:30–3:00 |
| Gameplay — Day, base layer | `SampleScene.unity`, `TimeOfDay.Day`/`.Morning` | Must-have | Yes | 1:30–3:00 |
| Gameplay — Day, tension layer | same, layered on top when a Homeowner/Police is Suspicious/Alerted/Chasing | Must-have | Yes, **same length as Day base** | matches Day base exactly |
| Gameplay — Night, base layer | `SampleScene.unity`, `TimeOfDay.Night` | Must-have (night is shipped, not planned) | Yes | 1:30–3:00 |
| Gameplay — Night, tension layer | same, night alert state | Must-have | Yes, **same length as Night base** | matches Night base exactly |
| Round-end / batch-transition stinger | overlay when a round ends or a batch boundary hits (quota step-up, no separate scene) | Nice-to-have | No (one-shot) | 3–8s |
| Jail ambient (or a filter, see below) | while a caught/quota-failed player is jailed (`JailState.cs`) | Nice-to-have | Yes if composed; N/A if using the filter option | 1:00–2:00 |
| Intro/logo sting | `Intro.unity` | Nice-to-have, lowest priority | No (one-shot) | 3–10s |

That's the full list — everything else audio-related (footsteps, Police
siren, catch/jail stingers, item pickup, shop SFX, sabotage-item SFX) is
**sound design, not music**, and already scoped separately in
art-info.md's own SFX bullets. Don't duplicate that work here.

## Format & export

Extends art-info.md's own cheatsheet (WAV source → OGG in-project):

- **Deliver WAV**, 44.1kHz, 16-bit, stereo (mono is fine for the
  stingers if that suits the sound). I'll convert to OGG on import — you
  don't need to do that conversion yourself, just don't hand me an MP3
  as the source (lossy-to-lossy re-encode loses more quality than
  starting from WAV).
- **No trailing silence** at the head or tail of any file you intend to
  loop — even a few milliseconds of dead air at the seam is audible as
  a click or a gap. Trim exactly to the material.
- **Folder/naming**, following art-info.md's `Assets/Audio/<System>/`
  convention: `Assets/Audio/Music/`, files named for what they are —
  `mainmenu_theme.wav`, `lobby_theme.wav`, `gameplay_day_base.wav`,
  `gameplay_day_tension.wav`, `gameplay_night_base.wav`,
  `gameplay_night_tension.wav`, `roundend_stinger.wav`,
  `jail_ambient.wav`, `intro_sting.wav`. Drop them in as you finish each
  one rather than waiting for the full set — I can wire each one up
  independently.

## Making a loop actually seamless

Unity plays a looping `AudioClip` gaplessly as long as the *source audio
itself* has no gap — there's no special export setting or metadata
needed beyond trimming correctly. The two ways a loop actually breaks:

1. **Silence padding** at the head/tail (see above — trim it out).
2. **An unresolved reverb/delay tail** — if your loop ends with reverb
   still ringing out, cutting straight to the loop point creates an
   audible chop. Fix: either print/render the tail so it naturally
   wraps into the start of the loop (many DAWs — Ableton, Logic, Reaper
   — have a dedicated "render as loop" or "loop bounce" mode that
   handles this for you automatically), or keep the loop dry enough
   that there's no tail to worry about and add space/reverb as a
   separate, non-looping layer if you want it.

**Pick one fixed BPM for the whole gameplay soundtrack** (Day/Night,
base/tension all share it) and compose every loop as a **whole number of
bars** at that tempo. This isn't just neatness — it's what makes the
tension-layer crossfade below work without any beat-matching code.

## Transitions

Two different situations, two different techniques — don't over-build
the simple one or under-build the other:

**Day/Night base ↔ tension (the real-time gameplay crossfade).** This is
the "calm exploration vs. tenser 'you've been spotted'" state art-info.md
already flags, driven by each nearby Homeowner/Police's alert state
(`HomeownerAI.cs`/`PoliceAI.cs`). Build this as **layered stems, not two
separate tracks that swap**: the base layer plays continuously and
never stops; the tension layer is a second, fully separate audio file of
the *exact same length, tempo, and key* as the base, that plays in
perfect sync underneath it at all times but starts silent. When a
Homeowner/Police goes Suspicious/Alerted/Chase, the code just fades the
tension layer's volume up (and back down on Idle/Returning) over a
second or two. Because both layers are always running in lockstep,
there's no seam, no re-triggering, no risk of the two ever drifting out
of sync — this is *why* the "same length as base" requirement in the
track list above is load-bearing, not a nice-to-have. Compose the
tension layer to sit comfortably either silent-in-the-mix or full-volume
against the base at any point in the loop, since the crossfade can catch
it at any bar.

**Everything else (Menu → Lobby → Gameplay, Day → Night, one-shot
stingers).** These are plain scene/state changes, not a real-time
gameplay signal — a straightforward ~1–2 second fade-out of the old
track and fade-in of the new one, handled entirely in code with two
`AudioSource`s crossfading. No sync requirement between these tracks at
all — any length, any tempo, they never need to play simultaneously.
Compose them independently.

**Jail** is the one exception worth a build-time decision rather than a
composing one: the cheapest option is no new composition at all — apply
a low-pass filter (`AudioLowPassFilter` on the jailed player's music
listener) to muffle whatever's already playing for them, evoking "behind
bars" for free. The fuller version is a dedicated `jail_ambient.wav`
loop that swaps in for just that one player (music is already
necessarily per-client, since each player's own tension layer already
depends on *their own* nearby AI state — a jailed player getting a
different base loop entirely is the same mechanism). Your call on which
is worth the time; flag which you'd rather do and I'll build the
corresponding playback logic to match.

## Musical identity — a starting point, not a mandate

Art-info.md already has this as an open to-do ("sketch the game's sonic
palette"); this is only here to not leave it blank. "Rob Everyone" is a
heist game where you're *also* working against your friends — comedic
tension more than genuine menace. A lean, slightly playful, low-stakes
heist-movie palette (think: pizzicato strings, muted brass, light
rhythmic percussion — Ocean's Eleven's smaller cousin, not a horror
score) probably suits the tension layer better than anything overtly
scary, since the actual threat on screen is a suburban Homeowner, not a
monster. Totally your call — this is just a starting point if you want
one, not a spec.
