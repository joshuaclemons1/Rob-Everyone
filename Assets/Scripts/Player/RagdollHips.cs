using UnityEngine;

namespace RobEveryone.Player
{
    // Marks whichever bone was assigned to the Ragdoll Wizard's "Pelvis"
    // slot on a skin prefab. Bone names vary between the 6 selectable
    // skins (they're separate character models, not guaranteed to share
    // exact naming), so PlayerRagdoll finds the hips by searching for this
    // marker instead of a hardcoded bone name -- add it once per skin,
    // right after running the wizard on that skin's prefab.
    [RequireComponent(typeof(Rigidbody))]
    public class RagdollHips : MonoBehaviour
    {
        public Rigidbody Rigidbody { get; private set; }

        private void Awake()
        {
            Rigidbody = GetComponent<Rigidbody>();
        }
    }
}
