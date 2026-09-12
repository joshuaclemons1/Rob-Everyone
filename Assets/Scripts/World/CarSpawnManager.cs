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

        // How many points to sample along the smooth curve between each
        // pair of hand-placed waypoints -- this is what actually fixes
        // "not enough points, doesn't follow the road": rather than
        // needing dozens of hand-placed waypoints to approximate a
        // curved road, a handful of them are treated as control points
        // for a Catmull-Rom spline (see BuildSmoothedPath/CatmullRom
        // below), densely sampled once at startup into the point list
        // CarDriver actually drives through. Higher = smoother curves,
        // more points for CarDriver to walk.
        [SerializeField] private int samplesPerSegment = 12;

        private readonly List<CarDriver> activeCars = new();
        // Computed once from lapWaypoints/originPoint and reused by every
        // car spawned off this same route -- the sparse waypoints never
        // change at runtime, so there's no reason to re-run the spline
        // sampling on every single spawn.
        private List<Vector3> smoothedPath;

        public override void OnStartServer()
        {
            if (originPoint != null && lapWaypoints.Count > 0)
            {
                smoothedPath = BuildSmoothedPath();
            }

            StartCoroutine(SpawnLoop());
        }

        // Control points are origin -> each waypoint in order -> origin
        // (matching the existing route), smoothly sampled through with a
        // Catmull-Rom spline instead of driven point-to-point directly --
        // the same sparse hand-placed waypoints produce a curved path
        // that actually follows a bending road, without needing to place
        // dozens of them by hand. Endpoints are clamped (duplicated)
        // rather than wrapped, since this is a there-and-back route, not
        // a closed loop.
        private List<Vector3> BuildSmoothedPath()
        {
            List<Vector3> controlPoints = new() { originPoint.position };
            foreach (Transform waypoint in lapWaypoints) controlPoints.Add(waypoint.position);
            controlPoints.Add(originPoint.position);

            List<Vector3> path = new();
            int count = controlPoints.Count;

            for (int i = 0; i < count - 1; i++)
            {
                Vector3 p1 = controlPoints[i];
                Vector3 p2 = controlPoints[i + 1];

                // Phantom points used only to estimate tangent direction,
                // not actually driven through. Confirmed bug: the origin
                // is typically way off to the side of the actual
                // waypoint layout (a spawn spoke feeding into a tight
                // loop/road shape) -- using it as the phantom for the
                // loop's own first/last segments pulled their tangents
                // toward a wildly distant, unrelated point and made the
                // curve balloon into a self-intersecting loop right at
                // that seam ("circles around the spawn point"). Reflecting
                // off the loop's own neighboring point instead keeps the
                // loop's curvature self-contained; the origin<->waypoint
                // segments themselves stay simple straight lines either
                // way (the coincident-point fallback in CatmullRom below).
                Vector3 p0 = i == 0 ? p1
                    : i == 1 ? p1 + (p1 - p2)
                    : controlPoints[i - 1];

                Vector3 p3 = i == count - 2 ? p2
                    : i == count - 3 ? p2 + (p2 - p1)
                    : controlPoints[i + 2];

                // The last segment also emits its final sample (t = 1,
                // i.e. the origin) -- every earlier segment leaves that
                // shared point to the next segment's t = 0 instead, so it
                // isn't duplicated in the middle of the path.
                int samples = i == count - 2 ? samplesPerSegment + 1 : samplesPerSegment;
                for (int s = 0; s < samples; s++)
                {
                    float t = s / (float)samplesPerSegment;
                    path.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }

            return path;
        }

        // Centripetal (alpha = 0.5) parameterization, not the naive
        // "uniform" formula (t, t^2, t^3 assuming every segment spans an
        // equal fraction of the curve) -- confirmed bug: uniform
        // Catmull-Rom is well known to loop back on itself when control
        // points are unevenly spaced, which the origin-to-first-waypoint
        // gap almost certainly is compared to the rest of the route
        // (cars were driving in circles right at the spawn point).
        // Centripetal parameterization (Barry & Goldman's recombination,
        // knots based on actual distance between points) eliminates that
        // looping/cusping regardless of spacing.
        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            const float alpha = 0.5f;
            float t0 = 0f;
            float t1 = t0 + Mathf.Pow(Vector3.Distance(p0, p1), alpha);
            float t2 = t1 + Mathf.Pow(Vector3.Distance(p1, p2), alpha);
            float t3 = t2 + Mathf.Pow(Vector3.Distance(p2, p3), alpha);

            // Coincident control points (the clamped phantom point at
            // either end of the whole route) would divide by zero below --
            // a straight blend between p1/p2 for just that boundary
            // segment is a perfectly reasonable fallback.
            if (t1 <= 0f || t2 <= t1 || t3 <= t2) return Vector3.Lerp(p1, p2, t);

            float u = Mathf.Lerp(t1, t2, t);

            Vector3 a1 = (t1 - u) / (t1 - t0) * p0 + (u - t0) / (t1 - t0) * p1;
            Vector3 a2 = (t2 - u) / (t2 - t1) * p1 + (u - t1) / (t2 - t1) * p2;
            Vector3 a3 = (t3 - u) / (t3 - t2) * p2 + (u - t2) / (t3 - t2) * p3;

            Vector3 b1 = (t2 - u) / (t2 - t0) * a1 + (u - t0) / (t2 - t0) * a2;
            Vector3 b2 = (t3 - u) / (t3 - t1) * a2 + (u - t1) / (t3 - t1) * a3;

            return (t2 - u) / (t2 - t1) * b1 + (u - t1) / (t2 - t1) * b2;
        }

        private IEnumerator SpawnLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(spawnCheckInterval);

                if (activeCars.Count >= maxConcurrentCars) continue;
                if (carPrefabs.Count == 0 || smoothedPath == null || originPoint == null) continue;
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

            driver.Init(smoothedPath, this);
            activeCars.Add(driver);
            NetworkServer.Spawn(instance);
        }

        public void NotifyCarDespawned(CarDriver driver)
        {
            activeCars.Remove(driver);
        }
    }
}
