#if !DISABLESTEAMWORKS
using Mirror;
using Steamworks;
using UnityEngine;
using UnityEngine.Audio;

namespace RobEveryone.Voice
{
    // One per player object, on every client. Steam voice is mono
    // 16-bit PCM at GetVoiceOptimalSampleRate() (usually 24 kHz). We
    // decode incoming compressed frames into a float ring buffer and let
    // a streaming AudioClip pull from it -- the AudioSource is 3D
    // (spatialBlend 1), so distance attenuation is automatic.
    [RequireComponent(typeof(AudioSource))]
    public class SteamVoicePlayback : MonoBehaviour
    {
        [SerializeField] private float amplitudeGain = 4f; // raw decoded RMS is small (~0.05-0.2 for normal speech); scales it up to a usable 0-1 cue
        [SerializeField] private float amplitudeDecayPerSecond = 3f; // how fast the cue falls back to 0 between frames, so it doesn't read as "stuck loud" after the speaker stops

        private AudioSource source;
        private NetworkIdentity identity;
        private uint sampleRate;

        private readonly byte[] decompressed = new byte[22050];        // ~0.9 s @ 24 kHz, 16-bit mono
        private float[] ring = new float[24000 * 2];                   // ~2 s
        private int writePos, readPos, available;
        private readonly object gate = new object();

        // Purely visual "how loud is this frame" cue, 0-1, read by
        // PlayerHeadTalkScale to pulse the speaker's head. Not used for
        // anything audio-critical -- safe to compute cheaply and decay
        // on a totally separate cadence from actual playback.
        public float Amplitude { get; private set; }

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            identity = GetComponent<NetworkIdentity>();
            sampleRate = SteamManager.Initialized ? SteamUser.GetVoiceOptimalSampleRate() : 24000;

            source.clip = AudioClip.Create("SteamVoice", (int)sampleRate, 1, (int)sampleRate, true, PcmRead);
            source.loop = true;
            source.spatialBlend = 1f;         // fully 3D
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 3f;
            source.maxDistance = 40f;          // proximity range -- tune vs. map scale

            // Auto-routes into the mixer's Voice group once
            // Assets/Resources/MainMixer.mixer exists (settings-menu-
            // setup.md Milestone C) -- no per-prefab Inspector wiring
            // needed. Silently stays on the default (unrouted) output
            // until then.
            AudioMixer mixer = Resources.Load<AudioMixer>("MainMixer");
            if (mixer != null)
            {
                AudioMixerGroup[] groups = mixer.FindMatchingGroups("Voice");
                if (groups.Length > 0) source.outputAudioMixerGroup = groups[0];
            }

            source.Play();
        }

        private void Update()
        {
            Amplitude = Mathf.MoveTowards(Amplitude, 0f, amplitudeDecayPerSecond * Time.deltaTime);
        }

        // Called from PlayerVoice.RpcReceiveVoice (main thread).
        public void EnqueueCompressed(byte[] frame)
        {
            if (!SteamManager.Initialized) return;
            if (identity != null && VoiceMuteList.IsMuted(identity.netId)) return;

            EVoiceResult r = SteamUser.DecompressVoice(
                frame, (uint)frame.Length,
                decompressed, (uint)decompressed.Length,
                out uint bytesWritten, sampleRate);
            if (r != EVoiceResult.k_EVoiceResultOK || bytesWritten == 0) return;

            int samples = (int)bytesWritten / 2; // 16-bit
            float sumSquares = 0f;
            lock (gate)
            {
                for (int i = 0; i < samples; i++)
                {
                    short s = (short)(decompressed[i * 2] | (decompressed[i * 2 + 1] << 8));
                    float normalized = s / 32768f;
                    sumSquares += normalized * normalized;

                    ring[writePos] = normalized;
                    writePos = (writePos + 1) % ring.Length;
                    if (available < ring.Length) available++;
                    else readPos = (readPos + 1) % ring.Length; // overflow: drop oldest
                }
            }

            if (samples > 0)
            {
                float rms = Mathf.Sqrt(sumSquares / samples);
                // Attack instantly (never wait a decay tick to reflect a
                // new, louder frame); Update() above handles the decay.
                Amplitude = Mathf.Max(Amplitude, Mathf.Clamp01(rms * amplitudeGain));
            }
        }

        // Audio thread. Fill with whatever's buffered, silence otherwise.
        private void PcmRead(float[] data)
        {
            lock (gate)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    if (available > 0)
                    {
                        data[i] = ring[readPos];
                        readPos = (readPos + 1) % ring.Length;
                        available--;
                    }
                    else data[i] = 0f;
                }
            }
        }
    }
}
#endif
