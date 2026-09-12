using UnityEngine;

namespace RobEveryone.Core
{
    // Pure locatable marker, no logic -- same shape as PlayerSpawnPoint.
    // One instance per occupiable cell *slot*, not per cell -- a 3-cell,
    // 2-slot-each jail is 6 of these, one at each spot a player should
    // actually stand. GameFlowManager.ClaimJailPoint hands out a free one
    // per jailed player so simultaneous jailings spread across the
    // physical layout instead of stacking on a single marker. Lives only
    // in the gameplay scene, inside/near the police station -- the Lobby
    // never needs one, nobody gets jailed there.
    public class JailPoint : MonoBehaviour
    {
    }
}
