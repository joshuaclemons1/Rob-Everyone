using System.Collections.Generic;
using Mirror;
using RobEveryone.AI;
using UnityEngine;
using UnityEngine.AI;

namespace RobEveryone.UI
{
    // Issue #51: replaces the old static single-house diorama with a
    // small non-networked stand-in neighborhood -- a ring of the real
    // house prefabs, plus a few wandering police stand-ins -- for the
    // Main Menu's background camera (MenuBackgroundCamera) to fly over.
    //
    // Deliberately NOT additively loading the real SampleScene (option 1
    // from the issue's own feasibility notes): PoliceAI is a
    // NetworkBehaviour that leans on a live RoundManager/server context
    // MainMenu doesn't have before a player clicks Play, and MainMenu
    // already runs its own NetworkManager for lobby purposes -- loading
    // the whole gameplay scene on top of that risks a collision for a
    // shot that's ultimately just cosmetic background. Everything here
    // is plain, client-only, non-networked content instead, built the
    // same way MenuCharacterPreview already builds its own isolated
    // preview stage: procedurally at runtime, not hand-placed in the
    // Editor -- a ring/circle is exact, reasoned-about math, not a set
    // of blind-placed Transforms nobody's actually seen render.
    public class MenuBackgroundBuilder : MonoBehaviour
    {
        [SerializeField] private List<GameObject> housePrefabs = new();
        [SerializeField] private GameObject policePrefab;

        [SerializeField] private int houseCount = 8;
        [SerializeField] private float ringRadius = 140f;

        [SerializeField] private int policeCount = 3;
        [SerializeField] private Vector2 policeWanderExtents = new(35f, 35f);

        [SerializeField] private float groundSize = 600f;
        [SerializeField] private Color groundColor = new(0.24f, 0.35f, 0.2f);

        // The old single-house diorama's root transforms (Roads, and the
        // Exterior/Fence/Grass bundle) -- disabled rather than deleted,
        // so this stays reversible instead of destructively editing
        // scene content that was hand-placed once already. Safe to
        // delete for real once this is confirmed working in the Editor.
        [SerializeField] private List<Transform> legacyDioramaRoots = new();

        private void Awake()
        {
            foreach (Transform root in legacyDioramaRoots)
            {
                if (root != null) root.gameObject.SetActive(false);
            }

            BuildGround();
            BuildHouseRing();
            SpawnPolice();
        }

        private void BuildGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "MenuBackgroundGround";
            ground.transform.SetParent(transform, false);
            // Unity's primitive Plane is 10x10 units at scale 1.
            ground.transform.localScale = Vector3.one * (groundSize / 10f);
            Destroy(ground.GetComponent<Collider>());
            TrySetColor(ground.GetComponent<Renderer>(), groundColor);
        }

        private void BuildHouseRing()
        {
            if (housePrefabs.Count == 0 || houseCount <= 0) return;

            // Evenly spaced on a circle -- at the defaults (8 houses,
            // radius 140) that's roughly 110 units between house centers,
            // comfortably clear of a house's own ~40x40 footprint
            // (HousePoolSpawner's own housePlotSize) with no overlap.
            float angleStep = 360f / houseCount;
            for (int i = 0; i < houseCount; i++)
            {
                float angleRad = i * angleStep * Mathf.Deg2Rad;
                Vector3 position = transform.position + OrbitOffset(angleRad, ringRadius);
                Quaternion rotation = Quaternion.LookRotation((transform.position - position).normalized, Vector3.up);

                GameObject prefab = housePrefabs[i % housePrefabs.Count];
                if (prefab == null) continue;

                Instantiate(prefab, position, rotation, transform);
            }
        }

        private void SpawnPolice()
        {
            if (policePrefab == null || policeCount <= 0) return;

            for (int i = 0; i < policeCount; i++)
            {
                Vector2 offset = Random.insideUnitCircle * 0.6f;
                Vector3 spawnPoint = transform.position + new Vector3(offset.x * policeWanderExtents.x, 0f, offset.y * policeWanderExtents.y);

                GameObject instance = Instantiate(policePrefab, spawnPoint, Quaternion.identity, transform);
                instance.name = $"MenuPoliceStandIn ({i})";
                StripNetworkComponents(instance);

                MenuPoliceWander wander = instance.AddComponent<MenuPoliceWander>();
                wander.Initialize(policeWanderExtents);
            }
        }

        // The real Police.prefab ships as a networked gameplay object
        // (NetworkIdentity, NetworkTransformReliable, the real PoliceAI
        // state machine, a NavMeshAgent) -- none of that is meant to run
        // outside a live server, and this stand-in ring has no baked
        // NavMesh under it anyway. Stripped down to just the model,
        // collider, and Animator (matching the same "purely cosmetic,
        // client-only" pattern already used elsewhere in this project --
        // see this script's own header comment), then MenuPoliceWander
        // takes over movement in their place.
        private static void StripNetworkComponents(GameObject instance)
        {
            DestroyIfPresent<PoliceAI>(instance);
            DestroyIfPresent<NetworkTransformReliable>(instance);
            DestroyIfPresent<NetworkIdentity>(instance);
            DestroyIfPresent<NavMeshAgent>(instance);
        }

        private static void DestroyIfPresent<T>(GameObject instance) where T : Component
        {
            T component = instance.GetComponent<T>();
            if (component != null) Destroy(component);
        }

        // URP's Lit shader names its color property "_BaseColor", not the
        // legacy "_Color" Material.color assumes -- checking both instead
        // of assuming one keeps this from silently doing nothing if the
        // default primitive material turns out to use either.
        private static void TrySetColor(Renderer renderer, Color color)
        {
            if (renderer == null) return;

            Material material = renderer.material;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private static Vector3 OrbitOffset(float angleRad, float radius)
        {
            return new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad)) * radius;
        }
    }
}
