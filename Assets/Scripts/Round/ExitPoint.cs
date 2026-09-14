using System.Collections.Generic;
using RobEveryone.Interaction;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.Round
{
    // The map's extraction point (the taxi/car). No longer a walk-in
    // trigger -- press E ("Press E to get away!") to seat yourself in
    // the car (ExitCarState.EnterCar), which doesn't resolve the round
    // immediately. Instead you sit there, frozen and robbable, for
    // CarWaitDuration -- a rival gets one last chance at you, and you
    // can still climb back out yourself (ExitCarState's own "Press E to
    // exit the car" prompt) if you need to react to something. Only
    // actually finishes the round for you if you ride it out
    // (ExitCarState.FinalizeExit -> NotifyPlayerExtracted below).
    [RequireComponent(typeof(Collider))]
    public class ExitPoint : MonoBehaviour, IInteractable, IInteractableWarning
    {
        [SerializeField] private RoundManager roundManager;
        // One Transform per physical seat in the car model, in whatever
        // order they should fill (front-to-back/driver-out typically
        // reads best). The first occupant gets seatPoints[0], the second
        // seatPoints[1], and so on.
        [SerializeField] private Transform[] seatPoints;
        [SerializeField] private Transform standPoint;
        // Nobody can leave until this many seconds of the round have
        // elapsed -- keeps the very start of a round from being a
        // footrace straight to the exit.
        [SerializeField] private float exitBlockedUntilSeconds = 120f;
        // How long a seated player stays vulnerable/robbable before
        // actually getting away on their own.
        [SerializeField] private float carWaitDuration = 5f;

        public Transform StandPoint => standPoint;
        public float CarWaitDuration => carWaitDuration;

        // Issue #45 fix. Originally just one shared seatPoint Transform,
        // with nothing tracking who's already sitting there -- a second
        // player entering while the first was still seated got
        // teleported into the exact same position. ExitCarFrozen only
        // gates FirstPersonController's own input-driven movement, not
        // the CharacterController itself, so it stayed fully active and
        // Unity's own automatic depenetration silently shoved the second
        // player out to whatever nearby space was free -- not a
        // deliberate teleport, which is exactly why the round still
        // finished correctly for them 5 seconds later (isWaiting never
        // actually got cleared; nothing was actually wrong server-side).
        // Server-only, same "not worth syncing" reasoning
        // GameFlowManager.occupiedJailPoints already uses.
        private readonly List<ExitCarState> occupants = new();

        // Overflow-only fallback, once every real seatPoints slot is
        // already claimed (more simultaneous occupants than the car
        // model actually has seats for, or seatPoints left empty
        // entirely) -- a computed offset off the last real seat (or this
        // object's own transform if there are no real seats at all),
        // alternating left/right, one seatSpacing further out each pair,
        // same spirit as the original single-seat stopgap. Not meant to
        // be the normal case once a car model with real seats exists.
        [SerializeField] private float seatSpacing = 0.6f;

        // Hands back the claimed seat's world position/rotation for
        // `occupant` -- a real seatPoints[slot] Transform for however
        // many physical seats are configured, an overflow-computed pose
        // beyond that.
        public void ClaimSeat(ExitCarState occupant, out Vector3 position, out Quaternion rotation)
        {
            if (!occupants.Contains(occupant)) occupants.Add(occupant);
            int slot = occupants.IndexOf(occupant);

            if (seatPoints != null && slot < seatPoints.Length && seatPoints[slot] != null)
            {
                position = seatPoints[slot].position;
                rotation = seatPoints[slot].rotation;
                return;
            }

            int seatCount = seatPoints?.Length ?? 0;
            Transform baseSeat = seatCount > 0 && seatPoints[seatCount - 1] != null ? seatPoints[seatCount - 1] : transform;
            rotation = baseSeat.rotation;

            int overflowIndex = slot - seatCount + 1; // 1-based once overflowing
            if (overflowIndex <= 0)
            {
                position = baseSeat.position;
                return;
            }

            float side = (overflowIndex % 2 == 1) ? 1f : -1f;
            float distance = seatSpacing * Mathf.CeilToInt(overflowIndex / 2f);
            position = baseSeat.position + baseSeat.right * side * distance;
        }

        public void ReleaseSeat(ExitCarState occupant) => occupants.Remove(occupant);

        public string InteractionPrompt => "Press E to get away!";

        // Always true (same reasoning as ShopShelfItem's locked shelf) --
        // Interactor only ever shows *any* prompt when CanInteract is
        // true, so gating this on the time-block too would silently
        // swallow the WarningText below along with it. Interact() is
        // what actually enforces the block, by simply no-oping.
        public bool CanInteract => true;

        private bool IsBlocked => roundManager != null && ElapsedSeconds < exitBlockedUntilSeconds;
        private float ElapsedSeconds => roundManager.RoundDuration - roundManager.TimeRemaining;

        public string WarningText =>
            IsBlocked ? $"Can't leave for another {Mathf.CeilToInt(exitBlockedUntilSeconds - ElapsedSeconds)}s" : "";

        public void Interact(GameObject interactor)
        {
            if (IsBlocked) return;

            ExitCarState carState = interactor.GetComponent<ExitCarState>();
            if (carState == null) return;

            carState.EnterCar(this);
        }

        // Called by ExitCarState once a seated player's wait window runs
        // out without them climbing back out -- the actual moment their
        // round resolves, same RoundResult.RoundComplete path the old
        // walk-in trigger used.
        public void NotifyPlayerExtracted(PlayerInventory player)
        {
            if (roundManager == null || player == null) return;
            roundManager.NotifyPlayerReachedExit(player);
        }
    }
}
