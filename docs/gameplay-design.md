# Gameplay Design

Detailed design for the core loop: loot/economy, capacity, the quota-batch
structure, the shop, jail/bail, sabotage, and movement. Written up from a
design session (2026-09-01) before any of this is built — this is a design
reference, not a build guide. Cross-reference:
[plan.md](plan.md) for stage sequencing, `Assets/Scripts/` for what's
actually implemented today.

**Status:** none of this is implemented yet beyond the basics already in
`PlayerInventory.cs` (unlimited carry, single running total) and
`RoundManager.cs` (single fixed quota, resets each round). This doc
describes a materially different, richer system — see "Gap vs. current
code" at the bottom before starting implementation.

---

## Loot items

Items have a name/type, but **value is randomized per pickup** within a
range per item type (e.g. Jewelry: $60–140), not a fixed value per type —
keeps the same house from feeling identical run to run and makes
memorized "optimal routes" less reliable.

## Carry capacity

- **5 shared slots**, used for both stolen loot and any shop-bought items
  brought into the round (sabotage tools, etc.) — real tradeoff between
  carrying more sabotage gear vs. more loot capacity.
- **Prison Wallet — a 6th, separate slot.** A safe pocket immune to both
  other players and the police:
  - You may only **place** an item into it during the robbing phase (not
    swap/replace once filled — first item in is locked for the round).
  - You can only **retrieve** its contents during the shop phase.
  - If caught mid-round, you lose everything in your 5 normal slots, but
    the Prison Wallet item's value is still safely banked.
- **"Backpack" shop item** — a single-round consumable purchase that adds
  +2 slots for that round only. Expensive by design (a real spending
  tradeoff, not a cheap default buy). Capacity is otherwise always fixed
  at 5 + Wallet for every player, permanently — no permanent upgrades.

## Cash, selling, and the shop

Two distinct value states, not one:

1. **Carried items** — raw loot/purchases sitting in your 5 slots (or the
   Wallet). At risk: lost if caught (except the Wallet), can potentially
   be stolen via PvP sabotage (see below).
2. **Cash balance** — a safe, persistent number. Carried items convert to
   Cash only by **selling them in the shop phase**. Cash cannot be
   affected by other players or the police once banked. Cash is what you
   spend on shop purchases.

Cash accumulates across a **3-round batch** (see Quota Batches below) —
it is not reset every single round, only at the batch boundary.

## Quota batches & progression

Rounds happen in **batches of 3**:

- The quota target is fixed for all 3 rounds in a batch.
- Your Cash balance (from selling loot across those 3 rounds) accumulates
  toward that one batch quota — you don't need to hit the quota every
  single round, just by the time the batch ends.
- **At the end of the batch:** if your Cash balance is *above* the quota,
  the surplus is deleted — you must spend down to (or below) the quota
  before the batch ends, or lose the difference. This is a deliberate
  anti-hoarding pressure: always be spending, not banking indefinitely.
- The **next batch's quota is higher**, and **shop item prices increase**.
  Roughly every time the quota steps up, **2 or so new, better shop items
  unlock** — items are gated behind quota tier, not all available from the
  start. (Exact quota curve, price curve, and which items unlock at which
  tier are not decided yet — needs its own balancing pass once there's a
  real item list to gate.)
- Quota also has an **individual add-on**: whatever you personally spend
  on sabotage items in a given batch adds to what you personally need to
  hit for that batch's quota. Sabotage is powerful, but it isn't free —
  the escalating difficulty comes from the batch curve *and* from your
  own spending choices.

## Jail & bail

There are **two distinct jail situations**, with different stakes:

### Mid-round catch (police catches you during a round)

- You lose everything in your 5 normal carry slots (loot + any items you
  brought in that round) — **except** the Prison Wallet, which is safe.
- Your **Cash balance is untouched** (it's already banked, separate from
  carried items).
- You're jailed **for the rest of that round** — but another player can
  break you out **mid-round**, for a **bond price** (paid in Cash), which
  lets you rejoin that same round in progress.
- Breaking someone out mid-round should be **harder than robbing a normal
  house, but still clearly worth the risk** most of the time — real
  difficulty, not a coin-flip. (The jail is physically inside the guarded
  police station per the map sketch, so a rescue is its own small
  stealth challenge past the same Police AI, not a freebie.)

### End-of-batch quota failure

- If your Cash balance is below quota when a 3-round batch ends, you're
  jailed **at the start of the next batch's first round** (the round
  where quota also just went up).
- You **keep your Cash balance** (failing doesn't erase progress — the
  penalty is being locked out of earning more, not losing what you had).
- Other players can break you out for a **bounty** — a **higher** bond
  price than the mid-round version — equal to **1/3 of the current
  (new, harder) quota**. This gives other players a real incentive to
  come get you rather than leaving you to rot.
- **Self-bail:** if nobody rescues you after enough time/rounds pass,
  you're auto-released on your own (exact threshold/rounds not decided
  yet) — a safety net so no one is ever fully dependent on someone
  else's goodwill to keep playing.

### Rescue risk level

Deliberately tuned to sit **between** a normal house heist and a trivial
errand — real risk (guarded location, same Police AI), but reliable
enough that rescuing is *almost always* worth attempting for the bond/
bounty payout. Exact tuning (guard density near the jail, catch
range/timing) needs playtesting once Police AI patrol logic exists for a
police-station interior.

## Win condition

**No formal winner.** This is deliberately endless score-attack/freeplay
— batches keep escalating (quota + shop tier) for as long as the group
wants to keep playing; whoever has the most Cash when the group decides
to stop is just bragging rights, not a coded end-game check. (This
replaces the old "5 rounds, highest total wins" idea from the original
pitch — no fixed round count or results screen needed.)

## Sabotage items

Mostly **direct PvP** (used on another player), with a handful of
**environmental** items (trick the world into working against a rival,
e.g. an alarm clock thrown near a rival triggers a nearby Homeowner's
alert without the rival actually doing anything wrong).

Structured as **escalating tiers, with multiple items per tier** — not
one single "best" item per power level, so there's real variety. Every
item should have both a **benefit and a drawback**, e.g.:

- Taser — quick PvP stun, but has a **recharge time** before reuse.
- Bat — knockout-tier PvP, but has limited **durability** (breaks after N
  uses).
- Alarm Clock — environmental, frames a rival via a nearby Homeowner, but
  is **single-use**.
- Hammer — could be PvP or environmental (e.g. breaking a shortcut) —
  exact role still open, pick whichever creates the least overlap with
  the other three once the full tier list is drafted.

Exact full item list, tier breakdown, and numeric tuning (stun duration,
recharge times, prices) are **not decided yet** — this is a placeholder
structure (benefit+drawback per item, multiple items per tier) to build
the real list against later, likely alongside Stage 6.

## Movement

Beyond the current walk + mouse-look (`FirstPersonController.cs`):

- **Sprint** — faster movement, but noisier/riskier — should make
  Homeowner suspicion build faster (louder) as a real tradeoff, not a
  strictly-better option.
- **Crouch** — slower movement, but quieter/stealthier — should reduce
  Homeowner suspicion build rate and/or detection range, giving a real
  stealth option beyond just staying out of the vision cone entirely.
- **Jump** — standard jump added.
- **Movement-tech (bhop-style) advanced mobility** — similar to Source
  engine bunnyhopping: chaining jumps with air-strafing lets a player who
  has learned the technique move faster than sprinting alone. Skill
  ceiling reward, not a menu-toggle ability — the "drawback" is inherent
  (it requires making the noise of sprinting/jumping repeatedly, so it's
  not stealthy), so it's a genuine tradeoff rather than a free upgrade.

## Map & player scaling

- **Target player count:** starts at 2 for first multiplayer testing, but
  the design goal is **4 players minimum, up to 8 max** — more players is
  more chaos, which is the actual point of the game.
- **Map size stays fixed** regardless of player count (the ~10-house ring
  + central compound from the sketch, not a dynamically-scaling map).
  More players just means more competition over the same fixed pool of
  houses/loot — that contention *is* the chaos, not a bigger map.

## Shop phase

- **Ready-up style, not a countdown timer.** No shared clock — instead,
  each player must **physically walk to and stand in a specific spot** in
  the shop area to mark themselves ready, and can **walk away to change
  their mind** before the round actually starts. More diegetic than a
  UI "Ready" button, fits the first-person interaction style already used
  elsewhere (E-to-interact).
- The next round starts once all players are standing in their ready
  spot simultaneously.

## HUD needs

On top of the existing money/quota/timer:

- **Carry slots readout** — visual list of the 5 slots + the Prison
  Wallet slot, showing what's in each, so players can see what they'd
  lose if caught.
- **Active sabotage cooldowns** — recharge/durability/uses-left icons for
  whatever sabotage items a player is currently carrying.
- **Rival's approximate status** — some signal about other players (exact
  trigger not decided yet — e.g. a ping when a rival is spotted/alerted
  by a Homeowner or Police, vs. showing their live Cash total) — adds
  information warfare without full player tracking. Needs its own design
  pass on the exact trigger condition before building.

## Open questions (not decided yet)

- Exact quota curve and shop price curve per batch tier.
- Exact shop item list, per-tier unlocks, and sabotage item numeric
  tuning (stun durations, recharge times, prices).
- Exact self-bail threshold (how many rounds before auto-release).
- Exact trigger condition for the "rival's approximate status" HUD ping.
- Whether the Hammer is PvP or environmental (or both).

## Gap vs. current code

This is a substantially different system from what's implemented today:

- `PlayerInventory.cs` currently tracks one unlimited running total with
  no concept of slots, a Wallet, or a Cash/carried-items split.
- `RoundManager.cs` currently has a single fixed quota checked at the end
  of every individual round, with no batch concept, no persistence across
  rounds, and no shop/sell phase.
- None of jail/bail, sabotage items, sprint/crouch/bhop movement, or the
  ready-spot shop phase exist in code yet.

Per [plan.md](plan.md)'s build order, none of this should be started
until the single-player core loop (Stage 3) and basic multiplayer (Stage
4–5) are proven — this doc exists so the design is ready to build from
once those stages are actually reached (roughly Stage 6–7).
