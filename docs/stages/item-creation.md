# Item creation — turning Assets/Art/Items/ into spawnable loot

Covers the full pipeline for all 33 raw models in `Assets/Art/Items/`
(one per item, flat folder, no per-item subfolders) into actual
in-game loot: wrapping each as a prefab, creating its `ItemDefinition`,
and — the part that needs real design, not just repetition — how the
size-tiered `LootTable` system keeps a laptop from spawning on a
fridge-sized floor spot and vice versa.

Do this **before** Stage 4 Part 4 (interaction/inventory networking) —
that Part expects every `ItemDefinition`'s World Model Prefab to already
have a `NetworkIdentity`, which this doc is what actually builds.

---

## 1. Why exclusion needs multiple LootTable assets, not one

`LootSpawnPoint.cs` already takes a single `LootTable` reference and
rolls a random item from it — `LootTable.cs` itself is just a flat list,
no size/category concept at all. Rather than add that as new code
(SpawnPoint size enums, per-item size tags, filtering logic — a real
system with its own bugs to chase), the existing shape already solves
this with zero code changes: **make 3 separate `LootTable` assets, one
per physical size class, and point each `LootSpawnPoint` at whichever
one matches where it's physically placed** inside the house prefab. A
spawn point on a kitchen counter references `LootTable_Small` (or
`_Medium`); a spawn point on a cleared floor patch sized for an
appliance references `LootTable_Large`. A laptop *can't* roll onto the
fridge's spot because it was never in that table to begin with — that's
the exclusion list, expressed as "which table is this," not a runtime
filter.

**Size and dollar value are independent axes** — don't conflate them.
`GoldIngots` is small (fits anywhere a wallet would) but worth more than
most medium-sized appliances. Track both separately per item (the table
below does).

### Create the 3 LootTable assets

1. `Assets/Data/LootTable/` (where `HouseLootTable.asset` already lives)
   → right-click → **Create → Rob Everyone → Loot Table**.
2. Make three: `LootTable_Small`, `LootTable_Medium`, `LootTable_Large`.
3. Leave `Possible Items` empty on each for now — you'll drag
   `ItemDefinition` assets in as you create them (Section 3).
4. `HouseLootTable.asset` (the one `Real_House_01`'s `LootSpawnPoint`
   already references) can either become `LootTable_Medium` directly
   (rename it, keep its existing GUID so nothing needs re-wiring) or
   stay as a 4th "anything goes" table for spawn points you haven't
   categorized yet — your call, but renaming it to be one of the three
   real tables is less to maintain long-term.

### Wire each LootSpawnPoint to the right table

Every `LootSpawnPoint` already placed inside `Real_House_01`/
`Real_House_02` (and any house Zach adds later) needs its `Loot Table`
field set based on what kind of spot it's sitting in/on:

- **Small spot** (countertop corner, nightstand, desk surface) →
  `LootTable_Small`.
- **Medium spot** (a table's worth of surface, a shelf) →
  `LootTable_Medium`.
- **Large spot** (a cleared floor patch sized for an appliance, against
  a wall) → `LootTable_Large`.

If a house doesn't have a large-enough spot for a Large item to make
sense, just don't put a `LootTable_Large`-wired spawn point in it — the
exclusion works in both directions, a house can simply not offer certain
sizes at all.

---

## 2. Size category reference (all 33 items)

| Size | Items |
|---|---|
| **Small** | Wallet, Watch, Jewelry, CarKeys, DiamondRing, Sunglasses, Smartphone, GoldIngots, Coins, Books, Headphones, Camera, Keyboard, Mouse |
| **Medium** | Purse, Backpack, TableLamp, Toaster, CoffeeMachine, Blender, ComputerMonitor, Laptop, Speaker, Radio, Microwave |
| **Large** | Fridge, FridgeLarge, Washer, Dryer, Stove, StoveElectric, TelevisionModern, TelevisionVintage, Safe |

13 + 11 + 9 = 33, matches everything currently in `Assets/Art/Items/`.

---

## 3. Full item + price list

Values are calibrated against the *existing* `Laptop.asset` (`value:
50`) and `RoundManager`'s starting quota of `200` — these are small
relative numbers, not real-world dollar amounts. A single item
shouldn't trivially clear quota on its own except right at the top of
the Large tier (the Safe is deliberately the one item that gets close).

**World Model Scale**: only `Laptop` has a known-good value already
(`0.2, 0.2, 0.2`, from its existing prefab). Every other item's scale is
unknown until you see it in-scene at `(1,1,1)` first — start there and
adjust by eye, same as `Laptop`'s original setup must have. `Section 4`'s
batch tool leaves every item at `(1,1,1)` for exactly this reason.

This table is also the exact, hardcoded data
`ItemPrefabBatchTool.cs` (Section 4) runs from — the two are kept in
sync on purpose. `ComputerKeyboardMouse` from earlier drafts of this doc
is split into two separate items below (`Keyboard`, `Mouse`) so every
source file maps 1:1 to one output item, avoiding a combined-prefab
special case in the tool.

### Small ($7–$90)

| Item | Source file | Value |
|---|---|---|
| Car Keys | `Key11_with_tag.001.fbx` | $8 |
| Coins | `Prop_Coins.fbx` | $10 |
| Books | `books.fbx` | $10 |
| Sunglasses | `Sunglasses_01.obj` | $12 |
| Mouse | `computerMouse.fbx` | $7 |
| Keyboard | `computerKeyboard.fbx` | $8 |
| Headphones | `Headphones.obj` | $18 |
| Wallet | `Wallet.obj` | $20 |
| Watch | `Wirst Watch.obj` | $30 |
| Smartphone | `Smartphone.obj` | $35 |
| Camera | `Camera.obj` | $35 |
| Jewelry | `Jewelry.obj` | $40 |
| Diamond Ring | `diamond_ring.fbx` | $65 |
| Gold Ingots | `Gold_Ingots.fbx` | $90 |

### Medium ($15–$50)

| Item | Source file | Value |
|---|---|---|
| Toaster | `toaster.fbx` | $15 |
| Purse | `Purse_01.obj` | $18 |
| Table Lamp | `lampRoundTable.fbx` | $20 |
| Backpack | `Backpack.fbx` | $20 |
| Radio | `radio.fbx` | $22 |
| Blender | `kitchenBlender.fbx` | $25 |
| Coffee Machine | `kitchenCoffeeMachine.fbx` | $28 |
| Speaker | `speaker.fbx` | $32 |
| Computer Monitor | `computerScreen.fbx` | $35 |
| Microwave | `kitchenMicrowave.fbx` | $45 |
| Laptop | `laptop.fbx` (existing prefab, don't rewrap) | $50 *(already set)* |

### Large ($60–$250)

| Item | Source file | Value |
|---|---|---|
| Vintage Television | `televisionVintage.fbx` | $60 |
| Dryer | `dryer.fbx` | $70 |
| Washer | `washer.fbx` | $75 |
| Stove | `kitchenStove.fbx` | $80 |
| Electric Stove | `kitchenStoveElectric.fbx` | $85 |
| Modern Television | `televisionModern.fbx` | $90 |
| Fridge | `kitchenFridge.fbx` | $100 |
| Large Fridge | `kitchenFridgeLarge.fbx` | $130 |
| Safe | `Safe.obj` | $250 *(jackpot item)* |

---

## 4. Run the batch tool

`ItemPrefabBatchTool.cs` (`Assets/Scripts/Editor/`) automates the
mechanical two-thirds of this: for each of the 33 items in the price
list above, it finds the matching source model, adds a fitted `Box
Collider` + `Network Identity`, saves it as a prefab in
`Assets/Prefabs/Items/`, and creates its `ItemDefinition` in
`Assets/Data/Items/` with the name/value from the table pre-filled and
`World Model Prefab` already wired. `Laptop` is skipped — it already has
a working prefab/`ItemDefinition` from before this tool existed.

1. `Rob Everyone → Batch Create Loot Items` (menu bar).
2. Read the Console output: it reports exactly how many prefabs and
   `ItemDefinition`s were created, how many were skipped (already
   existed — safe to re-run any time, nothing gets duplicated or
   overwritten), and logs a specific error for anything that failed
   rather than failing silently or partway through.
3. If an item is reported as "couldn't find a source model," the most
   likely cause is a filename that doesn't exactly match this doc's price
   table (e.g. a file got renamed at some point) — check the exact
   filename in `Assets/Art/Items/` against the table above.

What it deliberately **doesn't** do — still yours to finish by hand,
each one fast and easy to verify visually:

- **World Model Scale tuning** — every item is created at `(1,1,1)`;
  open each `ItemDefinition`, look at the prefab in the preview, adjust
  the scale until it looks right.
- **Adding each `ItemDefinition` to `ItemCatalog`** — multi-select all
  the new assets in `Assets/Data/Items/`, drag them into `ItemCatalog`'s
  list in one action.
- **Adding each `ItemDefinition` to the correct-size `LootTable`** —
  this is the actual size/exclusion decision from Section 1, drag each
  into `LootTable_Small`/`_Medium`/`_Large` based on the category table
  in Section 2.
- **Registering each new prefab as a Spawnable Prefab** on the
  `NetworkManager` (see the note below) — the batch tool can't reach
  into a scene's `NetworkManager` component from an Editor menu command
  safely, so this one's manual too.

### The old manual per-item pipeline

Kept below for reference/in case you ever add a single one-off item by
hand later — the batch tool is what you actually want for all 32 at once.

`Laptop` already has a working prefab + `ItemDefinition` — open
`Assets/Data/Items/Laptop.asset` and its referenced prefab first as a
worked example before doing the rest by hand.

1. **Drag the source file** (`.fbx`, or `.obj` — Unity imports both)
   from `Assets/Art/Items/` into the Hierarchy of a spare/scratch scene
   (or directly into the Project window to make a prefab without a
   scene round-trip, whichever you're more comfortable with).
2. **Add a Collider** — a `Box Collider` is fine for all of these, Unity
   usually auto-fits a reasonable size on Add Component, adjust if it's
   obviously wrong (e.g. way bigger than the visible mesh).
3. **Add a Network Identity** — required per
   [stage4-multiplayer-mirror.md](stage4-multiplayer-mirror.md) Part 4:
   "every `ItemDefinition`'s World Model Prefab needs a `Network
   Identity` component *on the prefab asset itself*." `PickupItem` does
   **not** need to be added by hand — `LootSpawnPoint` adds it at
   runtime automatically if it's missing.
4. **Drag it into `Assets/Prefabs/Items/`** (create that folder) to make
   it a real prefab asset. Delete the scene instance afterward if you
   used a scratch scene.
5. **Create the `ItemDefinition`**: right-click in `Assets/Data/Items/`
   → **Create → Rob Everyone → Item Definition**. Name it to match the
   item. Set:
   - **Item Name**: the display name (e.g. "Gold Ingots")
   - **Value**: from the table above
   - **Icon**: leave empty for now (a real HUD icon is separate art work,
     `todo.md` already tracks "loot item variety" needing icons — this
     doc is about getting the *world models* spawnable, not the hotbar
     icon art)
   - **World Model Prefab**: the prefab you just made in step 4
   - **World Model Scale**: `(1,1,1)` to start, tune once you see it
     in-game
6. **Add it to `ItemCatalog`** (`Assets/Data/` — create the asset via
   **Create → Rob Everyone → Item Catalog** if it doesn't exist yet) —
   required for multiplayer sync (`PlayerInventory` resolves carried
   items by name through this catalog, per
   stage4-multiplayer-mirror.md).
7. **Add it to the correct-size `LootTable`** from Section 1, based on
   the size category table in Section 2.

### Register the item's prefab as a Spawnable Prefab

Same requirement as houses/cars/existing items — on the
`NetworkManager`'s **Spawnable Prefabs** list, add every new item prefab
you make. Easy to forget one; if a specific item never seems to spawn
once Stage 4 testing starts, this is the first thing to check.

---

## 5. Test

Once at least a few items from each size tier are wired up: play a round,
walk through a house, confirm each `LootSpawnPoint` only ever rolls
items from its assigned size tier (a countertop spot never produces a
fridge, a floor spot never produces a wallet). Confirm `Cash`/hotbar
values match what's in the price table above when you pick something up.
