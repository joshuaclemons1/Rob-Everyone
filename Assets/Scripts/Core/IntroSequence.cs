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
