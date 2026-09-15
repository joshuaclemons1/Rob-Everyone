using RobEveryone.Core;
using UnityEngine;

namespace RobEveryone.World
{
    // Plain, unnetworked -- every client independently reads the
    // already-synced GameFlowManager.CurrentTimeOfDay/IsNightRound off
    // the same persistent GameFlowManager instance (same "every client
    // reacts identically off synced state" pattern HomeownerAI.
    // OnStateChanged already establishes for its color tint). One instance
    // of this lives in *both* SampleScene and Lobby, each with its own
    // skybox material/Light references, and each naturally re-runs fresh
    // every time its own scene loads. Placing it in Lobby too means the
    // upcoming round's time-of-day already shows there during the
    // pre-round shop/ready-up phase -- roundInBatch (and so
    // CurrentTimeOfDay) is updated at the *end* of the previous round,
    // before the scene change into Lobby even happens, so by the time a
    // player is looking at Lobby's own skybox it's already correctly
    // previewing what round they're about to play, including Night.
    //
    // Issue #12 fix: a plain one-shot Start() read raced GameFlowManager's
    // own sync timing on a joining/scene-loading client (GameFlowManager
    // is a scene-placed NetworkIdentity that starts disabled until Mirror's
    // spawn batch reaches this connection, which lands *after* this
    // client's own local scene load finishes) -- so Start() could latch a
    // stale/default roundInBatch with nothing to ever correct it,
    // reproducing as "my skybox shows Night while the host shows Morning."
    // Now also subscribes to GameFlowManager.OnTimeOfDayChanged (a static
    // event, subscribable even before GameFlowManager.Instance exists) and
    // re-applies whenever it fires -- Mirror invokes a SyncVar hook on the
    // *initial* sync too, not just later changes, so the real value always
    // arrives and self-corrects any earlier guess.
    public class NightModeVisuals : MonoBehaviour
    {
        [SerializeField] private Material morningSkybox;
        [SerializeField] private Material daySkybox;
        [SerializeField] private Material nightSkybox;
        [SerializeField] private Light sun;
        [SerializeField] private float dayIntensity = 1f;
        [SerializeField] private float nightIntensity = 0.15f;

        private void OnEnable()
        {
            GameFlowManager.OnTimeOfDayChanged += Apply;
            Apply(); // best-effort immediately; corrected later if this guessed wrong
        }

        private void OnDisable()
        {
            GameFlowManager.OnTimeOfDayChanged -= Apply;
        }

        private void Apply()
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

            // Skybox-driven ambient lighting doesn't recompute itself just
            // from swapping RenderSettings.skybox -- without this, ambient
            // light stays baked from whichever skybox was active when the
            // scene was authored (or from the last Apply() call), regardless
            // of which one this just picked.
            DynamicGI.UpdateEnvironment();
        }
    }
}
