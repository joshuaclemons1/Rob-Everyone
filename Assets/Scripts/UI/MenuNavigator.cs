using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RobEveryone.UI
{
    // Drives the Main Menu's slide+dim panel navigation (Main -> Play ->
    // Customization), per main-menu-visual-design.md's Navigation flow.
    // Each panel needs a RectTransform (stretched to fill the canvas,
    // anchored position (0,0) when "home") -- a CanvasGroup is added
    // automatically if missing. NavigateTo slides the current panel
    // partway left and dims it (stays visible, stops being interactable)
    // while the target panel slides in from off-screen-right to center;
    // NavigateBack reverses whichever panel is currently showing back to
    // its parent in the history stack.
    //
    // Deliberately out of scope for this pass (see plan.md/todo.md):
    // Settings stays a flat SetActive swap via MenuActions, not part of
    // this stack -- per the design doc it's a separate full-screen branch
    // off Main, not a slide transition. The title's continuous pulse and
    // Settings' fall-through-frame/fall-from-sky character animation
    // aren't built here either.
    public class MenuNavigator : MonoBehaviour
    {
        [SerializeField] private float slideDuration = 0.5f;
        // Matches the Canvas Scaler's reference resolution width used
        // everywhere else in this project (3840x2160) -- one full
        // "off-screen" slide distance.
        [SerializeField] private float slideDistance = 3840f;
        // File-tree feel by default: the outgoing panel just nudges
        // aside and stays clearly visible, rather than sliding a third
        // of the screen away and dimming to half black. Both are live-
        // tunable per instance in the Inspector -- changing these
        // defaults doesn't retroactively touch a MenuNavigator you've
        // already added to a scene, since Unity serializes the values it
        // had at add-time.
        [SerializeField, Range(0f, 1f)] private float outgoingSlideFraction = 0.1f;
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.85f;

        private readonly Stack<RectTransform> history = new();
        private RectTransform current;
        private Coroutine activeTransition;

        // Call once (e.g. from Awake) with whichever panel starts active
        // -- Main Menu itself.
        public void SetInitial(RectTransform initialPanel)
        {
            current = initialPanel;
            CanvasGroup group = GetOrAddCanvasGroup(current);
            current.anchoredPosition = Vector2.zero;
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        // Wire a button's OnClick directly to this -- Unity's Button
        // Inspector lets you pick a specific RectTransform as the
        // dynamic argument, no per-panel wrapper method needed.
        public void NavigateTo(RectTransform next)
        {
            if (activeTransition != null || current == null || next == null || next == current) return;

            history.Push(current);
            activeTransition = StartCoroutine(Slide(current, next, forward: true));
        }

        public void NavigateBack()
        {
            if (activeTransition != null || history.Count == 0) return;

            RectTransform previous = history.Pop();
            activeTransition = StartCoroutine(Slide(current, previous, forward: false));
        }

        private IEnumerator Slide(RectTransform outgoing, RectTransform incoming, bool forward)
        {
            incoming.gameObject.SetActive(true);
            CanvasGroup incomingGroup = GetOrAddCanvasGroup(incoming);
            CanvasGroup outgoingGroup = GetOrAddCanvasGroup(outgoing);

            incomingGroup.interactable = false;
            incomingGroup.blocksRaycasts = false;
            outgoingGroup.interactable = false;
            outgoingGroup.blocksRaycasts = false;

            float incomingStartX = forward ? slideDistance : -slideDistance * outgoingSlideFraction;
            float outgoingEndX = forward ? -slideDistance * outgoingSlideFraction : slideDistance;
            float outgoingStartX = outgoing.anchoredPosition.x;

            float outgoingStartAlpha = outgoingGroup.alpha;
            float outgoingEndAlpha = forward ? dimAlpha : 1f;
            float incomingStartAlpha = forward ? 1f : dimAlpha;

            incoming.anchoredPosition = new Vector2(incomingStartX, 0f);
            incomingGroup.alpha = incomingStartAlpha;

            float t = 0f;
            while (t < slideDuration)
            {
                t += Time.deltaTime;
                float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / slideDuration), 3f); // ease-out cubic

                outgoing.anchoredPosition = new Vector2(Mathf.Lerp(outgoingStartX, outgoingEndX, eased), 0f);
                incoming.anchoredPosition = new Vector2(Mathf.Lerp(incomingStartX, 0f, eased), 0f);
                outgoingGroup.alpha = Mathf.Lerp(outgoingStartAlpha, outgoingEndAlpha, eased);
                incomingGroup.alpha = Mathf.Lerp(incomingStartAlpha, 1f, eased);

                yield return null;
            }

            outgoing.anchoredPosition = new Vector2(outgoingEndX, 0f);
            incoming.anchoredPosition = Vector2.zero;
            outgoingGroup.alpha = outgoingEndAlpha;
            incomingGroup.alpha = 1f;

            incomingGroup.interactable = true;
            incomingGroup.blocksRaycasts = true;

            if (forward)
            {
                // Outgoing panel stays active, dimmed, non-interactable --
                // "stays partly visible" per the design doc, not hidden.
            }
            else
            {
                outgoing.gameObject.SetActive(false);
            }

            current = incoming;
            activeTransition = null;
        }

        private static CanvasGroup GetOrAddCanvasGroup(RectTransform target)
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            if (group == null) group = target.gameObject.AddComponent<CanvasGroup>();
            return group;
        }
    }
}
