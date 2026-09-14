# Known Bugs

A log of real, reproducible-or-suspected bugs — not the place for design
feedback or feature requests (those go in [docs/todo.md](docs/todo.md)
instead, or just tell Chayton). Useful any time, especially during a
local playtest where the person who hits a bug isn't the one who'll fix
it — a good report here is often the difference between "fixed in five
minutes" and "couldn't repro, closed."

**To file one**: copy the template below into a new entry under
"Open bugs," fill in what you actually know (leave a field blank rather
than guessing), and give it a short, specific title.

---

## Template

```
### <short specific title>

- **Reported by**:
- **Date**:
- **Branch / commit** (`git log -1 --oneline`, or the alpha version if
  you're playing a release build, not running from source):
- **Build type**: Editor Play / ParrelSync clone / Standalone build
- **Scene**: MainMenu / Lobby / SampleScene / Intro
- **Multiplayer context**: solo, or how many players — were you host or
  a client? Does it happen for everyone or just one person?
- **Steps to reproduce**:
  1.
  2.
  3.
- **Expected behavior**:
- **Actual behavior**:
- **Frequency**: every time / sometimes (roughly how often) / happened
  once
- **Console errors / Player.log**: paste the actual error text if the
  Console (Editor) or `Player.log` (Standalone build — Unity's docs have
  the path per OS) shows one. A screenshot of the Console works too.
- **Screenshots / video**: attach if it's visual or hard to describe
- **Workaround** (if you found one):
- **Status**: Open
```

---

## Open bugs

### Sabotage cooldowns don't reset between rounds

- **Reported by**: Chayton (code-review read-through, not a playtest report)
- **Branch / commit**: `jclem's-branch`, surfaced reviewing the Stage 6
  Phase 1/2 commits
- **Build type**: Editor Play
- **Steps to reproduce**: fire a Taser near the end of a round (while
  its cooldown is still active), let the round end and a new one start.
- **Expected behavior**: cooldowns reset at the start of a new round.
- **Actual behavior**: `SabotageUseController.nextReadyTime` keys off
  `Time.time`, which runs continuously across the Lobby round-trip — the
  Taser can still show on cooldown at the start of the next round.
- **Frequency**: every time, if the timing lines up
- **Workaround**: wait it out, or start the next round later
- **Status**: Open — low impact, also tracked in
  [docs/todo.md](docs/todo.md)'s code-review nits

*(add more above this line as they turn up)*

---

## Fixed

Move an entry here once it's actually fixed, with the commit that fixed
it — don't just delete it, so the same thing doesn't get "discovered"
and re-investigated later.

*(none logged yet)*
