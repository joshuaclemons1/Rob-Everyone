using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RobEveryone.UI
{
    // Request 2 (menu preview follow-up to #39): click-and-drag on the
    // character preview spins it around Y to see other angles; letting
    // go of the mouse eases it back to its original facing instead of
    // leaving it wherever the drag stopped.
    //
    // Attached automatically by MenuCharacterPreview.BuildStage onto the
    // same RawImage GameObject the Inspector's Preview Image field
    // already points at -- no extra manual Editor step needed, and it
    // stays correctly targeted even if that RawImage gets moved/resized
    // later since it's found by component, not by scene position.
    //
    // Targets a dedicated SpinPivot transform (passed in via Initialize),
    // never the model instance itself -- the model gets destroyed and
    // recreated on every skin change (see MenuCharacterPreview.SpawnModel),
    // which would silently drop whatever the user was mid-drag on.
    public class MenuCharacterPreviewDrag : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private float degreesPerPixel = 0.3f;
        [SerializeField] private float returnDuration = 0.4f;

        private Transform target;
        private Quaternion restRotation;
        private bool dragging;
        private Coroutine returnCoroutine;

        public void Initialize(Transform spinTarget)
        {
            target = spinTarget;
            if (target != null) restRotation = target.localRotation;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (target == null) return;

            if (returnCoroutine != null)
            {
                StopCoroutine(returnCoroutine);
                returnCoroutine = null;
            }

            dragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || target == null) return;

            // World-space Y so the spin axis stays vertical regardless of
            // whatever the pivot's own current rotation is -- a Self-space
            // rotation would tilt the spin axis as soon as it's not at
            // rest, making later drags feel like they're fighting the
            // model.
            target.Rotate(Vector3.up, -eventData.delta.x * degreesPerPixel, Space.World);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!dragging) return;

            dragging = false;
            if (target == null) return;

            returnCoroutine = StartCoroutine(ReturnToRest());
        }

        private IEnumerator ReturnToRest()
        {
            Quaternion start = target.localRotation;
            float elapsed = 0f;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / returnDuration), 3f); // ease-out cubic
                target.localRotation = Quaternion.Slerp(start, restRotation, eased);
                yield return null;
            }

            target.localRotation = restRotation;
            returnCoroutine = null;
        }
    }
}
