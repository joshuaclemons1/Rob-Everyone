using Mirror;
using RobEveryone.Audio;
using RobEveryone.Core;
using RobEveryone.Interaction;
using RobEveryone.Inventory;
using RobEveryone.UI;
using UnityEngine;

namespace RobEveryone.Round
{
    // Reversible "caught" state -- replaces PoliceAI.CatchPlayer's old
    // instant round-finalization. Losing the 5 hotbar slots still happens
    // immediately (RoundManager.NotifyPlayerCaught), but the player only
    // actually finalizes as RoundResult.Caught if the round ends before
    // someone bails them out (RoundManager's jailedPlayers bookkeeping).
    //
    // Also covers the separate end-of-batch quota-failure jailing
    // (GameFlowManager.HandleRoundStarted) -- the only differences are
    // where the rescue payout comes from (isEndOfBatchJail: quota/3 vs.
    // quota/6) and that this variant self-releases after one full round
    // if nobody rescues (also HandleRoundStarted, keyed off
    // jailedAtRoundOrdinal).
    [RequireComponent(typeof(PlayerInventory))]
    public class JailState : NetworkBehaviour, IInteractable
    {
        [SyncVar] private bool isJailed;
        [SyncVar] private bool isEndOfBatchJail;
        [SyncVar] private int jailedAtRoundOrdinal;

        [SerializeField] private AudioClip[] jailedClips;
        [SerializeField] private AudioClip[] rescuedClips;
        [SerializeField, Range(0f, 1f)] private float jailedVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float rescuedVolume = 0.7f;

        // Issue #8 fix -- server-authoritative leash on top of the cell's
        // own level geometry. horizontal-only distance from the exact
        // JailPoint slot GameFlowManager.TeleportToJail assigned this
        // player; still free to walk/look anywhere inside this radius,
        // but stepping past it snaps straight back instead of trusting
        // walls/gaps in the scene to physically stop them (which is all
        // that was holding a jailed player in before -- see EnterJail's
        // own long-standing comment below).
        [SerializeField] private float confinementRadius = 4f;

        // Server-only. Set in EnterJail, cleared on release. Not a
        // SyncVar -- only Update()'s own [Server]-gated check ever reads
        // it, same "server owns this, don't bother syncing it" reasoning
        // occupiedJailPoints uses in GameFlowManager.
        private Transform jailAnchor;

        public bool IsJailed => isJailed;
        public bool IsEndOfBatchJail => isEndOfBatchJail;
        public int JailedAtRoundOrdinal => jailedAtRoundOrdinal;

        public string InteractionPrompt => $"Bail out {GetComponent<PlayerInventory>().DisplayName}";

        // Every other Player-root IInteractable (PlayerTheftTarget) gates
        // CanInteract on its own mutually-exclusive state (ragdoll-stunned
        // vs. jailed) -- a player is never both at once, so there's no
        // ambiguity in which one Interactor.FindTarget's
        // GetComponentInParent<IInteractable> resolves to.
        public bool CanInteract => isJailed;

        [Server]
        public void EnterJail(bool endOfBatch)
        {
            // Confirmed real bug (issue #5's own log evidence): with
            // multiple Police officers now possible, two different
            // PoliceAI instances can both catch the same player within
            // the same or adjacent frames -- NotifyPlayerCaught had no
            // guard, so EnterJail ran twice. GameFlowManager.ClaimJailPoint
            // marks a slot "occupied" by this player but never frees their
            // *previous* slot on re-entry, so the second call always found
            // its own prior slot "taken" and got bumped to the next one --
            // observed live as a sustained back-and-forth between two
            // JailPoints, once per re-trigger, since the remote teleport
            // is async and never visually resolves before the next call.
            if (isJailed) return;

            isJailed = true;
            isEndOfBatchJail = endOfBatch;
            jailedAtRoundOrdinal = GameFlowManager.Instance != null ? GameFlowManager.Instance.RoundOrdinal : 0;
            // Deliberately doesn't freeze movement -- a jailed player can
            // still walk/look around, just physically confined by the
            // cell's own geometry (backed up by confinementRadius below,
            // issue #8). SpectatorController separately pauses input
            // while actively spectating (T key).
            jailAnchor = GameFlowManager.Instance?.TeleportToJail(transform);

            // Own-client-only full message covering the teleport itself;
            // a separate broadcast (below) tells everyone *else* there's
            // a new rescue opportunity and what it pays.
            if (connectionToClient != null)
                TargetShowJailNotification(connectionToClient, "Caught by police!\nGoing to jail...");
            RpcAnnounceJailed(GetComponent<PlayerInventory>().DisplayName, ComputeBailPrice());
            RpcPlayJailedSfx();
        }

        // Separate from RpcAnnounceJailed -- that one deliberately skips
        // the jailed player themselves (they get their own text message
        // instead), but everyone including them should hear the cell door.
        [ClientRpc]
        private void RpcPlayJailedSfx()
        {
            SfxPlayer.PlayRandomAt(jailedClips, transform.position, jailedVolume);
        }

        [TargetRpc]
        private void TargetShowJailNotification(NetworkConnectionToClient target, string message)
        {
            JailNotificationUI ui = FindAnyObjectByType<JailNotificationUI>();
            if (ui != null) ui.Show(message, 3f);
        }

        // Skips showing this to the player who was actually just jailed --
        // TargetShowJailNotification above already covers them with their
        // own dedicated message.
        [ClientRpc]
        private void RpcAnnounceJailed(string playerName, int bailPrice)
        {
            if (playerName == PlayerInventory.LocalPlayer?.DisplayName) return;

            JailAlertUI alert = FindAnyObjectByType<JailAlertUI>();
            if (alert != null) alert.Show($"{playerName} got caught! Bail price is ${bailPrice}");
        }

        // Shared by EnterJail's broadcast and Interact's actual payout so
        // the two can never drift apart -- bounty (end-of-batch) = 1/3 of
        // current quota per gameplay-design.md; bond (mid-round) = half
        // that, 1/6.
        private int ComputeBailPrice() =>
            GameFlowManager.Instance != null
                ? Mathf.RoundToInt(GameFlowManager.Instance.CurrentQuota / (isEndOfBatchJail ? 3f : 6f))
                : 0;

        // Clears state without a payout -- used by both a real rescue and
        // round-end/self-bail finalization.
        [Server]
        public void ForceRelease()
        {
            isJailed = false;
            jailAnchor = null;
        }

        // Server-only cooldown gate for the confinement correction below --
        // confirmed real bug (issue #5's own log evidence, same root cause
        // as EnterJail's re-entry guard above): TeleportPlayerTo's remote
        // path is a TargetRpc round-trip, so this Transform (the server's
        // own copy, driven by NetworkTransform's client-authoritative sync)
        // doesn't actually reflect a just-issued correction for at least a
        // frame or two. Without this cooldown, a still-stale read on the
        // very next Update() looked exactly as out-of-bounds as before and
        // fired ANOTHER correction on top of the one still in flight --
        // observed live as a sustained per-frame flood of racing
        // TargetRpc calls to the same client.
        private float nextConfinementCorrection;

        // Issue #8 fix. Horizontal-only (Y excluded) so a jailed player
        // can still jump in place without tripping this -- only actually
        // putting distance between themselves and their assigned cell
        // slot counts. Checked every frame rather than relying on a
        // trigger volume, since no such volume exists in the scene today
        // and this needs to work without any Editor/scene changes.
        private void Update()
        {
            if (!isServer || !isJailed || jailAnchor == null) return;
            if (Time.time < nextConfinementCorrection) return;

            Vector3 offset = transform.position - jailAnchor.position;
            offset.y = 0f;
            if (offset.sqrMagnitude <= confinementRadius * confinementRadius) return;

            // Long enough for the TargetRpc -> client apply -> CmdTeleport
            // round trip to plausibly land before the next check trusts
            // this Transform again.
            nextConfinementCorrection = Time.time + 1f;
            GameFlowManager.Instance?.TeleportPlayerTo(transform, jailAnchor);
        }

        public void Interact(GameObject interactor)
        {
            if (!isServer || !isJailed) return;

            PlayerInventory rescuer = interactor.GetComponent<PlayerInventory>();
            PlayerInventory self = GetComponent<PlayerInventory>();
            if (rescuer == null || rescuer == self) return;

            // Flat reward TO the rescuer -- the jailed player pays/loses
            // nothing either way.
            rescuer.GrantCash(ComputeBailPrice());

            ForceRelease();
            GameFlowManager.Instance?.TeleportToJailExit(transform);
            GameFlowManager.Instance?.TeleportToJailExit(rescuer.transform);
            GameFlowManager.Instance?.HandlePlayerRescued(self);
            RpcPlayRescuedSfx();
        }

        [ClientRpc]
        private void RpcPlayRescuedSfx()
        {
            SfxPlayer.PlayRandomAt(rescuedClips, transform.position, rescuedVolume);
        }
    }
}
