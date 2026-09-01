using UnityEngine;
using UnityEngine.UI;

namespace RobEveryone.UI
{
    // A pixel-art bar drawn as discrete sprite frames (empty to full)
    // rather than a fill-amount/mask shader. Call SetRatio each frame
    // with a 0-1 value; picks whichever frame is closest.
    public class LevelBarUI : MonoBehaviour
    {
        [SerializeField] private Image image;
        [SerializeField] private Sprite[] levelSprites;

        public void SetRatio(float ratio01)
        {
            if (image == null || levelSprites == null || levelSprites.Length == 0) return;

            int level = Mathf.RoundToInt(Mathf.Clamp01(ratio01) * (levelSprites.Length - 1));
            image.sprite = levelSprites[level];
        }
    }
}
