# Ragdoll carry — pick up, carry, and throw downed players

**Unbuilt** — this is the plan. Code-first (in order, each Part has a
🔴 Rest Point), then Editor wiring at the end.

## The design (settled)

- **Pick up:** `E` on *any* ragdolled rival — car hit or sabotage stun,
  the whole time they're on the ground, not just the steal window. A
  "Carry <name>" prompt.
- **While carrying:** near-normal — walk/run fine, items and `E` still
  work — but **no bhop** (holding a body, you can't be zooming), and
  **if the carrier gets ragdolled, they drop the body instantly.**
- **Throw:** aim where you're looking, **hold to charge** force, release
  to launch. The body re-ragdolls and flies.
- **Carried players are protected:** while carried, **nobody can steal
  from them** (including the carrier). Carrying is a grief/relocation
  toy — moving a rival off the exit, dumping them in traffic or the
  police cone — *not* a way to strip-mine someone's inventory or pass a
  body around. (The carrier still keeps whatever one item they legitimately
  took via the normal steal window *before* picking the body up.)
- **A carried player can't get up.** Their ragdoll is held for the whole
  carry, plus a short **`carryReleaseStun`** after they're dropped or
  thrown before they can stand.
- **Ragdoll physics still apply while carried** — limbs/head flop in
  your hands (sack-of-potatoes). Only the hips are pinned to the carry
  point.

Networking: server-authoritative. The carrier's Command grabs; the
server tracks `carriedBy` (a SyncVar on the victim) and drives the
victim's transform to follow the carrier. The ragdoll itself stays the
existing client-side cosmetic.

---

## Part 1 — `Carryable` (on the victim / every Player)

New `Assets/Scripts/Player/Carryable.cs`. One per Player.

```csharp
using Mirror;
using UnityEngine;

namespace RobEveryone.Player
{
    // The "can this downed player be picked up, and is one being picked
    // up" half of the carry mechanic. CarryController (on the carrier)
    // drives it via Commands; this just holds the synced state and, while
    // carried, follows the carrier and pins the hips.
    [RequireComponent(typeof(PlayerRagdoll))]
    [RequireComponent(typeof(NetworkIdentity))]
    public class Carryable : NetworkBehaviour
    {
        // Non-null while this player is being carried -- the carrier's
        // NetworkIdentity. SyncVar so every client can render the follow
        // and gate the steal prompt.
        [SyncVar] public NetworkIdentity carriedBy;

        // How long after being dropped/thrown before the ragdoll is
        // allowed to end (fed to PlayerRagdoll.NotifyReleasedFromCarry).
        [SerializeField] private float carryReleaseStun = 1.5f;
        public float CarryReleaseStun => carryReleaseStun;

        private PlayerRagdoll ragdoll;
        private Rigidbody hips;

        public bool IsCarried => carriedBy != null;

        // Only a ragdolled player who isn't already carried can be
        // grabbed. Car hits and sabotage stuns both leave PlayerRagdoll
        // ragdolling, so this covers both.
        public bool CanBeGrabbed => ragdoll != null && ragdoll.IsRagdolling && carriedBy == null;

        private void Awake()
        {
            ragdoll = GetComponent<PlayerRagdoll>();
        }

        // Server-only, from CarryController.CmdGrab.
        [Server]
        public void ServerAttach(NetworkIdentity carrier)
        {
            if (!CanBeGrabbed || carrier == null) return;
            carriedBy = carrier;
        }

        // Server-only. thrown == false is a gentle set-down.
        [Server]
        public void ServerDetach()
        {
            carriedBy = null;
            RpcOnDetached();
        }

        [ClientRpc]
        private void RpcOnDetached()
        {
            // Un-pin the hips so the full ragdoll physics resume, and
            // tell PlayerRagdoll to hold the ragdoll a bit longer before
            // standing up.
            if (hips != null) hips.isKinematic = false;
            if (ragdoll != null) ragdoll.NotifyReleasedFromCarry(carryReleaseStun);
        }

        private void Update()
        {
            // Runs on server + every client. While carried, snap the
            // whole player object to the carrier's carry anchor and pin
            // the hips there; the rest of the ragdoll flops from physics.
            if (carriedBy == null) return;

            CarryController carrier = carriedBy.GetComponent<CarryController>();
            if (carrier == null || carrier.CarryAnchor == null) return;

            transform.position = carrier.CarryAnchor.position;
            transform.rotation = carrier.CarryAnchor.rotation;

            LazyResolveHips();
            if (hips != null)
            {
                hips.isKinematic = true;
                hips.transform.position = carrier.CarryAnchor.position;
                hips.transform.rotation = carrier.CarryAnchor.rotation;
            }
        }

        private void LazyResolveHips()
        {
            if (hips != null) return;
            // PlayerRagdoll finds the hips lazily too (skin spawns later
            // for a remote copy) -- mirror that.
            RagdollHips h = GetComponentInChildren<RagdollHips>(true);
            if (h != null) hips = h.Rigidbody;
        }
    }
}
```

> **Why drive `transform.position` directly instead of parenting to the
> carrier?** Mirror doesn't replicate ad-hoc runtime re-parenting (same
> reason `LootSpawnPoint` stopped parenting its spawn). Writing the
> position every frame on every client, plus the server, keeps it in
> sync through the victim's own `NetworkTransform` and needs no
> authority juggling — the victim's `FirstPersonController` is disabled
> (they're ragdolled) so nothing on their side fights it.

### 🔴 Rest Point 1
No carrier yet — set `carriedBy` from the Inspector on a ragdolled
player (or a debug key). Confirm the body snaps to a point and follows
if you move that reference object, with the limbs still flopping. Clear
it → body drops, ragdoll physics resume.

---

## Part 2 — `PlayerRagdoll` changes

The ragdoll must not end while the player is carried, and must hold a
little longer after release.

```csharp
// New field
[SerializeField] private float carryHoldExtra = 0f; // set by NotifyReleasedFromCarry at runtime

private Carryable carryable;      // resolve in Awake alongside cameraRig
private float extraHoldUntil;     // Time.time floor added by a carry release

public bool IsCarried => carryable != null && carryable.IsCarried;

// Called by Carryable.RpcOnDetached -- keep ragdolling for `stun`
// seconds past now before standing up.
public void NotifyReleasedFromCarry(float stun)
{
    extraHoldUntil = Mathf.Max(extraHoldUntil, Time.time + stun);
}
```

In `ImpactSequence`'s settle loop, the break conditions become:

```csharp
if (Time.time >= hardCapTime && !IsCarried) break; // hard cap can't fire mid-carry

bool minPassed = Time.time >= minEndTime && Time.time >= extraHoldUntil;
bool restedLongEnough = settledSince >= 0f && Time.time - settledSince >= settleHoldTime;
if (minPassed && restedLongEnough && !IsCarried) break;
```

So: carried → never breaks. Released → `extraHoldUntil` pushes
`minEndTime` out by `carryReleaseStun`, then the normal settle check
applies (which naturally waits for a *thrown* body to land, since it's
moving fast).

> **A body grabbed after its ragdoll already settled:** the settle loop
> would have broken already. Guard the top of the loop — if
> `carryable.IsCarried` becomes true, re-enter/keep looping. Simplest:
> the loop condition is `while (true)` already; just make sure the two
> `break`s both check `!IsCarried` (above), and that `ImpactSequence`
> hasn't returned yet. A player whose ragdoll fully ended and stood up
> **can't** be grabbed (`CanBeGrabbed` needs `IsRagdolling`), so this
> only matters for the settled-but-still-down window — which the 3.5s
> `defaultStunDuration` covers.

### 🔴 Rest Point 2
Ragdoll a player (car hit). Set `carriedBy` while they're down → they
never stand up. Clear it → they stay down ~1.5s more, then stand.
Throw-test comes in Part 3.

---

## Part 3 — `CarryController` (on the carrier / every Player)

New `Assets/Scripts/Player/CarryController.cs`.

```csharp
using Mirror;
using RobEveryone.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobEveryone.Player
{
    // Owner input for picking up, carrying and throwing a downed rival.
    // E to grab (only when Interactor has no target of its own, so
    // stealing always wins the keypress first); hold LMB to charge a
    // throw, release to launch; press E again (or get ragdolled) to
    // drop.
    [RequireComponent(typeof(FirstPersonController))]
    public class CarryController : NetworkBehaviour
    {
        [SerializeField] private Transform viewPoint;
        [SerializeField] private Transform carryAnchor;   // in front of / over the shoulder; where the body sits
        [SerializeField] private float grabRange = 2.5f;
        [SerializeField] private float maxThrowForce = 45f;
        [SerializeField] private float minThrowForce = 8f;
        [SerializeField] private float throwChargeTime = 1.2f;   // seconds to full charge
        [SerializeField] private LayerMask playerMask = ~0;

        public Transform CarryAnchor => carryAnchor;

        [SyncVar] private NetworkIdentity carried; // the victim, server-set
        public bool IsCarrying => carried != null;

        private Interactor interactor;
        private FirstPersonController fpc;
        private PlayerRagdoll ownRagdoll;
        private float chargeStart = -1f;

        private void Awake()
        {
            interactor = GetComponent<Interactor>();
            fpc = GetComponent<FirstPersonController>();
            ownRagdoll = GetComponent<PlayerRagdoll>();
        }

        private void Update()
        {
            // Server: drop the body the instant the carrier ragdolls.
            if (isServer && carried != null && ownRagdoll != null && ownRagdoll.IsRagdolling)
            {
                ServerDrop(thrown: false);
            }

            if (!isOwned || fpc.IsFrozen) return;

            if (!IsCarrying)
            {
                // E grabs -- but only if Interactor isn't already
                // pointing at something (a steal window, a pickup),
                // so stealing sequences before carrying.
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame &&
                    interactor.CurrentTarget == null && TryFindCarryable(out NetworkIdentity target))
                {
                    CmdGrab(target);
                }
                return;
            }

            // Carrying: E sets them down; hold LMB to charge + release to throw.
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                CmdDrop(thrown: false, Vector3.zero, 0f);
                return;
            }

            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame) chargeStart = Time.time;
                else if (Mouse.current.leftButton.wasReleasedThisFrame && chargeStart >= 0f)
                {
                    float t = Mathf.Clamp01((Time.time - chargeStart) / throwChargeTime);
                    float force = Mathf.Lerp(minThrowForce, maxThrowForce, t);
                    chargeStart = -1f;
                    CmdDrop(thrown: true, viewPoint.forward, force);
                }
            }
        }

        private bool TryFindCarryable(out NetworkIdentity target)
        {
            target = null;
            if (viewPoint == null) return false;
            if (!Physics.Raycast(viewPoint.position, viewPoint.forward, out RaycastHit hit, grabRange, playerMask))
                return false;

            Carryable c = hit.collider.GetComponentInParent<Carryable>();
            if (c == null || c == GetComponent<Carryable>() || !c.CanBeGrabbed) return false;
            target = c.GetComponent<NetworkIdentity>();
            return true;
        }

        [Command]
        private void CmdGrab(NetworkIdentity target)
        {
            if (carried != null || target == null) return;
            Carryable c = target.GetComponent<Carryable>();
            if (c == null || !c.CanBeGrabbed) return;

            // Range re-check server-side.
            if (Vector3.Distance(transform.position, target.transform.position) > grabRange + 1f) return;

            c.ServerAttach(netIdentity);
            carried = target;
        }

        [Command]
        private void CmdDrop(bool thrown, Vector3 direction, float force) => ServerDrop(thrown, direction, force);

        [Server]
        private void ServerDrop(bool thrown, Vector3 direction = default, float force = 0f)
        {
            if (carried == null) return;
            Carryable c = carried.GetComponent<Carryable>();
            carried = null;
            if (c == null) return;

            c.ServerDetach();
            if (thrown && force > 0f) c.GetComponent<PlayerRagdoll>()?.ServerThrow(direction.normalized, force);
        }
    }
}
```

`PlayerRagdoll` needs a `ServerThrow` (or reuse the impact path):

```csharp
// Adds a fresh launch impulse to an already-ragdolling body (a thrown
// carried player). Fans out via the existing RpcApplyImpact so every
// client's ragdoll gets the same shove; NotifyReleasedFromCarry has
// already extended the hold, and the settle check waits for it to land.
[Server]
public void ServerThrow(Vector3 direction, float force)
{
    RpcApplyImpact(direction, force, defaultStunDuration); // duration is ignored past a carry release; the settle wait handles it
}
```

*(RpcApplyImpact is currently private — make it `internal`/`public` or
add a thin server method that calls it.)*

### 🔴 Rest Point 3
Two Editors. A tases B (or a car hits B). A walks up, presses `E` →
B hoists onto A's carry anchor, flopping. A walks around — B follows.
A holds LMB, a charge builds (add a HUD meter later), releases → B
launches along A's view, re-ragdolls, lands, stays down ~1.5s, stands.
A presses `E` while carrying → B set down gently. A gets tased while
carrying → B drops immediately.

---

## Part 4 — steal protection

In `PlayerTheftTarget`:

```csharp
private Carryable carryable; // resolve in Awake

public bool CanInteract => relay != null && relay.IsStealable
                        && (carryable == null || !carryable.IsCarried);
```

And in `Interact` / `CmdStealItem`, add the same `!carryable.IsCarried`
guard server-side. Now: you can stun someone and take your one item via
the normal steal window, *then* carry them — but once carried, the
prompt is gone and the steal Command refuses, so a body can't be passed
around and stripped.

> **If you'd rather the carrier keep their steal access while carrying**
> (knock down → carry → then steal your one item): gate on
> `carryable.carriedBy != <this client's player>` instead of blocking
> unconditionally. The design call above is "no stealing while carried,
> full stop."

### 🔴 Rest Point 4
Carry B. Confirm the "Steal" prompt does not appear and `E` doesn't
open the steal screen on them. Drop B, re-stun (fresh sabotage hit) →
steal works again.

---

## Part 5 — carrier movement (no bhop while carrying)

`FirstPersonController` gets a hook:

```csharp
// Set by CarryController on the owner. Light touch -- walk/run still
// work, just no bunny-hopping a body around the map.
public bool CarryingSomething { get; set; }
```

In `HandleMove`, when `CarryingSomething`, force `maxAirSpeed` down to
walk speed (or skip the air-accel block entirely) and ignore
`holdToAutoHop` (a single deliberate jump is fine, chained hops
aren't). `CarryController` sets `fpc.CarryingSomething = carried != null`
each frame on the owner.

### 🔴 Rest Point 5 — full pass
Two Editors, one round: stun a rival, take your one item, carry them
off the exit path, throw them into the road, watch a car hit them.
Confirm: no bhop while carrying; items/`E` still work; carrier tased →
instant drop; carried player can't act or stand until well after
landing; nobody can steal from them mid-carry.

---

## Editor wiring

### Player prefab
1. **Add Component → Carryable.** `Carry Release Stun` `1.5`.
2. **Add Component → Carry Controller.** `View Point` → the camera/
   viewPoint transform. **Carry Anchor** → a new empty child transform
   positioned where a carried body should sit (over-the-shoulder: up
   ~1.4, forward ~0.3, or held-in-front: forward ~0.8, up ~1.0 — tune
   in Rest Point 3). `Grab Range` `2.5`, `Max Throw Force` `45`,
   `Throw Charge Time` `1.2`, `Player Mask` → the layer players/ragdolls
   sit on (Default).
3. `Player Ragdoll` — new tuning fields (`Carry Hold Extra` etc.) have
   sane defaults; nothing to wire.

### HUD (follow-up, not blocking)
- A throw-charge meter while LMB is held (like the drag ghost, small).
- A "carried" indicator for the victim.

---

## Follow-ups / open

- **No-carry zone near the exit?** Design said relocation-off-the-exit
  is a *feature*, so probably not — but if grabbing someone the instant
  they touch the exit trigger feels cheap, gate `CanBeGrabbed` on
  distance from the `ExitPoint`.
- **Carry animation** — the carrier currently just walks with a body
  clipping into them. A one-armed/fireman carry pose is tracked in
  todo.md's "held item + carry/run animations" item.
- **Two carriers, same body** — `CmdGrab` checks `carried == null` and
  `CanBeGrabbed` (which needs `carriedBy == null`), so first Command
  wins; the loser's grab no-ops.
- **Carrier disconnects mid-carry** — `RobEveryoneNetworkManager.
  OnServerDisconnect` should call `carryController.ServerDrop(false)`
  before the base disconnect tears the player down.
