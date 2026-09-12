#if !DISABLESTEAMWORKS
using Mirror;
using UnityEngine;

namespace RobEveryone.Voice
{
    // On the Player prefab. Owner: feeds SteamVoiceCapture frames up to
    // the server. Server: relays to every *other* client. Every client:
    // hands incoming frames to a SteamVoicePlayback attached to this
    // same (the speaker's) object, so 3D audio rolloff does proximity.
    [RequireComponent(typeof(SteamVoicePlayback))]
    public class PlayerVoice : NetworkBehaviour
    {
        private SteamVoiceCapture capture;   // resolved on the local player only
        private SteamVoicePlayback playback;

        private void Awake() => playback = GetComponent<SteamVoicePlayback>();

        public override void OnStartLocalPlayer()
        {
            // The capture component lives once on the local player rig
            // (see Editor wiring in voip-setup.md) -- find it and subscribe.
            capture = FindFirstObjectByType<SteamVoiceCapture>();
            if (capture != null) capture.OnFrame += SendFrame;
        }

        private void OnDestroy()
        {
            if (capture != null) capture.OnFrame -= SendFrame;
        }

        private void SendFrame(byte[] buf, int count)
        {
            // Copy: `buf` is reused next poll. count is small (~1-2 KB).
            byte[] frame = new byte[count];
            System.Array.Copy(buf, frame, count);
            CmdSendVoice(frame);
        }

        [Command(channel = Channels.Unreliable, requiresAuthority = true)]
        private void CmdSendVoice(byte[] frame)
        {
            // Fan out to everyone except the speaker. RpcReceiveVoice on
            // a per-object component reaches all clients; each skips its
            // own via isLocalPlayer below.
            RpcReceiveVoice(frame);
        }

        [ClientRpc(channel = Channels.Unreliable, includeOwner = false)]
        private void RpcReceiveVoice(byte[] frame)
        {
            // includeOwner:false already excludes the speaker's own
            // client; the guard is belt-and-suspenders.
            if (isLocalPlayer) return;
            playback.EnqueueCompressed(frame);
        }
    }
}
#endif
