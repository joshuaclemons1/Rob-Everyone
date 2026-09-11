using Mirror;
using RobEveryone.Player;
using UnityEngine;
using UnityEngine.UI;

namespace RobEveryone.UI
{
    // A 5-step hollow-ring meter centered on the crosshair, shown only
    // while charging a throw (CarryController's hold-LMB-to-charge). Not
    // a filled/continuous bar -- the source art (Pixel UI pack 3's
    // Strength-Meter_1..5 sub-sprites) is five discrete ring frames, so
    // this swaps between them rather than scaling or masking one image.
    //
    // Resolves *this client's own* CarryController lazily, same
    // NetworkClient.localPlayer pattern CrosshairUI already uses -- the
    // local Player object doesn't exist until Mirror spawns it at
    // runtime.
    public class ThrowChargeMeterUI : MonoBehaviour
    {
        [SerializeField] private Image meterImage;

        // Index 0 = lowest charge, index 4 = fully charged. Assign from
        // Assets/Art/UI/Pixel UI pack 3/03.png's sliced sub-sprites --
        // pick by thumbnail, not name: two of the five kept their
        // default auto-generated names ("03_0", "Strength-Meter_")
        // instead of "Strength-Meter_2"/"Strength-Meter_1" when the sheet
        // was originally sliced.
        [SerializeField] private Sprite[] meterSteps = new Sprite[5];

        private CarryController carry;

        private void Update()
        {
            if (carry == null)
            {
                if (NetworkClient.localPlayer == null) return;
                carry = NetworkClient.localPlayer.GetComponent<CarryController>();
                if (carry == null) return;
            }

            bool charging = carry.IsChargingThrow;
            if (meterImage != null) meterImage.enabled = charging;
            if (!charging || meterImage == null || meterSteps.Length == 0) return;

            int step = Mathf.Clamp(Mathf.FloorToInt(carry.ThrowCharge01 * meterSteps.Length), 0, meterSteps.Length - 1);
            Sprite sprite = meterSteps[step];
            if (sprite != null) meterImage.sprite = sprite;
        }
    }
}
