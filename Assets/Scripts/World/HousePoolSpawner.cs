using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace RobEveryone.World
{
    // Randomly assigns one prefab from a pool to each slot Transform on
    // Start(), turning "a pool of house prefabs" (Stage 3f) into an actual
    // map (Stage 3g). Normal and Good House slots draw from separate pools
    // so the slots meant to hold higher-value houses actually get one,
    // rather than every slot picking from the same combined list.
    //
    // Networking (Stage 4): server-only (OnStartServer instead of Start)
    // and NetworkServer.Spawn instead of a plain Instantiate -- every
    // connected client needs to see the *same* randomly-chosen house in
    // each slot, not each independently roll their own. Every house
    // prefab needs a NetworkIdentity at its root and to be registered as
    // a Spawnable Prefab on the NetworkManager (see
    // stage4-multiplayer-mirror.md Part 5) -- the nested Homeowner/
    // LootSpawnPoint inside each house don't need their own
    // NetworkIdentity, they ride along under the house's single one as
    // long as they're still its children when this Instantiate call
    // creates the house.
    public class HousePoolSpawner : NetworkBehaviour
    {
        [SerializeField] private List<GameObject> normalHousePrefabs = new();
        [SerializeField] private List<GameObject> goodHousePrefabs = new();
        [SerializeField] private List<Transform> normalSlots = new();
        [SerializeField] private List<Transform> goodSlots = new();

        // Every house prefab is a 40x40 plot (Stage 3e, updated from the
        // original 25x25) -- since actual houses only exist once Start()
        // spawns them at Play time, this draws a same-size placeholder box
        // at each slot in the Scene view at all times (not just when
        // selected), so roads/fences/etc. can be placed against a visible
        // footprint without needing Play mode.
        [SerializeField] private Vector3 housePlotSize = new(40f, 4f, 40f);

        public override void OnStartServer()
        {
            SpawnAt(normalSlots, normalHousePrefabs);
            SpawnAt(goodSlots, goodHousePrefabs);
        }

        private void SpawnAt(List<Transform> slots, List<GameObject> pool)
        {
            if (pool.Count == 0) return;

            foreach (Transform slot in slots)
            {
                if (slot == null) continue;

                GameObject prefab = pool[Random.Range(0, pool.Count)];
                GameObject instance = Instantiate(prefab, slot.position, slot.rotation);
                NetworkServer.Spawn(instance);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
            DrawSlotGizmos(normalSlots);

            Gizmos.color = new Color(1f, 0.8f, 0.15f, 0.6f);
            DrawSlotGizmos(goodSlots);
        }

        private void DrawSlotGizmos(List<Transform> slots)
        {
            foreach (Transform slot in slots)
            {
                if (slot == null) continue;

                Matrix4x4 previousMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(slot.position + Vector3.up * (housePlotSize.y * 0.5f), slot.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, housePlotSize);
                Gizmos.matrix = previousMatrix;
            }
        }
    }
}
