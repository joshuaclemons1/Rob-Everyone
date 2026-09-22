# Why sessions stall after batch 1 — research against the reference games

Direct response to a real playtest signal: sessions are ending after
5–10 minutes, right around the end of the first quota batch, before
players lose interest. This isn't a "needs more polish" problem — it's
specific enough to actually diagnose. Deep dive into Super Battle Golf
and Gamble With Your Friends (two of this project's own four named
references) to find out why, with a direct comparison against Rob
Everyone's actual current loop.

## The headline finding

**Gamble With Your Friends — one of this project's own reference
games — has the *exact* failure mode Rob Everyone is showing right
now**, and its own critics name the cause specifically:

> "The most persistent criticisms concern the five-minute timer,
> **escalating quotas**, repetitive casino floors, weak cooperation,
> limited replayability..."

> "With little variety between runs and most of its tricks revealed
> quickly, it's hard to see much reason to return once you've had your
> fill."

> "You very quickly run out of new stuff, and it gets repetitive."

Rob Everyone's current batch structure is, mechanically, very close to
this weaker pattern: `GameFlowManager.HandleRoundEnded` grows quota by
a flat 50% every batch (`quotaGrowthMultiplier = 1.5f`, compounding,
`currentQuota = 200` to start — batch 2 needs 300, batch 3 needs 450)
and otherwise **the map, the tools, and the round structure are
identical batch to batch**. That's the "escalating quota, nothing else
changes" shape critics call out as the specific reason people stop
playing Gamble With Your Friends. Confirmed timing data backs this up:
`RoundManager.roundDuration` defaults to 5 minutes, and per the report
a whole *first batch* (3 rounds) wraps in 5–10 minutes — meaning the
moment-to-moment loop is already working (short, efficient rounds,
matching the fast-pacing both reference games are praised for) and the
drop-off is happening specifically **at the batch boundary**, not
mid-round. Players are reaching the exact moment the game asks them to
sign up for "the same thing again, but harder" and declining.

Super Battle Golf, the *other* reference game, does not have this
problem — reviewers specifically credit its course variety with
*preventing* the staleness Gamble With Friends is criticized for. The
difference between the two is instructive and maps directly onto
concrete, actionable changes below.

## What Super Battle Golf does that Gamble With Friends doesn't

- **Zero downtime, low cost for a mistake.** "The pacing is fast enough
  that even mistakes rarely feel punishing for long, because the next
  opportunity arrives almost immediately." Simultaneous play, no
  waiting your turn.
- **An explicit, mechanical comeback system — not just "more chaos."**
  Hitting another player with a club grants the *attacker* a speed
  boost, and there's a named comeback-points system that specifically
  rewards players who are further behind first place. The game's own
  design deliberately makes aggression the tool a losing player reaches
  for to catch up, not just a way to grief whoever's already winning.
- **Structural variety, not just numeric difficulty.** Three distinct
  nine-hole sets; reviewers say this is specifically what keeps runs
  from feeling repetitive even across many sessions.
- **Goal-tied unlocks that push players toward variety.** "Some
  cosmetic things can only be unlocked after completing certain
  goals... forces you to try out different ways to play."
- The one real criticism found is **solo play being boring** ("without
  any path to mastery, individual play quickly devolves into
  boredom") — not relevant to Rob Everyone, which has no solo mode, but
  worth knowing the genre's actual weak spot is elsewhere.

## What Gamble With Friends does well (worth keeping in mind, not just criticizing)

- **17 minigames across 4 themed floors, randomized layout every
  run** — real variety *within* a single playthrough, which is why
  the moment-to-moment session still gets called fun even by critics
  who dislike the game's long-term legs.
- **The actual draw is social negotiation under shared risk**, not the
  gambling mechanics themselves: "convincing friends to trust an
  absurdly risky strategy and proving them wrong is where the game
  truly shines." Worth remembering for Rob Everyone too — the
  PvP/sabotage layer is at its best when it's producing an argument
  between friends, not just a stat check.
- Its own critics separately note **"weak cooperation"** and that "the
  gameplay itself does not have much substance" beneath the social
  layer — the social dynamic alone isn't enough to carry a game long
  after the novelty wears off, which is exactly what seems to be
  happening in Rob Everyone's own playtests right now.

## Direct recommendations, prioritized

### 1. Make sabotage an explicit comeback tool, not just griefing (#60)

Right now, using a sabotage item is a pure Cash cost with only an
*indirect* benefit (slowing a rival down) — nothing like Super Battle
Golf's direct "attacking rewards the attacker" design. Worth a real
look at giving a landed sabotage hit some direct payoff, possibly
scaled by how far behind the attacker actually is on quota progress
(mirroring the "comeback points" concept precisely) — this single
change could do a lot to make batch 2+ feel like "now I get to use the
tools I bought to actually catch up" instead of "now I have to hit the
same higher number again."

### 2. Give batches structural variety, not just a bigger number (#61, #62)

This is the core of the diagnosis above. A few concrete levers already
exist or are already planned:

- **#59** (just filed) — an alternate map layout is already on the
  table for *testing* purposes. Worth reconsidering its scope in light
  of this research: rotating which layout a batch plays on (not just
  having a second layout to compare in the Editor) is a very direct,
  cheap way to make "batch 2" feel like a genuinely different
  experience rather than the same map with a harder number attached.
- **`ShopShelfItem.unlockBatch`** already exists — new sabotage tools
  *do* unlock at higher batches (proposed progression: Taser at 1,
  Bat/Alarm Clock at 2, Hammer/Tranq Gun at 3, Dynamite at 4). This is
  genuinely the right idea already partially built — the likely gap is
  that a session ending at the close of batch 1 never actually *sees*
  what's waiting at batch 2, so the hook isn't landing. Worth surfacing
  the next batch's unlock at the end-of-round-3 screen explicitly
  ("Batch 2 unlocks: Bat, Alarm Clock") rather than leaving it to be
  discovered by returning to the shop. (#61)
- Longer-term, a rotating modifier per batch (night round already
  exists for round 3 specifically — worth asking whether that pattern
  could extend earlier/further) is the same idea taken further. (#62)

### 3. Reconsider quota growth as the *only* difficulty lever (#63)

1.5x compounding every batch is steep on its own, and it's the exact
mechanic named as a top complaint in a game this project is directly
modeled on. Not necessarily "make it easier" — more "don't let it be
the *only* thing that changes," per #2 above. If structural variety
lands, the raw number climbing may need far less tuning to still feel
fair.

### 4. The social/PvP layer is the actual draw — protect it explicitly

Both games' strongest praise is about player-to-player dynamics
(Gamble With Friends' persuasion-under-risk, Super Battle Golf's
voice-chat-driven reactions), and both games' weak points show up once
that social novelty wears thin and there's nothing else underneath it.
Rob Everyone already has the structural pieces (competitive quotas,
sabotage PvP, Jail & Bail, proximity voice) — the recommendation isn't
to add something new here, it's to make sure #1/#2 above are framed
around *creating moments between players* (a landed sabotage hit that
matters, a batch that looks/plays differently enough to talk about)
rather than just tuning numbers.

## What NOT to conclude from this

This is not a "the core loop is broken" finding — round-to-round
pacing is already in the range both reference games are praised for
(short, efficient, low-downtime). The problem is specifically the
*batch boundary* — the exact moment Rob Everyone currently offers
players the least new information to justify continuing. That's a
narrower, more fixable problem than a full loop redesign.

## Where to look

- `Assets/Scripts/Core/GameFlowManager.cs` — `quotaGrowthMultiplier`,
  `HandleRoundEnded`'s batch-boundary logic, the exact point a session
  currently offers nothing new.
- `Assets/Scripts/Shop/ShopShelfItem.cs` — `unlockBatch`, the
  already-built (but under-surfaced) batch-to-batch variety hook.
- #59 — alternate map layouts, worth reconsidering as a rotation
  mechanic in light of this research, not just an Editor testing copy.
- `Assets/Scripts/Sabotage/SabotageUseController.cs` — where a direct
  comeback reward for landing a hit would hook in.
