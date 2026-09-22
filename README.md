# Rob Everyone

A first-person heist game where you're not on the same team as your
friends — everyone has their own quota, everyone's stealing from the
same houses, and everyone's a legitimate target. Steam multiplayer,
4–8 players, no fixed round count — an endless score-attack you keep
coming back to, not a match with a winner.

## Get the game

Playing requires the launcher — a one-time download that installs the
game and keeps it updated automatically from then on.

1. Download **[RobEveryoneLauncher.exe](https://github.com/joshuaclemons1/Rob-Everyone/releases/download/launcher-v1/RobEveryoneLauncher.exe)**.
2. Move it into its own folder wherever you want to keep it (Desktop is
   fine) — simplest to just leave it there permanently, don't bury it
   in Downloads.
3. Run **RobEveryoneLauncher.exe**.
4. First run: it downloads the current build automatically (may take a
   minute depending on your connection) and launches it when done. It
   also creates a **"Rob Everyone"** folder right next to itself — that's
   the actual game. Leave it alone; the launcher manages it.
5. Every time after that: just run the same **RobEveryoneLauncher.exe**
   again. It checks for a newer build on its own, installs it if you're
   behind, then starts the game — no manual download, ever again.

That's it — no zip to extract, no install wizard. Download the one
file, run it, done. Windows only for now.

You'll need **Steam** running (signed into your own account) to play —
that's how multiplayer lobbies work, you don't need to own anything.

**Having trouble?** If the launcher shows an error and you're sure
you're online, try running it again — a flaky connection mid-check is
the most common cause. Still stuck, or something in-game is broken? Say
so in [Issues](https://github.com/joshuaclemons1/Rob-Everyone/issues/new/choose)
or just tell Chayton directly.

## The gameplay loop

Each round you spawn into a neighborhood of houses surrounding a fenced
police compound with a single, deliberately contested exit. Break into
houses and loot what you find into a 5-slot hotbar, while:

- **Homeowners** patrol, get suspicious if they see you, and call the
  **police** if you linger.
- **Police** patrol, respond, chase, and catch — getting caught strips
  your loot and jails you, but you're not out of the round: another
  player can rescue you for a Cash bond, or you self-bail if nobody
  does. Caught with nothing stolen on you, though, and they just let
  you go.
- **Rivals** are fair game — sabotage items (Taser, Baseball Bat,
  Hammer, Tranquilizer Gun, Dynamite, Alarm Clock) let you stun and
  steal from another player directly, or even pick up their downed body
  and carry it somewhere inconvenient.
- **Traffic** on the road loop will flatten you if you're not careful,
  ragdoll and all.

Reach the exit (or survive to the timer) before you're caught, then sell
your loot and buy gear back at the Lobby's pawn shop between rounds.
Quota and prices grow every 3-round batch, Cash above quota gets wiped
at the batch boundary, and the last round of each batch is a harder
night round.

This is a rough alpha, actively being playtested and changed — expect
rough edges, and expect them to keep moving.

## For the dev team

Contributing, or setting the project up from source? Start with
**[Setup.md](Setup.md)** — engine version, branches, and the day-to-day
workflow live there, not here.
