using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RobEveryone.UI
{
    // One row in the VoiceCaptionsHUD list -- a live head portrait (fed
    // a RenderTexture from VoicePortraitPool) beside the speaker's name.
    public class VoiceCaptionRow : MonoBehaviour
    {
        [SerializeField] private RawImage portraitImage;
        [SerializeField] private TMP_Text nameText;

        public void Set(Texture portrait, string displayName)
        {
            if (portraitImage != null) portraitImage.texture = portrait;
            if (nameText != null) nameText.text = displayName;
        }
    }
}
