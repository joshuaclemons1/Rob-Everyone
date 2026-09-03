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
    }
}
