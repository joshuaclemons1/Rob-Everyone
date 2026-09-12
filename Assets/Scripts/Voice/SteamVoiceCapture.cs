#if !DISABLESTEAMWORKS
using RobEveryone.Core;
using RobEveryone.Input;
using Steamworks;
using UnityEngine;

namespace RobEveryone.Voice
{
    // Local player only. Steam owns the mic (device selection + encode +
    // its own noise suppression); this just toggles recording on the
    // push-to-talk action and hands whatever compressed bytes Steam has
    // ready to `OnFrame` each poll.
    public class SteamVoiceCapture : MonoBehaviour
    {
        public enum Mode { PushToTalk, OpenMic }

        [SerializeField] private Mode mode = Mode.PushToTalk;
        // Steam recommends ~4x GetAvailableVoice's max; 8 KB is plenty
        // for one poll at our cadence.
        private readonly byte[] buffer = new byte[8192];

        private bool recording;
        public System.Action<byte[], int> OnFrame; // (buffer, byteCount) -- valid only for the call

        public bool Transmitting => recording;

        private void Update()
        {
            if (!SteamManager.Initialized) return;

            bool want = mode == Mode.OpenMic || InputManager.Gameplay.PushToTalk.IsPressed();

            if (want && !recording) { SteamUser.StartVoiceRecording(); recording = true; }
            else if (!want && recording) { SteamUser.StopVoiceRecording(); recording = false; }

            if (!recording) return;

            EVoiceResult avail = SteamUser.GetAvailableVoice(out uint pending);
            if (avail != EVoiceResult.k_EVoiceResultOK || pending == 0) return;

            EVoiceResult got = SteamUser.GetVoice(true, buffer, (uint)buffer.Length, out uint written);
            if (got == EVoiceResult.k_EVoiceResultOK && written > 0)
            {
                OnFrame?.Invoke(buffer, (int)written);
            }
        }

        private void OnDisable()
        {
            if (recording && SteamManager.Initialized) SteamUser.StopVoiceRecording();
            recording = false;
        }
    }
}
#endif
