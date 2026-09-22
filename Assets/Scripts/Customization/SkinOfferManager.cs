using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace RobEveryone.Customization
{
    // Issue #52 (Phase 2): rolls a fresh, distinct random skin for each
    // SkinOfferPedestal in the customization building once per Lobby
    // load. "Each lobby phase" falls straight out of Lobby.unity being a
    // single-mode scene that fully reloads (and every scene-placed
    // NetworkIdentity in it -- this manager and every pedestal alike --
    // gets destroyed and freshly recreated by Unity) every time
    // GameFlowManager.HandleRoundEnded's ServerChangeScene runs, which
    // is after *every* round -- confirmed by reading that method
    // directly, not just every batch. OnStartServer already fires fresh
    // on every one of those reloads, so "reroll every lobby phase" needs
    // no extra event wiring anywhere -- it's just what this hook already
    // does each time it runs.
    //
    // A separate coordinator rather than each pedestal rolling for
    // itself, so the whole set can be sampled *without* replacement --
    // the point of offering several pedestals is to actually show
    // several different skins in one phase, not risk the same one
    // showing twice.
    public class SkinOfferManager : NetworkBehaviour
    {
        [SerializeField] private PlayerSkinRoster skinRoster;
        [SerializeField] private List<SkinOfferPedestal> pedestals = new();

        public override void OnStartServer()
        {
            if (skinRoster == null || skinRoster.Count == 0 || pedestals.Count == 0) return;

            var pool = new List<int>(skinRoster.Count);
            for (int i = 0; i < skinRoster.Count; i++) pool.Add(i);

            // Fisher-Yates -- shuffle the whole pool, then hand out the
            // first N. Sampling without replacement this way is simpler
            // and just as correct as repeatedly picking a random
            // not-yet-used index, for a pool this small (dozens, not
            // millions).
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            int count = Mathf.Min(pedestals.Count, pool.Count);
            for (int i = 0; i < count; i++)
            {
                if (pedestals[i] != null) pedestals[i].ServerAssignOfferedSkin(pool[i]);
            }
        }
    }
}
