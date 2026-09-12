using RobEveryone.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RobEveryone.UI
{
    // One row per connected rival in the Audio tab's mute list -- binds
    // a mosaic-filled Toggle to VoiceMuteList, keyed by that player's
    // own netId (session-local, matching VoiceMuteList's own design --
    // see voip-setup.md Part 5, muting only ever affects your own
    // client).
    public class MuteRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Toggle muteToggle;

        private uint netId;

        public void Bind(string displayName, uint playerNetId)
        {
            netId = playerNetId;
            if (nameLabel != null) nameLabel.text = displayName;
            if (muteToggle != null) muteToggle.SetIsOnWithoutNotify(VoiceMuteList.IsMuted(netId));
        }

        public void OnMuteToggled(bool isOn) => VoiceMuteList.SetMuted(netId, isOn);
    }
}
