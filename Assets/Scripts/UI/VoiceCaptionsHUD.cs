#if !DISABLESTEAMWORKS
using System.Collections.Generic;
using RobEveryone.Inventory;
using RobEveryone.Player;
using RobEveryone.Voice;
using UnityEngine;

namespace RobEveryone.UI
{
    // A HUD-corner list of currently-speaking players -- rivals *and*
    // yourself, all in the one shared list/container so your own row
    // reads as part of it rather than a separate indicator elsewhere.
    // Each row is a live head portrait (VoicePortraitPool) beside a
    // name -- a supplemental cue to PlayerHeadTalkScale's pulse for
    // anyone who can't easily see a distant head, or is playing with
    // sound off. Rival rows are gated by ControlSettings.CaptionsEnabled
    // (an accessibility toggle); your own row is not -- it's your own
    // mic status, not a caption, so it always shows while you're
    // transmitting regardless of that setting.
    //
    // Rival rows are keyed by SteamVoicePlayback instance rather than a
    // display-name string -- names aren't unique (two rivals can both
    // still read "rival" before their SyncVar arrives). Your own row
    // can't use that same key: your own voice frames never reach your
    // own SteamVoicePlayback in the first place (PlayerVoice's
    // RpcReceiveVoice is includeOwner:false, the "never hear yourself"
    // rule), so there's no Amplitude signal for yourself -- it's tracked
    // separately, keyed off SteamVoiceCapture.Transmitting instead
    // (already-local state, no network round-trip needed).
    public class VoiceCaptionsHUD : MonoBehaviour
    {
        [SerializeField] private Transform rowContainer;
        [SerializeField] private VoiceCaptionRow rowTemplate;
        [SerializeField] private float speakingThreshold = 0.05f; // roughly matches where PlayerHeadTalkScale's pulse starts responding

        public static VoiceCaptionsHUD Instance { get; private set; }

        private readonly Dictionary<SteamVoicePlayback, PlayerInventory> tracked = new();
        private readonly Dictionary<SteamVoicePlayback, VoiceCaptionRow> activeRows = new();

        private SteamVoiceCapture localCapture;
        private VoiceCaptionRow selfRow;
        private bool selfShown;
        private bool warnedNoPool;

        private void Awake()
        {
            Instance = this;
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Register(SteamVoicePlayback playback, PlayerInventory inventory)
        {
            if (playback != null) tracked[playback] = inventory;
        }

        public void Unregister(SteamVoicePlayback playback)
        {
            if (playback == null) return;
            ReleaseRow(playback); // needs `tracked` to still have the entry, so this comes first
            tracked.Remove(playback);
        }

        private void Update()
        {
            UpdateSelfRow();

            if (!ControlSettings.CaptionsEnabled)
            {
                if (activeRows.Count > 0) ClearAllRows();
                return;
            }

            foreach (KeyValuePair<SteamVoicePlayback, PlayerInventory> pair in tracked)
            {
                bool speaking = pair.Key != null && pair.Key.Amplitude > speakingThreshold;
                bool hasRow = activeRows.ContainsKey(pair.Key);

                if (speaking && !hasRow) ClaimRow(pair.Key, pair.Value);
                else if (!speaking && hasRow) ReleaseRow(pair.Key);
            }
        }

        private void UpdateSelfRow()
        {
            if (localCapture == null)
            {
                localCapture = FindFirstObjectByType<SteamVoiceCapture>();
                if (localCapture == null) return;
            }

            bool transmitting = localCapture.Transmitting;
            if (transmitting == selfShown) return;
            selfShown = transmitting;

            if (transmitting)
            {
                if (rowContainer == null || rowTemplate == null) return;

                PlayerInventory local = PlayerInventory.LocalPlayer;
                Texture portrait = ResolvePortrait(local);

                selfRow = Instantiate(rowTemplate, rowContainer);
                selfRow.gameObject.SetActive(true);
                selfRow.transform.SetAsFirstSibling(); // you always show first, rivals below
                selfRow.Set(portrait, "You");
            }
            else
            {
                if (selfRow != null) Destroy(selfRow.gameObject);
                selfRow = null;

                PlayerInventory local = PlayerInventory.LocalPlayer;
                if (local != null) VoicePortraitPool.Instance?.Release(local);
            }
        }

        private void ClaimRow(SteamVoicePlayback playback, PlayerInventory inventory)
        {
            if (rowContainer == null || rowTemplate == null) return;

            Texture portrait = ResolvePortrait(inventory);

            VoiceCaptionRow row = Instantiate(rowTemplate, rowContainer);
            row.gameObject.SetActive(true);
            row.Set(portrait, inventory != null ? inventory.DisplayName : "rival");
            activeRows[playback] = row;
        }

        // Centralizes the null-Instance case so it's logged once instead
        // of silently leaving every row's portrait blank (a RawImage
        // with a null texture renders as a solid white square, which is
        // otherwise indistinguishable from "the portrait genuinely
        // failed to render" -- see VoicePortraitPool's own warnings for
        // that case instead).
        private Texture ResolvePortrait(PlayerInventory inventory)
        {
            if (inventory == null) return null;

            if (VoicePortraitPool.Instance == null)
            {
                if (!warnedNoPool)
                {
                    Debug.LogWarning("[VoiceCaptionsHUD] No VoicePortraitPool found in this scene -- every portrait will show blank (white). Add one per settings-menu-setup.md Milestone E.");
                    warnedNoPool = true;
                }
                return null;
            }

            return VoicePortraitPool.Instance.Acquire(inventory);
        }

        private void ReleaseRow(SteamVoicePlayback playback)
        {
            if (!activeRows.TryGetValue(playback, out VoiceCaptionRow row)) return;
            activeRows.Remove(playback);
            if (row != null) Destroy(row.gameObject);

            if (tracked.TryGetValue(playback, out PlayerInventory inventory) && inventory != null)
            {
                VoicePortraitPool.Instance?.Release(inventory);
            }
        }

        private void ClearAllRows()
        {
            foreach (SteamVoicePlayback playback in new List<SteamVoicePlayback>(activeRows.Keys))
            {
                ReleaseRow(playback);
            }
        }
    }
}
#endif
