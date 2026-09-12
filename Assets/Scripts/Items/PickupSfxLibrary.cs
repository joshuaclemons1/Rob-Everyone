using UnityEngine;

namespace RobEveryone.Items
{
    // A shared pool of generic pickup sounds, loaded once from Resources
    // so every PickupItem gets a sound without wiring it individually on
    // each of the ~40 separate item prefabs (same self-bootstrap-via-
    // Resources shape InputManager/AudioMixerApplier already use).
    //
    // Editor setup: right-click Assets/Resources -> Create ->
    // RobEveryone -> Pickup Sfx Library, name it exactly
    // "PickupSfxLibrary", drop clips into its Clips array. Every
    // PickupItem picks up the change automatically -- no per-prefab
    // wiring needed, and adding a new item prefab later needs no audio
    // setup at all.
    [CreateAssetMenu(fileName = "PickupSfxLibrary", menuName = "RobEveryone/Pickup Sfx Library")]
    public class PickupSfxLibrary : ScriptableObject
    {
        [SerializeField] private AudioClip[] clips;
        public AudioClip[] Clips => clips;

        private static PickupSfxLibrary instance;
        private static bool loadAttempted;

        public static PickupSfxLibrary Instance
        {
            get
            {
                if (!loadAttempted)
                {
                    instance = Resources.Load<PickupSfxLibrary>("PickupSfxLibrary");
                    loadAttempted = true;
                }
                return instance;
            }
        }
    }
}
