using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RobEveryone.UI
{
    // Drives the Main Menu's slide+dim panel navigation (Main -> Play ->
    // Customization), per main-menu-visual-design.md's Navigation flow.
    // Each panel needs a RectTransform (stretched to fill the canvas,
    // anchored position (0,0) when "home") -- a CanvasGroup is added
    // automatically if missing.
    //
    // A "stacking list" effect (file-tree style), not a two-panel
    // overlap: every panel currently behind the active one animates
    // together each time NavigateTo/NavigateBack runs, so depth 1 sits
    // one slide-increment left, depth 2 sits two increments left, and so
    // on -- rather than every background panel independently sliding to
    // the same fixed offset and landing on top of each other.
    //
    // Settings still isn't part of this stack -- per the design doc it's
    // a separate full-screen branch off Main, not one more entry in this
    // horizontal slide. It gets its own vertical slide animation instead,
    // driven by MenuActions (see that script's SettingsSlideRoutine) --
    // not a flat SetActive swap anymore, just not this particular
    // animation. The title's continuous pulse and Settings' fall-through-
    // frame/fall-from-sky character animation aren't built here either.
    public class MenuNavigator : MonoBehaviour
    {
        [SerializeField] private float slideDuration = 0.5f;
        // Matches the Canvas Scaler's reference resolution width used
        // everywhere else in this project (3840x2160) -- one full
        // "off-screen" slide distance, and the unit outgoingSlideFraction
        // multiplies against for each step back in the stack.
        [SerializeField] private float slideDistance = 3840f;
        // File-tree feel by default: each background panel just nudges
        // aside and stays clearly visible, rather than sliding a third of
        // the screen away and dimming to half black. Both are live-
        // tunable per instance in the Inspector -- changing these
        // defaults doesn't retroactively touch a MenuNavigator you've
        // already added to a scene, since Unity serializes the values it
        // had at add-time.
        [SerializeField, Range(0f, 1f)] private float outgoingSlideFraction = 0.08f;
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.85f;

        // Ordered nearest-to-farthest: index 0 is the current, fully
        // visible/interactable panel; index 1 is one step back, index 2
        // two steps back, etc.
        private readonly List<RectTransform> stack = new();
        private Coroutine activeTransition;

        // Call once (e.g. from MenuActions.Awake) with whichever panel
        // starts active -- Main Menu itself.
        public void SetInitial(RectTransform initialPanel)
        {
            stack.Clear();
            if (initialPanel == null) return;

            stack.Add(initialPanel);
            CanvasGroup group = GetOrAddCanvasGroup(initialPanel);
            initialPanel.gameObject.SetActive(true);
            initialPanel.anchoredPosition = Vector2.zero;
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        // Wire a button's OnClick directly to this -- Unity's Button
        // Inspector lets you pick a specific RectTransform as the
        // dynamic argument, no per-panel wrapper method needed.
        public void NavigateTo(RectTransform next)
        {
            if (activeTransition != null || next == null || stack.Count == 0 || stack.Contains(next)) return;

            activeTransition = StartCoroutine(Animate(next, forward: true));
        }

        public void NavigateBack()
        {
            if (activeTransition != null || stack.Count <= 1) return;

            activeTransition = StartCoroutine(Animate(null, forward: false));
        }

        private IEnumerator Animate(RectTransform next, bool forward)
        {
            List<RectTransform> newStack;
            RectTransform leaving = null;

            if (forward)
            {
                newStack = new List<RectTransform>(stack.Count + 1) { next };
                newStack.AddRange(stack);

                next.gameObject.SetActive(true);
                CanvasGroup incomingGroup = GetOrAddCanvasGroup(next);
                next.anchoredPosition = new Vector2(slideDistance, 0f);
                incomingGroup.alpha = 1f;
            }
            else
            {
                leaving = stack[0];
                newStack = stack.GetRange(1, stack.Count - 1);
            }

            // Lock out input on everything involved for the duration.
            foreach (RectTransform t in newStack)
            {
                CanvasGroup g = GetOrAddCanvasGroup(t);
                g.interactable = false;
                g.blocksRaycasts = false;
            }
            if (leaving != null)
            {
                CanvasGroup g = GetOrAddCanvasGroup(leaving);
                g.interactable = false;
                g.blocksRaycasts = false;
            }

            var startPos = new Dictionary<RectTransform, float>();
            var startAlpha = new Dictionary<RectTransform, float>();
            foreach (RectTransform t in newStack)
            {
                startPos[t] = t.anchoredPosition.x;
                startAlpha[t] = GetOrAddCanvasGroup(t).alpha;
            }
            if (leaving != null)
            {
                startPos[leaving] = leaving.anchoredPosition.x;
            }

            float elapsed = 0f;
            while (elapsed < slideDuration)
            {
                elapsed += Time.deltaTime;
                float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / slideDuration), 3f); // ease-out cubic

                for (int i = 0; i < newStack.Count; i++)
                {
                    RectTransform t = newStack[i];
                    float targetX = -i * slideDistance * outgoingSlideFraction;
                    float targetAlpha = i == 0 ? 1f : dimAlpha;

                    t.anchoredPosition = new Vector2(Mathf.Lerp(startPos[t], targetX, eased), 0f);
                    GetOrAddCanvasGroup(t).alpha = Mathf.Lerp(startAlpha[t], targetAlpha, eased);
                }

                if (leaving != null)
                {
                    leaving.anchoredPosition = new Vector2(Mathf.Lerp(startPos[leaving], slideDistance, eased), 0f);
                }

                yield return null;
            }

            for (int i = 0; i < newStack.Count; i++)
            {
                RectTransform t = newStack[i];
                t.anchoredPosition = new Vector2(-i * slideDistance * outgoingSlideFraction, 0f);
                GetOrAddCanvasGroup(t).alpha = i == 0 ? 1f : dimAlpha;
            }

            if (leaving != null)
            {
                leaving.anchoredPosition = new Vector2(slideDistance, 0f);
                leaving.gameObject.SetActive(false);
            }

            CanvasGroup frontGroup = GetOrAddCanvasGroup(newStack[0]);
            frontGroup.interactable = true;
            frontGroup.blocksRaycasts = true;

            stack.Clear();
            stack.AddRange(newStack);
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
