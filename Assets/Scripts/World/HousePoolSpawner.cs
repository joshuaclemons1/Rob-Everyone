using System.Collections.Generic;
using UnityEngine;

namespace RobEveryone.World
{
    // Randomly assigns one prefab from a pool to each slot Transform on
    // Start(), turning "a pool of house prefabs" (Stage 3f) into an actual
    // map (Stage 3g). Normal and Good House slots draw from separate pools
    // so the slots meant to hold higher-value houses actually get one,
    // rather than every slot picking from the same combined list.
    public class HousePoolSpawner : MonoBehaviour
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

        private void Start()
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
                Instantiate(prefab, slot.position, slot.rotation);
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
