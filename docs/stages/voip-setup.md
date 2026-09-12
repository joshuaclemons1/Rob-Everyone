# VoIP — in-game proximity voice chat (Steam voice)

Still **unbuilt** — this is the plan. Uses Steam's own voice API through
the Steamworks.NET already installed for Stage 5 (`SteamUser.*Voice*`),
so no third-party SDK and no second transport: mic audio is captured by
Steam, sent as ordinary Mirror messages over the existing FizzySteamworks
connection, and played back through Unity `AudioSource`s attached to each
speaker's player object — which gets spatial falloff for free.

Prereq: Stage 5 done through Part 3 (`SteamManager` initializing,
`DISABLESTEAMWORKS` removed). Everything here lives behind
`#if !DISABLESTEAMWORKS` like `SteamLobby`/`SteamManager`.

## Design decisions (settle first)

- **Proximity, not team-wide.** You're rivals in the same map —
  overhearing someone rifling a house next door, or shouting across the
  exit, *is* the game. Voice attenuates with distance the same way
  `HomeownerAI`/`PoliceAI` vision has a range. A jailed player can talk
  to whoever's near the cells. (A team-wide channel could come later as a
  toggle, but it's not the default.)
- **Spatial falloff is client-side, fan-out is dumb.** The server relays
  every voice packet to every other client; each client's `AudioSource`
  is parented to the speaking player, so Unity's 3D audio rolloff does
  the proximity work. At the 4–8 player design target the bandwidth is
  trivial (~1–2 KB per speaker per 100 ms). Server-side distance culling
  is a later optimization, not v1.
- **Push-to-talk by default**, hold `V`. Open-mic and voice-activation
  are options in the same capture script, but PTT avoids every player
  broadcasting house-search foley constantly. Make it a setting.
- **Unreliable channel.** A dropped 20 ms of audio is inaudible; a
  stalled reliable queue is not. Voice Rpcs go on `Channels.Unreliable`.
- **You never hear yourself.** Capture and playback are wired so the
  local player's own packets are never played back locally.

---

## Part 1 — `SteamVoiceCapture` (mic → compressed bytes)

New file `Assets/Scripts/Voice/SteamVoiceCapture.cs`. One instance,
local-player-only. Steam handles the mic device, encoding, and noise
gate; we just start/stop recording and drain the compressed buffer.

```csharp
#if !DISABLESTEAMWORKS
using Steamworks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobEveryone.Voice
{
    // Local player only. Steam owns the mic (device selection + encode +
    // its own noise suppression); this just toggles recording on the
    // push-to-talk key and hands whatever compressed bytes Steam has
    // ready to `onFrame` each poll. Mirrors the project's
    // Keyboard.current polling style.
    public class SteamVoiceCapture : MonoBehaviour
    {
        public enum Mode { PushToTalk, OpenMic }

        [SerializeField] private Mode mode = Mode.PushToTalk;
        [SerializeField] private Key pushToTalkKey = Key.V;
        // Steam recommends ~4x GetAvailableVoice's max; 8 KB is plenty
        // for one poll at our cadence.
        private readonly byte[] buffer = new byte[8192];

        private bool recording;
        public System.Action<byte[], int> OnFrame; // (buffer, byteCount) -- valid only for the call

        public bool Transmitting => recording;

        private void Update()
        {
            if (!SteamManager.Initialized) return;

            bool want = mode == Mode.OpenMic ||
                        (Keyboard.current != null && Keyboard.current[pushToTalkKey].isPressed);

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
```

> `GetVoice`'s deprecated uncompressed params are passed as
> `bWantCompressed: true` + the rest defaulted by the Steamworks.NET
> wrapper — always take the compressed path, decode on the receiver.

---

## Part 2 — Network plumbing (`PlayerVoice`)

New file `Assets/Scripts/Voice/PlayerVoice.cs`. A `NetworkBehaviour` on
the **Player prefab**, so it has a `NetworkIdentity`, an owner, and a
transform to attach playback to.

```csharp
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
            // (see Editor wiring) -- find it and subscribe.
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
```

> **`includeOwner = false`** on the Rpc is the "never hear yourself"
> guarantee at the network layer. Keep the `isLocalPlayer` check too in
> case that attribute ever changes.

> **Server-side distance cull (later):** replace `RpcReceiveVoice` with a
> manual loop over `NetworkServer.connections`, `TargetReceiveVoice` only
> to those whose player is within, say, 40 m of the speaker. Skip for
> v1 — 8 players × 10 packets/sec is nothing.

---

## Part 3 — `SteamVoicePlayback` (bytes → PCM → 3D AudioSource)

New file `Assets/Scripts/Voice/SteamVoicePlayback.cs`. On the Player
prefab, on **every** copy (it's how you hear that player). Decodes with
`SteamUser.DecompressVoice` and streams the PCM into a looping
`AudioClip` via its pull callback + a ring buffer.

```csharp
#if !DISABLESTEAMWORKS
using Steamworks;
using UnityEngine;

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
        private AudioSource source;
        private uint sampleRate;

        private readonly byte[] decompressed = new byte[22050];        // ~0.9 s @ 24 kHz, 16-bit mono
        private float[] ring = new float[24000 * 2];                   // ~2 s
        private int writePos, readPos, available;
        private readonly object gate = new object();

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            sampleRate = SteamManager.Initialized ? SteamUser.GetVoiceOptimalSampleRate() : 24000;

            source.clip = AudioClip.Create("SteamVoice", (int)sampleRate, 1, (int)sampleRate, true, PcmRead);
            source.loop = true;
            source.spatialBlend = 1f;         // fully 3D
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 3f;
            source.maxDistance = 40f;          // proximity range -- tune vs. map scale
            source.Play();
        }

        // Called from PlayerVoice.RpcReceiveVoice (main thread).
        public void EnqueueCompressed(byte[] frame)
        {
            if (!SteamManager.Initialized) return;

            EVoiceResult r = SteamUser.DecompressVoice(
                frame, (uint)frame.Length,
                decompressed, (uint)decompressed.Length,
                out uint bytesWritten, sampleRate);
            if (r != EVoiceResult.k_EVoiceResultOK || bytesWritten == 0) return;

            int samples = (int)bytesWritten / 2; // 16-bit
            lock (gate)
            {
                for (int i = 0; i < samples; i++)
                {
                    short s = (short)(decompressed[i * 2] | (decompressed[i * 2 + 1] << 8));
                    ring[writePos] = s / 32768f;
                    writePos = (writePos + 1) % ring.Length;
                    if (available < ring.Length) available++;
                    else readPos = (readPos + 1) % ring.Length; // overflow: drop oldest
                }
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
```

> **Latency vs. underrun.** The ring buffer trades one for the other. If
> voice sounds choppy, hold ~150 ms of samples before `source.Play()`
> starts draining (a tiny "prime" counter in `EnqueueCompressed`). If
> it's laggy, shrink the ring. Start simple, tune in Rest Point 3.

> **Sample-rate mismatch.** `AudioClip.Create` here is made at Steam's
> optimal rate and Unity resamples to the project output rate
> automatically. Don't hard-code 24000 anywhere except the fallback.

---

## Part 3.5 — talk-scale visual cue (implemented, replaces the floating icon idea below for now)

New file `Assets/Scripts/Voice/PlayerHeadTalkScale.cs`. On the Player
prefab, alongside `PlayerVoice`/`SteamVoicePlayback`. Reads
`SteamVoicePlayback.Amplitude` (a new 0-1 property computed from the
RMS of each decoded frame, decaying back to 0 between frames) and
smoothly scales that player's `Head` bone (same exact-name-match
convention `PlayerSkinSpawner`/`FirstPersonBodyTrim` already use) up to
+15% at full volume. Harmless on the owner's own client -- their own
`Head` is already scaled to zero by `FirstPersonBodyTrim`, and they
never receive their own frames anyway (`includeOwner:false`), so
`Amplitude` just stays 0 for themselves. Every other client sees the
un-trimmed `Head` on that player's model pulse normally, so it's
visually obvious who's talking without needing a separate icon/UI.

## Part 4 — a talk indicator (optional but cheap)

- `SteamVoiceCapture.Transmitting` → show a small mic icon on your own
  HUD while PTT is held.
- `PlayerVoice` can set a `[SyncVar] bool speaking` (server sets it true
  on each relayed frame, a coroutine clears it ~0.3 s after the last
  one) → a floating "🔊" over a speaking player, gated by the same
  `HomeownerAI`-style visibility if you want it stealth-relevant.

---

## Part 5 — mute & per-player volume

- A simple `static HashSet<uint> Muted` keyed by `netId` (or SteamID);
  `SteamVoicePlayback.EnqueueCompressed` early-returns if its owner is
  muted.
- Per-player volume = `source.volume`. A right-click menu on a player, or
  a list in the Tab inventory screen's corner, later.
- These are **local** — muting someone only affects your client.

---

## Editor wiring (after it compiles)

### Scripting define

1. Confirm `DISABLESTEAMWORKS` is **not** in **Player Settings →
   Scripting Define Symbols** (removed in Stage 5). If it's back, all
   three scripts compile to nothing.

### Local rig

2. On the same persistent GameObject as `SteamManager` /
   `RobEveryoneNetworkManager` (or a child), add **Steam Voice Capture**.
   Set **Mode** = `PushToTalk`, **Push To Talk Key** = `V`.

### Player prefab (`Assets/Prefabs/Player.prefab`)

3. Add **Steam Voice Playback** (adds an `AudioSource` via
   `RequireComponent` — leave it, the script configures it in `Awake`).
4. Add **Player Voice**.
5. Add **Player Head Talk Scale** (needs `PlayerSkinSpawner`, already on
   this prefab, alongside it — no fields to wire).
6. Make sure the Player prefab has an **AudioListener** only on the
   *local* player's camera (it already does, from the FPS camera) — two
   listeners warns and breaks spatial audio.

### Project audio

7. **Edit → Project Settings → Audio**: default settings are fine; if
   you raised **DSP Buffer Size** for latency elsewhere, voice will feel
   it too.

### 🔴 Rest Point 1 — capture

Single Editor, Steam running: hold `V`, watch the Console/HUD indicator —
`SteamVoiceCapture.Transmitting` flips true, `OnFrame` fires with
non-zero counts. No audio yet.

### 🔴 Rest Point 2 — loopback

Two Editors (ParrelSync), two Steam accounts (test AppID 480 gives real
voice). Host + join, stand next to each other, hold `V` on one, talk —
the other hears it out of the correct speaker side, and watches that
player's head visibly (subtly) swell while they're talking. You do
**not** hear yourself, and your own head doesn't pulse on your own
screen.

### 🔴 Rest Point 3 — proximity + quality

- Walk apart → voice fades to nothing by ~40 m; walk back → returns.
- Talk continuously for 30 s → no growing lag, no dropouts (tune the
  ring buffer / prime if it's rough).
- Two people talking at once → both audible, positioned correctly.
- One player jailed, the other near the cells → they can talk; from
  across the map they can't.

### 🔴 Rest Point 4 — full playtest

Fold into the Stage 8 friend-group playtest: 4+ people, real matches,
confirm voice helps the chaos rather than becoming noise. Decide then
whether a push-to-talk-to-*mute* (open by default) or a team channel is
worth adding.

---

## Gotchas

- **AppID 480 works for voice** in the Editor (`steam_appid.txt` from
  Stage 5). No extra Steamworks config needed.
- **`SteamAPI.RunCallbacks()`** is already pumped by `SteamManager.Update`
  — the voice API doesn't need callbacks, but don't remove that.
- **Mic permissions** — first run, the OS may prompt. Steam also has its
  own "Voice" settings (input device, volume, "let games access mic");
  if capture returns `k_EVoiceResultNotRecording` forever, check there.
- **`byte[]` Rpc args** are fine in Mirror but each allocates — the
  per-frame `new byte[count]` in `SendFrame` is the tradeoff for not
  wrestling `ArraySegment` through a Command. At voice cadence it's
  negligible; if a profiler ever flags it, pool the buffers.
- **Don't gate voice behind `IsFrozen`** — a jailed player talking to
  their would-be rescuer is a designed interaction.
- **Echo** if two clients run on one machine without headphones — that's
  the speakers, not the code. Use headphones for two-Editor tests.
