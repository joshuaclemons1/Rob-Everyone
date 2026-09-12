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
        [SerializeField] private Transform seatPoint;
        [SerializeField] private Transform standPoint;
        // Nobody can leave until this many seconds of the round have
        // elapsed -- keeps the very start of a round from being a
        // footrace straight to the exit.
        [SerializeField] private float exitBlockedUntilSeconds = 120f;
        // How long a seated player stays vulnerable/robbable before
        // actually getting away on their own.
        [SerializeField] private float carWaitDuration = 5f;

        public Transform SeatPoint => seatPoint;
        public Transform StandPoint => standPoint;
        public float CarWaitDuration => carWaitDuration;

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
