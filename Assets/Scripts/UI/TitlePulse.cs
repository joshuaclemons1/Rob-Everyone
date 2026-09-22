using UnityEngine;

namespace RobEveryone.UI
{
    // Issue #39: the Main Menu title's continuous idle pulse -- a gentle,
    // ongoing scale "breathing" so the title doesn't sit completely
    // static while a player looks at the menu. Deliberately just a scale
    // animation on this object's own Transform (works whether the title
    // is one Image or, like the current MainMenuPanel/TitleWordmark
    // hierarchy, a container grouping a few child pieces -- scaling the
    // container scales all of them together, no per-child wiring needed).
    public class TitlePulse : MonoBehaviour
    {
        [SerializeField] private float pulseCyclesPerSecond = 0.5f;
        [SerializeField, Range(0f, 0.5f)] private float pulseAmplitude = 0.04f;

        private Vector3 baseScale;

        private void Awake()
        {
            baseScale = transform.localScale;
        }

        // Time.time-driven, not an accumulated phase -- this has no
        // start/stop/intensity to smooth (unlike FirstPersonController's
        // view bob), it's just a continuous idle loop for as long as the
        // object is enabled, so there's nothing an accumulator buys here
        // that a direct Sin(Time.time) doesn't already give for free.
        private void Update()
        {
            float scale = 1f + Mathf.Sin(Time.time * pulseCyclesPerSecond * Mathf.PI * 2f) * pulseAmplitude;
            transform.localScale = baseScale * scale;
        }
    }
}
