using Mirror;
using RobEveryone.Items;
using UnityEngine;

namespace RobEveryone.Sabotage
{
    // Common entry point for anything SabotageUseController.CmdUseThrown
    // can launch -- SabotageProjectile (fuse-timer AOE, e.g. Dynamite) and
    // RetrievableProjectile (collision-triggered single-target, lands and
    // stays pickupable, e.g. Hammer) are different enough state machines
    // that branching one script into two modes would be messier than this
    // small interface (mirrors the project's own existing precedent of
    // keeping CarDriver and SabotageProjectile separate despite
    // superficial similarity).
    public interface ILaunchable
    {
        // remainingUses is the thrown slot's current durability/ammo
        // count at the moment of the throw (0 for an untracked item, e.g.
        // Dynamite) -- SabotageProjectile ignores it, RetrievableProjectile
        // needs it so a thrown-and-landed Hammer keeps its already-reduced
        // count instead of resetting to full.
        void ServerLaunch(Vector3 direction, ItemDefinition item, NetworkIdentity thrower, int remainingUses);
    }
}
