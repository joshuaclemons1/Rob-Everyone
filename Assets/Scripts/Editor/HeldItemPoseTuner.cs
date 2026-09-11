using System.Linq;
using Mirror;
using RobEveryone.Items;
using RobEveryone.Player;
using UnityEditor;
using UnityEngine;

namespace RobEveryone.EditorTools
{
    // Lets every item's held-in-hand pose (HeldItemDisplay) be tuned in
    // one sitting instead of picking each one up individually to see it.
    // Forces the local player's HeldItemDisplay to preview a chosen
    // ItemDefinition regardless of actual inventory state, with
    // Position/Rotation/Scale fields that reposition it live as you
    // drag -- the values write straight to the ItemDefinition asset via
    // SerializedObject, the same as editing it in a normal Inspector, so
    // they're saved for real (ScriptableObject asset edits made during
    // Play Mode persist after stopping, unlike scene-object edits).
    //
    // Requires actually being in Play Mode with a local player spawned
    // (SampleScene or Lobby, hosted from MainMenu) -- this only ever
    // talks to whatever HeldItemDisplay already exists on
    // NetworkClient.localPlayer, it doesn't spawn anything of its own.
    public class HeldItemPoseTuner : EditorWindow
    {
        private ItemDefinition[] items;
        private int index;
        private bool previewing;

        // The default drag-numeric-field UI isn't built for the
        // sub-0.01 precision this actually needs -- these govern the
        // +/- nudge buttons below instead. Typing an exact value into
        // the field itself always still works too.
        private float positionStep = 0.005f;
        private float rotationStep = 1f;

        [MenuItem("Rob Everyone/Held Item Pose Tuner")]
        public static void Open() => GetWindow<HeldItemPoseTuner>("Held Item Pose Tuner");

        private void OnEnable()
        {
            RefreshItemList();
        }

        private void OnFocus()
        {
            RefreshItemList();
        }

        private void RefreshItemList()
        {
            items = AssetDatabase.FindAssets("t:ItemDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>)
                .Where(i => i != null)
                .OrderBy(i => i.ItemName)
                .ToArray();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode (Host from MainMenu) with a local player spawned, then reopen/focus this window.", MessageType.Info);
                previewing = false;
                return;
            }

            if (items == null || items.Length == 0)
            {
                EditorGUILayout.HelpBox("No ItemDefinition assets found under Assets/Data/Items.", MessageType.Warning);
                return;
            }

            HeldItemDisplay display = FindLocalDisplay();
            if (display == null)
            {
                EditorGUILayout.HelpBox("No local player with a HeldItemDisplay found yet -- wait for your player to spawn.", MessageType.Info);
                return;
            }

            index = Mathf.Clamp(index, 0, items.Length - 1);
            ItemDefinition current = items[index];

            EditorGUILayout.LabelField($"{index + 1} / {items.Length}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(current.ItemName, EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("< Prev")) { index = (index - 1 + items.Length) % items.Length; ShowPreview(display, items[index]); }
            if (GUILayout.Button("Next >")) { index = (index + 1) % items.Length; ShowPreview(display, items[index]); }
            EditorGUILayout.EndHorizontal();

            if (!previewing)
            {
                if (GUILayout.Button("Show in hand")) ShowPreview(display, current);
            }
            else
            {
                if (GUILayout.Button("Stop previewing (show real inventory again)"))
                {
                    display.ClearPreview();
                    previewing = false;
                }
            }

            EditorGUILayout.Space();

            var so = new SerializedObject(current);
            so.Update();
            SerializedProperty pos = so.FindProperty("heldPositionOffset");
            SerializedProperty rot = so.FindProperty("heldRotationOffset");
            SerializedProperty scale = so.FindProperty("worldModelScale");

            EditorGUILayout.Space();
            positionStep = EditorGUILayout.FloatField("Position Nudge Step", positionStep);
            rotationStep = EditorGUILayout.FloatField("Rotation Nudge Step", rotationStep);

            bool changed = false;
            changed |= DrawVector3WithNudge("Held Position Offset", pos, positionStep);
            changed |= DrawVector3WithNudge("Held Rotation Offset", rot, rotationStep);

            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(scale, new GUIContent("World Model Scale"));
            changed |= EditorGUI.EndChangeCheck();

            if (GUILayout.Button("Reset Position + Rotation"))
            {
                pos.vector3Value = Vector3.zero;
                rot.vector3Value = Vector3.zero;
                changed = true;
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                if (previewing) display.RefreshPreviewPose();
            }
        }

        // A typed FloatField per axis (so an exact value is always one
        // click-and-type away) plus +/- buttons stepping by `step` --
        // far more reliable than the default drag-numeric-field UI once
        // you're working at sub-0.01 precision, which the default drag
        // sensitivity was never tuned for.
        private static bool DrawVector3WithNudge(string label, SerializedProperty prop, float step)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            Vector3 v = prop.vector3Value;
            bool changed = false;

            v.x = DrawAxisRow("X", v.x, step, ref changed);
            v.y = DrawAxisRow("Y", v.y, step, ref changed);
            v.z = DrawAxisRow("Z", v.z, step, ref changed);

            if (changed) prop.vector3Value = v;
            return changed;
        }

        private static float DrawAxisRow(string axisLabel, float value, float step, ref bool changed)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(axisLabel, GUILayout.Width(14));
            float newValue = EditorGUILayout.FloatField(value, GUILayout.Width(70));
            if (GUILayout.Button("-", GUILayout.Width(24))) newValue -= step;
            if (GUILayout.Button("+", GUILayout.Width(24))) newValue += step;
            EditorGUILayout.EndHorizontal();

            if (!Mathf.Approximately(newValue, value)) changed = true;
            return newValue;
        }

        private void ShowPreview(HeldItemDisplay display, ItemDefinition item)
        {
            display.SetPreviewItem(item);
            previewing = true;
        }

        private static HeldItemDisplay FindLocalDisplay()
        {
            if (NetworkClient.localPlayer == null) return null;
            return NetworkClient.localPlayer.GetComponent<HeldItemDisplay>();
        }
    }
}
