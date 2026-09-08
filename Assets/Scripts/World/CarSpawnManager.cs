using System.Collections;
using System.Collections.Generic;
using Mirror;
using RobEveryone.AI;
using UnityEngine;

namespace RobEveryone.World
{
    // Periodically rolls a chance to spawn a car at originPoint, which then
    // drives one lap of waypoints (in order) and returns to originPoint to
    // despawn itself (see CarDriver). Deliberately not "always N cars
    // patrolling forever" -- per design, sometimes there should be zero
    // cars at all, so the road isn't a guaranteed hazard every single time
    // a player crosses it.
    //
    // Networking (Stage 4): server-only spawner (NetworkBehaviour +
    // isServer guard) -- every client needs to see the *same* cars on the
    // *same* laps, not each roll its own independent traffic, so this
    // can't run on every client the way it did single-player.
    // NetworkServer.Spawn (instead of a plain Instantiate) is what makes
    // the resulting car visible to already-connected clients at all --
    // Mirror doesn't automatically network a GameObject just because it
    // has NetworkBehaviour components, spawning has to be requested
    // explicitly. Car prefabs need registering as Spawnable Prefabs on
    // the NetworkManager (see stage4-multiplayer-mirror.md Part 7).
    public class CarSpawnManager : NetworkBehaviour
    {
        [SerializeField] private List<GameObject> carPrefabs = new();
        [SerializeField] private List<Transform> lapWaypoints = new();
        [SerializeField] private Transform originPoint;

        [SerializeField] private int maxConcurrentCars = 2;
        [SerializeField] private float spawnCheckInterval = 20f;
        [SerializeField, Range(0f, 1f)] private float spawnChance = 0.5f;

        private readonly List<CarDriver> activeCars = new();

        public override void OnStartServer()
        {
            StartCoroutine(SpawnLoop());
        }

        private IEnumerator SpawnLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(spawnCheckInterval);

                if (activeCars.Count >= maxConcurrentCars) continue;
                if (carPrefabs.Count == 0 || lapWaypoints.Count == 0 || originPoint == null) continue;
                if (Random.value > spawnChance) continue;

                SpawnCar();
            }
        }

        private void SpawnCar()
        {
            GameObject prefab = carPrefabs[Random.Range(0, carPrefabs.Count)];
            GameObject instance = Instantiate(prefab, originPoint.position, originPoint.rotation);

            CarDriver driver = instance.GetComponent<CarDriver>();
            if (driver == null)
            {
                Debug.LogWarning($"{prefab.name} is in Car Prefabs but has no CarDriver component -- destroying.");
                Destroy(instance);
                return;
            }

            driver.Init(lapWaypoints, originPoint, this);
            activeCars.Add(driver);
            NetworkServer.Spawn(instance);
        }

        public void NotifyCarDespawned(CarDriver driver)
        {
            activeCars.Remove(driver);
        }
    }
}
