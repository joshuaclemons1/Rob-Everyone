using System;
using System.Collections.Generic;
using UnityEngine;

namespace RobEveryone.Player
{
    // Scales named bones (the head, by default) to zero on the owner's
    // own skin instance so their first-person camera -- which renders
    // their body now, so it isn't a floating nothing and so a held
    // hotbar item has hands to sit in later -- doesn't clip through its
    // own head. Added by PlayerSkinSpawner on the owner's copy only;
    // every other client's copy of this player keeps the full model.
    //
    // Re-applies in LateUpdate (after the Animator writes its poses) in
    // case a locomotion clip ever curves bone scale -- the Quaternius
    // clips don't, but this is cheap insurance.
    public class FirstPersonBodyTrim : MonoBehaviour
    {
        private Transform[] trimmed = Array.Empty<Transform>();
        private PlayerRagdoll ragdoll;

        private void Awake() => ragdoll = GetComponentInParent<PlayerRagdoll>();

        public void Apply(Transform skinRoot, string[] boneNames)
        {
            if (skinRoot == null || boneNames == null || boneNames.Length == 0) return;

            var matches = new List<Transform>();
            foreach (Transform t in skinRoot.GetComponentsInChildren<Transform>(true))
            {
                foreach (string n in boneNames)
                {
                    if (!string.IsNullOrEmpty(n) && string.Equals(t.name, n, StringComparison.OrdinalIgnoreCase))
                    {
                        matches.Add(t);
                        break;
                    }
                }
            }

            trimmed = matches.ToArray();
            ZeroThem();

            if (trimmed.Length == 0)
            {
                Debug.LogWarning($"FirstPersonBodyTrim on '{name}': none of [{string.Join(", ", boneNames)}] matched a bone -- the head won't be trimmed. Check the skin's bone names.", this);
            }
        }

        private void ZeroThem()
        {
            // Leave the bones alone during a stun -- PlayerRagdoll is
            // driving these same bones with physics (and the owner sees
            // their own ragdoll), so a collapsed head there would look
            // broken and give it a degenerate collider.
            if (ragdoll != null && ragdoll.IsRagdolling) return;

            for (int i = 0; i < trimmed.Length; i++)
            {
                if (trimmed[i] != null) trimmed[i].localScale = Vector3.zero;
            }
        }

        private void LateUpdate() => ZeroThem();
    }
}
