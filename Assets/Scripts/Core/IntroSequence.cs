using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace RobEveryone.Core
{
    // Plays the logo/intro clip once, then loads MainMenu -- either when
    // the clip finishes on its own or the player skips it early (any key/
    // click/gamepad button). Polls the new Input System's devices
    // directly rather than going through InputManager/the Gameplay action
    // map -- this scene runs before a player exists and before any
    // gameplay context makes sense, so a low-level "was anything pressed
    // this frame" check is simpler and has nothing to conflict with.
    [RequireComponent(typeof(VideoPlayer))]
    public class IntroSequence : MonoBehaviour
    {
        [SerializeField] private string nextSceneName = "MainMenu";

        private VideoPlayer player;
        private bool advancing;

        private void Awake()
        {
            player = GetComponent<VideoPlayer>();

            // Issue #46 fix: on any aspect ratio wider than the clip's
            // own (most commonly an ultrawide monitor), whatever scaling
            // mode and camera clear settings happen to be saved on the
            // scene decide what shows in the gap the video doesn't cover
            // -- with the target camera's default Skybox clear, that gap
            // rendered as visible empty scene on the sides (or top/
            // bottom, on a narrower-than-16:9 display), making the video
            // read like a floating frame inside a real 3D scene instead
            // of the game's own loading sequence. Force both here rather
            // than trust the Editor-authored values to stay correct
            // forever: FitHorizontally always pins the video to the full
            // width of the screen (matching height, letterboxed, on
            // anything wider than the clip; cropped top/bottom on
            // anything narrower), and a solid black clear means whatever
            // letterbox gap remains is always pure black, never the
            // scene behind it.
            player.aspectRatio = VideoAspectRatio.FitHorizontally;

            Camera targetCamera = player.targetCamera;
            if (targetCamera != null)
            {
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = Color.black;
            }
        }

        private void OnEnable() => player.loopPointReached += HandleFinished;
        private void OnDisable() => player.loopPointReached -= HandleFinished;

        private void Update()
        {
            if (AnySkipInputThisFrame()) Advance();
        }

        private static bool AnySkipInputThisFrame()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) return true;
            return false;
        }

        private void HandleFinished(VideoPlayer _) => Advance();

        private void Advance()
        {
            if (advancing) return;
            advancing = true;
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
