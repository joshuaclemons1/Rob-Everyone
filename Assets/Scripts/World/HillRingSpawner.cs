using System.Collections.Generic;
using UnityEngine;

namespace RobEveryone.World
{
    // Places rolling background hills in a ring just outside the forest
    // (Stage 3i), further obscuring the flat horizon line before it reaches
    // the distant skyline ring. Purely visual -- placed well behind
    // ForestRingSpawner's BoundaryWall, so these never need their own
    // collision. Same even-by-perimeter placement as ForestRingSpawner (see
    // RoundedRectPerimeter), just sparser and with much more size variance
    // per instance, since real rolling hills are uneven, unlike a tree line
    // that needs solid coverage.
    public class HillRingSpawner : MonoBehaviour
    {
        [SerializeField] private List<GameObject> hillPrefabs = new();

        [SerializeField] private float innerHalfWidth = 160f;
        [SerializeField] private float innerHalfHeight = 210f;
        [SerializeField] private float outerHalfWidth = 220f;
        [SerializeField] private float outerHalfHeight = 270f;
        [SerializeField] private float cornerRadius = 50f;

        [SerializeField] private int ringCount = 2;
        [SerializeField] private int hillsPerRing = 30;

        [SerializeField] private float normalJitter = 15f;
        [SerializeField] private float perimeterJitter = 10f;
        [SerializeField] private Vector2 scaleRangeXZ = new(0.7f, 1.8f);
        [SerializeField] private Vector2 scaleRangeY = new(0.6f, 1.4f);

        private void Start()
        {
            if (hillPrefabs.Count == 0) return;

            for (int ring = 0; ring < ringCount; ring++)
            {
                float t = ringCount == 1 ? 0f : ring / (float)(ringCount - 1);
                float halfWidth = Mathf.Lerp(innerHalfWidth, outerHalfWidth, t);
                float halfHeight = Mathf.Lerp(innerHalfHeight, outerHalfHeight, t);
                SpawnRing(halfWidth, halfHeight);
            }
        }

        private void SpawnRing(float halfWidth, float halfHeight)
        {
            float radius = RoundedRectPerimeter.ClampCornerRadius(cornerRadius, halfWidth, halfHeight);
            float perimeter = RoundedRectPerimeter.Perimeter(halfWidth, halfHeight, radius);
            float step = perimeter / hillsPerRing;

            for (int i = 0; i < hillsPerRing; i++)
            {
                float s = i * step + Random.Range(-perimeterJitter, perimeterJitter);
                RoundedRectPerimeter.PointOnPerimeter(halfWidth, halfHeight, radius, s, perimeter, out Vector3 localPos, out Vector3 outwardNormal);

                Vector3 position = transform.position + localPos + outwardNormal * Random.Range(-normalJitter, normalJitter);

                GameObject prefab = hillPrefabs[Random.Range(0, hillPrefabs.Count)];
                GameObject hill = Instantiate(prefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);

                float scaleXZ = Random.Range(scaleRangeXZ.x, scaleRangeXZ.y);
                float scaleY = Random.Range(scaleRangeY.x, scaleRangeY.y);
                Vector3 baseScale = prefab.transform.localScale;
                hill.transform.localScale = new Vector3(baseScale.x * scaleXZ, baseScale.y * scaleY, baseScale.z * scaleXZ);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.45f, 0.35f, 0.15f, 0.8f);
            RoundedRectPerimeter.DrawGizmo(transform.position, innerHalfWidth, innerHalfHeight, cornerRadius);
            RoundedRectPerimeter.DrawGizmo(transform.position, outerHalfWidth, outerHalfHeight, cornerRadius);

            Gizmos.color = new Color(0.45f, 0.35f, 0.15f, 0.35f);
            for (int ring = 0; ring < ringCount; ring++)
            {
                float t = ringCount == 1 ? 0f : ring / (float)(ringCount - 1);
                RoundedRectPerimeter.DrawGizmo(transform.position, Mathf.Lerp(innerHalfWidth, outerHalfWidth, t), Mathf.Lerp(innerHalfHeight, outerHalfHeight, t), cornerRadius);
            }
        }
    }
}
