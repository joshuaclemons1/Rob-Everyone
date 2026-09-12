using RobEveryone.Core;
using UnityEngine;

namespace RobEveryone.World
{
    // Plain, unnetworked -- every client independently reads the
    // already-synced GameFlowManager.CurrentTimeOfDay/IsNightRound off
    // the same persistent GameFlowManager instance (same "every client
    // reacts identically off synced state" pattern HomeownerAI.
    // OnStateChanged already establishes for its color tint). Start(),
    // not a scene-load event or SyncVar hook -- one instance of this
    // lives in *both* SampleScene and Lobby, each with its own skybox
    // material/Light references, and each naturally re-runs fresh every
    // time its own scene loads. Placing it in Lobby too means the
    // upcoming round's time-of-day already shows there during the
    // pre-round shop/ready-up phase -- roundInBatch (and so
    // CurrentTimeOfDay) is updated at the *end* of the previous round,
    // before the scene change into Lobby even happens, so by the time a
    // player is looking at Lobby's own skybox it's already correctly
    // previewing what round they're about to play, including Night.
    public class NightModeVisuals : MonoBehaviour
    {
        [SerializeField] private Material morningSkybox;
        [SerializeField] private Material daySkybox;
        [SerializeField] private Material nightSkybox;
        [SerializeField] private Light sun;
        [SerializeField] private float dayIntensity = 1f;
        [SerializeField] private float nightIntensity = 0.15f;

        private void Start()
        {
            GameFlowManager.TimeOfDay timeOfDay = GameFlowManager.Instance != null
                ? GameFlowManager.Instance.CurrentTimeOfDay
                : GameFlowManager.TimeOfDay.Morning;

            RenderSettings.skybox = timeOfDay switch
            {
                GameFlowManager.TimeOfDay.Day => daySkybox,
                GameFlowManager.TimeOfDay.Night => nightSkybox,
                _ => morningSkybox,
            };

            if (sun != null) sun.intensity = timeOfDay == GameFlowManager.TimeOfDay.Night ? nightIntensity : dayIntensity;

            // Skybox-driven ambient lighting doesn't recompute itself
            // just from swapping RenderSettings.skybox -- without this,
            // ambient light stays baked from whichever skybox was active
            // when the scene was authored, regardless of which one this
            // just picked.
            DynamicGI.UpdateEnvironment();
        }
    }
}
