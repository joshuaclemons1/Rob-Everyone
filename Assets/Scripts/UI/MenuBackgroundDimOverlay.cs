using UnityEngine;
using UnityEngine.UI;

namespace RobEveryone.UI
{
    // Issue #51: darkens the moving background so the button stack,
    // title, and character preview stay legible over it -- see the
    // issue's own "don't lose menu legibility" note. A single semi-
    // transparent full-screen Image, built at runtime and forced to the
    // very back of the Canvas's sibling order (SetAsFirstSibling) so it
    // always renders behind every panel regardless of what order they
    // were added in the Editor -- same "build it in code instead of
    // hand-placing it" approach MenuCharacterPreview already used for
    // its own preview stage.
    public class MenuBackgroundDimOverlay : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.45f;

        private void Awake()
        {
            var overlayGO = new GameObject("BackgroundDimOverlay", typeof(RectTransform), typeof(Image));
            overlayGO.transform.SetParent(transform, false);
            overlayGO.transform.SetAsFirstSibling();

            var rect = (RectTransform)overlayGO.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = overlayGO.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, dimAlpha);
            // Never blocks clicks -- it's purely a visual dimmer sitting
            // behind every real panel/button, not an interactive layer.
            image.raycastTarget = false;
        }
    }
}
