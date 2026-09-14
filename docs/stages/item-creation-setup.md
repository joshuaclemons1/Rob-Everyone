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

**Not covered here**: the Prison Wallet (a 6th, separate slot that holds
1 item of any size/value, immune to whatever happens to the other 5 when
caught) — deliberately not built in this pass since it touches
jail/catch behavior and selling logic well beyond loot spawning. Built
later as part of the Tab/steal screen work — see
[issue #32](https://github.com/joshuaclemons1/Rob-Everyone/issues/32).

---

## 0. InventorySize — bulky items cost more than one slot

`ItemDefinition.InventorySize` (default `1`) is how many of
`PlayerInventory`'s 5 slots one item costs to carry — a `Watch` costs 1,
a `Large Fridge` costs 4, a `Safe` costs all 5 (the entire inventory, on
purpose — the jackpot item should feel like it takes everything you've
got to haul out). `AddItem` looks for that many *consecutive* free
slots, not just any one free slot, and fails (leaving the item in the
world) if it can't find a big enough contiguous run — a full inventory
with 3 free slots scattered between other items still can't fit a
4-slot fridge.

Under the hood this is still a single `SyncList<string>` (one name per
slot, unchanged schema) — a bulky item writes its name into its first
("head") slot and a reserved sentinel into every slot after that, so
`TotalValue` counts it exactly once regardless of how many slots it
spans, while the hotbar UI still shows the item's icon repeated across
every slot it occupies (the same visual language inventory-Tetris games
use for a multi-cell item). None of this needs touching by hand — it's
already wired into `PlayerInventory.cs`, and `ItemPrefabBatchTool`
(Section 4) sets the right `InventorySize` per item automatically.

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
- **Creating `ItemCatalog` itself, then adding each `ItemDefinition` to
  it** — this asset doesn't exist yet (only the `ItemCatalog.cs` script
  does). Right-click in `Assets/Data/` → **Create → Rob Everyone → Item
  Catalog**, then multi-select all the new assets in
  `Assets/Data/Items/` and drag them into its list in one action. **Then
  wire it up**: select the `Player` prefab, find `PlayerInventory`'s
  **Catalog** field, and drag this asset in — without that, item pickups
  won't resolve to anything (`PlayerInventory` looks names up through
  this exact reference, see its own class comment).
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
   Identity` component *on the prefab asset itself*."
4. **Add `PickupItem`** too — also has to be baked into the prefab
   asset, not left for `LootSpawnPoint` to add at runtime. It technically
   *can* be added after the fact, but `NetworkIdentity.Awake()` already
   scans and caches this object's networked components by the time that
   would happen, leaving `PickupItem` permanently unable to tell it's
   spawned — it'll throw the instant anything tries to interact with it,
   and Mirror will disconnect whoever triggered it. (This was a real bug
   here, not a hypothetical — `LootSpawnPoint.cs` now fails loudly with a
   clear error instead of silently adding it, specifically so this can't
   happen silently again.)
5. **Drag it into `Assets/Prefabs/Items/`** (create that folder) to make
   it a real prefab asset. Delete the scene instance afterward if you
   used a scratch scene.
6. **Create the `ItemDefinition`**: right-click in `Assets/Data/Items/`
   → **Create → Rob Everyone → Item Definition**. Name it to match the
   item. Set:
   - **Item Name**: the display name (e.g. "Gold Ingots")
   - **Value**: from the table above
   - **Icon**: leave empty — turned out not to be needed at all, the
     hotbar renders each item's own 3D world model as its preview
     instead of a separate icon sprite
   - **World Model Prefab**: the prefab you just made in step 5
   - **World Model Scale**: `(1,1,1)` to start, tune once you see it
     in-game
   - **Inventory Size**: `1` unless the item is genuinely bulky (see
     Section 0) — most things stay at the default
7. **Add it to `ItemCatalog`** (`Assets/Data/` — create the asset via
   **Create → Rob Everyone → Item Catalog** if it doesn't exist yet) —
   required for multiplayer sync (`PlayerInventory` resolves carried
   items by name through this catalog, per
   stage4-multiplayer-mirror.md).
8. **Add it to the correct-size `LootTable`** from Section 1, based on
   the size category table in Section 2.

### Register the item's prefab as a Spawnable Prefab

Same requirement as houses/cars/existing items — on the
`NetworkManager`'s **Spawnable Prefabs** list, add every new item prefab
you make. Easy to forget one; if a specific item never seems to spawn
once Stage 4 testing starts, this is the first thing to check.

---

## 4b. Sabotage items — same tool, separate menu command

`ItemPrefabBatchTool.cs` also has **`Rob Everyone → Batch Create
Sabotage Items`**, a second `[MenuItem]` sharing all the same prefab/
`ItemDefinition` creation logic as Section 4 above, but reading from a
separate `SabotageItems` array instead of `Items`. Source models live in
the same flat `Assets/Art/Items/` folder (including `.glb` now, not just
`.fbx`/`.obj` — `FindSourceModel` checks all three extensions).

**These are not loot** — gameplay-design.md's Sabotage items section
describes shop-purchased/PvP-won tools (taser, hammer, bat, alarm
clock), not things found lying in a house. Do **not** drag any of these
into `LootTable_Small`/`_Medium`/`_Large`. The batch tool only builds
the mechanical pieces (prefab + `ItemDefinition`, same as any loot item)
— actual sabotage behavior (stun, PvP theft window, durability,
recharge, the alarm clock's environmental trigger) is separate, not-yet-
built Stage 6 gameplay code. Values assigned are rough placeholders,
since gameplay-design.md's own "Open questions" leaves sabotage pricing
undecided.

## 5. Test

Once at least a few items from each size tier are wired up: play a round,
walk through a house, confirm each `LootSpawnPoint` only ever rolls
items from its assigned size tier (a countertop spot never produces a
fridge, a floor spot never produces a wallet). Confirm `Cash`/hotbar
values match what's in the price table above when you pick something up.
