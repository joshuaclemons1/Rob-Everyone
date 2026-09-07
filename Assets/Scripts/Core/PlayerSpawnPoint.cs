using UnityEngine;

namespace RobEveryone.Core
{
    // Pure locatable marker, no logic -- one lives in each scene (the
    // gameplay map, the Lobby) at the position GameFlowManager should
    // place the player once that scene finishes loading.
    public class PlayerSpawnPoint : MonoBehaviour
    {
    }
}
