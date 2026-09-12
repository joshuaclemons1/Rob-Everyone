#if !DISABLESTEAMWORKS
using RobEveryone.Player;
using UnityEngine;

namespace RobEveryone.Voice
{
    // Cheap, purely visual "who's talking" cue: a speaking player's Head
    // bone swells slightly in proportion to how loud SteamVoicePlayback
    // is currently decoding for them. Reads the same PlayerSkinSpawner.
    // SkinInstance / bone-name convention FirstPersonBodyTrim already
    // uses (Quaternius rig: "Head") rather than inventing a second way
    // to find it.
    //
    // On the owner's own client this is harmless, not meaningful: their
    // own Head bone is already scaled to zero by FirstPersonBodyTrim (so
    // the first-person camera doesn't clip through it), and they never
    // receive their own voice frames anyway (PlayerVoice's
    // includeOwner:false) -- so Amplitude just stays 0 for themselves.
    // Every other client sees this player's *un-trimmed* Head bone pulse
    // normally.
    [RequireComponent(typeof(PlayerSkinSpawner))]
    [RequireComponent(typeof(SteamVoicePlayback))]
    public class PlayerHeadTalkScale : MonoBehaviour
    {
        [SerializeField] private string headBoneName = "Head";
        [SerializeField] private float maxScaleBoost = 0.15f; // +15% at full volume -- visible, not cartoonish
        [SerializeField] private float smoothTime = 0.08f;

        private PlayerSkinSpawner skinSpawner;
        private SteamVoicePlayback playback;
        private Transform headBone;
        private Vector3 baseScale = Vector3.one;
        private float currentBoost;
        private float boostVelocity;

        private void Awake()
        {
            skinSpawner = GetComponent<PlayerSkinSpawner>();
            playback = GetComponent<SteamVoicePlayback>();
        }

        private void Update()
        {
            if (headBone == null)
            {
                // SkinInstance spawns a frame or so after this component
                // wakes up (PlayerSkinSpawner.OnStartLocalPlayer / the
                // SyncVar hook on remote clients) -- keep polling until
                // it exists rather than trying to resolve it once.
                if (skinSpawner.SkinInstance == null) return;

                headBone = FindBone(skinSpawner.SkinInstance.transform, headBoneName);
                if (headBone == null) return;

                baseScale = headBone.localScale;
            }

            float target = playback.Amplitude * maxScaleBoost;
            currentBoost = Mathf.SmoothDamp(currentBoost, target, ref boostVelocity, smoothTime);
            headBone.localScale = baseScale * (1f + currentBoost);
        }

        private static Transform FindBone(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindBone(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif
