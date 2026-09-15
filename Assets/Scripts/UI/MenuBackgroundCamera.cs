using UnityEngine;

namespace RobEveryone.UI
{
    // Issue #51: the "moving drone shot" flythrough itself. Attached
    // directly to the Main Menu's existing background Main Camera
    // (previously completely static, parked behind the old diorama).
    //
    // A pure circular orbit around orbitCenter rather than a hand-
    // authored waypoint spline -- no Cinemachine package is installed in
    // this project (see the issue's own notes), and a parametric sin/cos
    // loop has a real advantage over a hand-placed path here: it's
    // mathematically guaranteed to loop with zero seam, and every value
    // is a plain tunable number instead of a set of blind-placed
    // waypoint Transforms nobody's actually seen render. A slight
    // "look ahead" offset and a slow vertical bob keep it from reading
    // as a camera locked to a rail -- more like a drone actually flying
    // a route than a fixed rotating spotlight.
    public class MenuBackgroundCamera : MonoBehaviour
    {
        [SerializeField] private Vector3 orbitCenter = Vector3.zero;
        [SerializeField] private float orbitRadius = 110f;
        [SerializeField] private float orbitHeight = 65f;
        [SerializeField] private float orbitSpeedDegreesPerSecond = 2.5f;
        [SerializeField] private float lookAheadDegrees = 20f;
        [SerializeField] private float lookTargetRadiusFraction = 0.35f;
        [SerializeField] private float bobAmplitude = 3f;
        [SerializeField] private float bobCyclesPerSecond = 0.05f;

        private float angleDegrees;

        private void Start()
        {
            // Randomized start angle so the menu doesn't look identical
            // every single time it loads.
            angleDegrees = Random.Range(0f, 360f);
        }

        private void Update()
        {
            angleDegrees += orbitSpeedDegreesPerSecond * Time.deltaTime;

            float angleRad = angleDegrees * Mathf.Deg2Rad;
            float height = orbitHeight + Mathf.Sin(Time.time * bobCyclesPerSecond * Mathf.PI * 2f) * bobAmplitude;
            Vector3 position = orbitCenter + OrbitOffset(angleRad, orbitRadius) + Vector3.up * height;
            transform.position = position;

            float lookAngleRad = (angleDegrees + lookAheadDegrees) * Mathf.Deg2Rad;
            Vector3 lookTarget = orbitCenter + OrbitOffset(lookAngleRad, orbitRadius * lookTargetRadiusFraction);
            Vector3 lookDirection = lookTarget - position;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            }
        }

        private static Vector3 OrbitOffset(float angleRad, float radius)
        {
            return new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad)) * radius;
        }
    }
}
