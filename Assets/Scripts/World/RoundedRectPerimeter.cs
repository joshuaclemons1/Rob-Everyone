using UnityEngine;

namespace RobEveryone.World
{
    // Shared geometry for anything placed as a ring around the map --
    // ForestRingSpawner and HillRingSpawner both walk the perimeter of a
    // rounded rectangle (not a circle, since the map itself is rectangular)
    // evenly by arc length rather than by angle, so spacing stays even on
    // the straight sides and through the rounded corners alike.
    public static class RoundedRectPerimeter
    {
        public static float Perimeter(float halfWidth, float halfHeight, float cornerRadius)
        {
            float a = halfWidth - cornerRadius;
            float b = halfHeight - cornerRadius;
            return 4f * a + 4f * b + 2f * Mathf.PI * cornerRadius;
        }

        // Clamps a requested corner radius so it never exceeds either half
        // extent -- otherwise the "straight" segments would have negative
        // length.
        public static float ClampCornerRadius(float cornerRadius, float halfWidth, float halfHeight)
        {
            return Mathf.Min(cornerRadius, halfWidth, halfHeight);
        }

        // Walks the rounded rectangle's perimeter starting at the middle of
        // the top edge, going clockwise: top-right corner arc, right edge,
        // bottom-right corner arc, bottom edge, bottom-left corner arc, left
        // edge, top-left corner arc, top edge (closes the loop). Returns a
        // position local to whatever Transform the caller adds this to,
        // plus the outward-facing normal at that point (useful for jitter
        // or facing rotation).
        public static void PointOnPerimeter(float halfWidth, float halfHeight, float cornerRadius, float s, float perimeter, out Vector3 localPos, out Vector3 outwardNormal)
        {
            float a = halfWidth - cornerRadius;
            float b = halfHeight - cornerRadius;
            float cornerArcLength = (Mathf.PI / 2f) * cornerRadius;

            float[] segmentLengths = { cornerArcLength, 2f * b, cornerArcLength, 2f * a, cornerArcLength, 2f * b, cornerArcLength, 2f * a };
            Vector2[] cornerCenters = { new(a, b), new(a, -b), new(-a, -b), new(-a, b) };
            float[] cornerStartAngle = { 90f, 0f, -90f, -180f };

            s = ((s % perimeter) + perimeter) % perimeter;

            int segmentIndex = 0;
            float remaining = s;
            while (segmentIndex < segmentLengths.Length - 1 && remaining > segmentLengths[segmentIndex])
            {
                remaining -= segmentLengths[segmentIndex];
                segmentIndex++;
            }

            Vector2 point;
            Vector2 normal;

            if (segmentIndex % 2 == 0)
            {
                // Corner arc.
                int cornerIndex = segmentIndex / 2;
                float u = Mathf.Clamp01(remaining / cornerArcLength);
                float angle = (cornerStartAngle[cornerIndex] - 90f * u) * Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                point = cornerCenters[cornerIndex] + direction * cornerRadius;
                normal = direction;
            }
            else
            {
                // Straight edge: 0 = right, 1 = bottom, 2 = left, 3 = top.
                int edgeIndex = segmentIndex / 2;
                float u = Mathf.Clamp01(remaining / segmentLengths[segmentIndex]);

                switch (edgeIndex)
                {
                    case 0:
                        point = new Vector2(a + cornerRadius, Mathf.Lerp(b, -b, u));
                        normal = new Vector2(1f, 0f);
                        break;
                    case 1:
                        point = new Vector2(Mathf.Lerp(a, -a, u), -(b + cornerRadius));
                        normal = new Vector2(0f, -1f);
                        break;
                    case 2:
                        point = new Vector2(-(a + cornerRadius), Mathf.Lerp(-b, b, u));
                        normal = new Vector2(-1f, 0f);
                        break;
                    default:
                        point = new Vector2(Mathf.Lerp(-a, a, u), b + cornerRadius);
                        normal = new Vector2(0f, 1f);
                        break;
                }
            }

            localPos = new Vector3(point.x, 0f, point.y);
            outwardNormal = new Vector3(normal.x, 0f, normal.y);
        }

        // Draws a wireframe of the rounded rectangle in the Scene view --
        // shared by both spawners' OnDrawGizmos so the preview code doesn't
        // need to duplicate the segment-walking logic either.
        public static void DrawGizmo(Vector3 origin, float halfWidth, float halfHeight, float cornerRadius, int segments = 64)
        {
            cornerRadius = ClampCornerRadius(cornerRadius, halfWidth, halfHeight);
            float perimeter = Perimeter(halfWidth, halfHeight, cornerRadius);

            PointOnPerimeter(halfWidth, halfHeight, cornerRadius, 0f, perimeter, out Vector3 first, out _);
            Vector3 previous = origin + first;

            for (int i = 1; i <= segments; i++)
            {
                PointOnPerimeter(halfWidth, halfHeight, cornerRadius, perimeter / segments * i, perimeter, out Vector3 next, out _);
                Vector3 worldNext = origin + next;
                Gizmos.DrawLine(previous, worldNext);
                previous = worldNext;
            }
        }
    }
}
