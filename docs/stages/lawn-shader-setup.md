# Mowed-lawn Shader Graph setup

A procedural shader that gives the yard a "recently mowed" look — alternating
light/dark green stripes, using the locked palette colors from
[art-info.md](../art-info.md), plus real bump depth from a free normal map so
it doesn't read as a flat color. No texture file to hand-paint; stripe
width/angle/colors are all Inspector-tunable afterward.

## 1. Get a normal map for bump depth

The stripes come from the shader itself — this normal map just gives the
lawn's surface some texture/depth instead of being perfectly flat. Doesn't
need to be stylized since it's not driving color, just surface bumpiness:

- [FreePBR — Grass #1](https://freepbr.com/product/grass-1-pbr-material/)
  (free) or any similar free grass material — you only need the **Normal**
  map from the download, ignore the color/albedo map entirely.
- Import the normal map PNG into `Assets/Art/Environment/Textures/` (create
  that folder), select it in the Project window, and in the Inspector set
  **Texture Type → Normal Map**, then **Apply**.

## 2. Create the Shader Graph

1. In the Project window, right-click `Assets/Art/Environment/` → **Create
   → Shader Graph → URP → Lit Shader Graph**. Name it `LawnStripes`.
2. Double-click to open it in the Shader Graph editor.

## 3. Build the stripe pattern

1. Add a **Position** node (Space: World).
2. Add a **Split** node, feed Position into it — take the **R** (X) or
   **B** (Z) output depending on which direction you want the stripes to
   run (whichever matches how the yard is oriented).
3. Add a **Divide** node: input = that X or Z value, divide by a **Float**
   property named `Stripe Width` (default `2` — that's 2 world units per
   stripe, tune later against the yard's actual size).
4. Feed the Divide result into a **Fraction** node (gives a repeating 0→1
   ramp), then into a **Step** node with **Edge = 0.5** — this turns the
   ramp into a hard on/off pattern (the actual stripes).
5. Add a **Lerp** node: **T** = the Step output. Set **A** and **B** to two
   **Color** properties, `Stripe Color A` and `Stripe Color B`, defaulted
   to the locked grass hexes from `art-info.md`:
   - `Stripe Color A` = `#6FCF97` (the locked "Grass / ground" color)
   - `Stripe Color B` = a slightly darker hand-picked variant, e.g.
     `#4FA870` — same hue, just darker, so it reads as "same lawn, mowed
     the other way" rather than two different colors.
6. Connect the Lerp's output to the main **Fragment** node's **Base
   Color** (Lit target).

## 4. Add the bump depth

1. Add a **Sample Texture 2D** node, set its **Texture** default to the
   normal map imported in step 1, and set its **Type → Normal**.
2. Add a **Normal Strength** node between the sample and the output if you
   want to dial the bump intensity down (grass shouldn't look aggressively
   bumpy at this style level — start around `0.3–0.5`).
3. Connect to the Fragment node's **Normal (Tangent Space)** slot.
4. On the Sample Texture 2D's UV input, add a **Tiling And Offset** node
   (Tiling e.g. `20, 20`) so the normal map repeats fine-grained across the
   whole yard instead of stretching over one giant plane.

## 5. Save and apply

1. **Save Asset** in the Shader Graph editor (top-left).
2. In the Project window, right-click `LawnStripes` → **Create → Material**
   (or drag the Shader Graph directly onto your yard's ground object — Unity
   auto-creates a material either way).
3. Drag that material onto the yard ground plane in `Real_House_01` (and
   any other house prefab's yard).
4. Select the material, tune `Stripe Width` and the two stripe colors in
   the Inspector until it reads right at actual in-game camera distance —
   what looks right zoomed into the Shader Graph preview sphere is often
   too fine/coarse at real scale.

## Notes

- This material can be reused across every house's yard — same shader,
  same tunable properties — so you only build it once.
- If the stripe direction looks wrong on a rotated house instance, that's
  because it's driven by **world** position (so stripes stay aligned
  across the whole map, not per-object) — this is intentional, not a bug.
  If you'd rather each yard's stripes follow that house's own rotation,
  swap the **Position (World)** node for **Position (Object)** in step 3.
