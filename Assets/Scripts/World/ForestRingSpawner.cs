using System.Collections.Generic;
using UnityEngine;

namespace RobEveryone.World
{
    // Fills the gap between the house ring (Stage 3g/3h) and the distant
    // background skyline (Stage 3i) with a dense multi-row treeline, so the
    // map feels enclosed instead of having empty grass between "the map"
    // and "the background." Trees are placed on evenly-spaced concentric
    // rounded-rectangle rings (see RoundedRectPerimeter) rather than a fully
    // random scatter, for even coverage -- but tree colliders alone still
    // leave walk-through gaps in places (trunk/canopy collision isn't a
    // perfect tiled wall), so this also spawns a small number of plain
    // invisible BoxColliders around the same outer rectangle as the actual,
    // guaranteed-solid world boundary. The trees are why a player can't
    // see past the map edge; the boxes are why they can't walk past it.
    public class ForestRingSpawner : MonoBehaviour
    {
        [SerializeField] private List<GameObject> treePrefabs = new();

        [SerializeField] private float innerHalfWidth = 100f;
        [SerializeField] private float innerHalfHeight = 150f;
        [SerializeField] private float outerHalfWidth = 140f;
        [SerializeField] private float outerHalfHeight = 190f;
        [SerializeField] private float cornerRadius = 40f;

        [SerializeField] private int ringCount = 4;
        [SerializeField] private int treesPerRing = 90;

        [SerializeField] private float normalJitter = 4f;
        [SerializeField] private float perimeterJitter = 3f;
        [SerializeField] private float uniformScaleMin = 0.85f;
        [SerializeField] private float uniformScaleMax = 1.3f;

        // Confirmed bug: trees spawned directly on top of other placed
        // background props (houses, roads, other decoration) since
        // nothing ever checked. Leave avoidLayers at Nothing (the
        // default) to skip the check entirely -- assign it to whatever
        // layer your other placed objects use (e.g. a new "Environment"
        // layer) to actually avoid them. avoidCheckRadius only needs to
        // cover the trunk footprint, not the whole canopy -- canopies
        // overlapping is normal for a forest, spawning inside a house
        // isn't.
        [Header("Overlap Avoidance")]
        [SerializeField] private LayerMask avoidLayers;
        [SerializeField] private float avoidCheckRadius = 2f;
        [SerializeField] private int maxOverlapRetries = 5;

        [Header("World Boundary")]
        [SerializeField] private bool generateBoundaryWall = true;
        [SerializeField] private float boundaryWallHeight = 25f;
        [SerializeField] private float boundaryWallThickness = 4f;

        private void Start()
        {
            if (treePrefabs.Count > 0)
            {
                for (int ring = 0; ring < ringCount; ring++)
                {
                    float t = ringCount == 1 ? 0f : ring / (float)(ringCount - 1);
                    float halfWidth = Mathf.Lerp(innerHalfWidth, outerHalfWidth, t);
                    float halfHeight = Mathf.Lerp(innerHalfHeight, outerHalfHeight, t);
                    SpawnTreeRing(halfWidth, halfHeight);
                }
            }

            if (generateBoundaryWall)
            {
                GenerateBoundaryWall();
            }
        }

        private void SpawnTreeRing(float halfWidth, float halfHeight)
        {
            float radius = RoundedRectPerimeter.ClampCornerRadius(cornerRadius, halfWidth, halfHeight);
            float perimeter = RoundedRectPerimeter.Perimeter(halfWidth, halfHeight, radius);
            float step = perimeter / treesPerRing;

            for (int i = 0; i < treesPerRing; i++)
            {
                if (!TryFindClearPosition(halfWidth, halfHeight, radius, perimeter, i * step, out Vector3 position)) continue;

                GameObject prefab = treePrefabs[Random.Range(0, treePrefabs.Count)];
                GameObject tree = Instantiate(prefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);

                float scale = Random.Range(uniformScaleMin, uniformScaleMax);
                tree.transform.localScale = prefab.transform.localScale * scale;
            }
        }

        // Retries a few times with fresh jitter before giving up on this
        // one ring slot entirely -- skipping one tree out of dozens per
        // ring is invisible; forcing it to overlap an existing prop
        // wouldn't be. avoidLayers left at Nothing (the default) makes
        // this always succeed on the first try, i.e. a no-op.
        private bool TryFindClearPosition(float halfWidth, float halfHeight, float radius, float perimeter, float baseArcLength, out Vector3 position)
        {
            for (int attempt = 0; attempt < maxOverlapRetries; attempt++)
            {
                float s = baseArcLength + Random.Range(-perimeterJitter, perimeterJitter);
                RoundedRectPerimeter.PointOnPerimeter(halfWidth, halfHeight, radius, s, perimeter, out Vector3 localPos, out Vector3 outwardNormal);
                Vector3 candidate = transform.position + localPos + outwardNormal * Random.Range(-normalJitter, normalJitter);

                if (avoidLayers == 0 || !Physics.CheckSphere(candidate, avoidCheckRadius, avoidLayers, QueryTriggerInteraction.Ignore))
                {
                    position = candidate;
                    return true;
                }
            }

            position = default;
            return false;
        }

        // A plain rectangle (sharp corners, ignoring cornerRadius entirely)
        // sized to the outer tree ring -- nobody ever sees this, so there's
        // no reason to round it to match; four BoxColliders is simpler and
        // just as solid as trying to approximate rounded corners with more
        // pieces. Placed at the *outer* ring so the player can still walk
        // into and through the treeline itself (it should feel like a
        // forest, not like bouncing off a wall at the first row of trees).
        private void GenerateBoundaryWall()
        {
            GameObject wall = new("BoundaryWall");
            wall.transform.SetParent(transform, false);

            float halfWidth = outerHalfWidth;
            float halfHeight = outerHalfHeight;
            float centerY = boundaryWallHeight * 0.5f;

            AddWallSegment(wall.transform, new Vector3(0f, centerY, halfHeight), new Vector3(halfWidth * 2f + boundaryWallThickness, boundaryWallHeight, boundaryWallThickness));
            AddWallSegment(wall.transform, new Vector3(0f, centerY, -halfHeight), new Vector3(halfWidth * 2f + boundaryWallThickness, boundaryWallHeight, boundaryWallThickness));
            AddWallSegment(wall.transform, new Vector3(halfWidth, centerY, 0f), new Vector3(boundaryWallThickness, boundaryWallHeight, halfHeight * 2f + boundaryWallThickness));
            AddWallSegment(wall.transform, new Vector3(-halfWidth, centerY, 0f), new Vector3(boundaryWallThickness, boundaryWallHeight, halfHeight * 2f + boundaryWallThickness));
        }

        private void AddWallSegment(Transform parent, Vector3 localPosition, Vector3 size)
        {
            GameObject segment = new("Segment");
            segment.transform.SetParent(parent, false);
            segment.transform.localPosition = localPosition;

            BoxCollider box = segment.AddComponent<BoxCollider>();
            box.size = size;
        }

        // Preview the ring band (and the boundary wall footprint) in the
        // Scene view at all times, not just Play mode, so it can be tuned
        // against the actual house ring and skyline without pressing Play --
        // same idea as HousePoolSpawner's plot gizmos.
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.15f, 0.55f, 0.2f, 0.8f);
            RoundedRectPerimeter.DrawGizmo(transform.position, innerHalfWidth, innerHalfHeight, cornerRadius);
            RoundedRectPerimeter.DrawGizmo(transform.position, outerHalfWidth, outerHalfHeight, cornerRadius);

            Gizmos.color = new Color(0.15f, 0.55f, 0.2f, 0.35f);
            for (int ring = 0; ring < ringCount; ring++)
            {
                float t = ringCount == 1 ? 0f : ring / (float)(ringCount - 1);
                RoundedRectPerimeter.DrawGizmo(transform.position, Mathf.Lerp(innerHalfWidth, outerHalfWidth, t), Mathf.Lerp(innerHalfHeight, outerHalfHeight, t), cornerRadius);
            }

            if (generateBoundaryWall)
            {
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
                Vector3 center = transform.position + new Vector3(0f, boundaryWallHeight * 0.5f, 0f);
                Gizmos.DrawWireCube(center, new Vector3(outerHalfWidth * 2f, boundaryWallHeight, outerHalfHeight * 2f));
            }
        }
    }
}
