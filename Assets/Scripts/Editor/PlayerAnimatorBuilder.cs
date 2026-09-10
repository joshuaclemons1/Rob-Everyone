using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RobEveryone.EditorTools
{
    // Rebuilds Assets/Art/Characters/Animators/PlayerAnimator.controller
    // from scratch in one click, instead of hand-wiring ~15 states and
    // transitions in the Animator window (and re-wiring them every time
    // the movement model shifts). Same spirit as RagdollBatchTool: a
    // deterministic, re-runnable Editor tool for a piece of setup that's
    // tedious and error-prone by hand.
    //
    // What it builds:
    //   Base Layer (no mask)
    //     - Locomotion  : 1D blend tree on Speed -> Idle / Walk / Run
    //     - CarryIdle   : Walk_Carry frozen on frame 0 (no Idle_Carry clip
    //                     exists, so this is the "standing, arms full" pose)
    //     - CarryMove   : 1D blend tree on Speed -> Walk_Carry / Run_Carry
    //     - Jump        : the existing jump clip, JumpSpeed as speed param
    //   Action Layer (upper-body mask, Override, weight driven from code)
    //     - None        : empty default state (base layer shows through)
    //     - Action      : 1D blend tree on ActionType -> PickUp / Shoot /
    //                     Swing / RecieveHit  (tagged "Action" so
    //                     PlayerAnimationDriver knows to fade the layer in)
    //
    // Parameters: Speed, Grounded, Jump (trigger), JumpSpeed, Carrying,
    // Action (trigger), ActionType (int) -- matches PlayerAnimationDriver
    // and PlayerActionAnim's int values.
    //
    // The controller asset is edited IN PLACE (its GUID never changes), so
    // PlayerSkinSpawner's serialized reference to it survives a rebuild.
    // The upper-body AvatarMask is (re)generated next to it as
    // PlayerActionMask.mask from BaseCharacter.fbx's own bone hierarchy.
    public static class PlayerAnimatorBuilder
    {
        private const string ControllerPath =
            "Assets/Art/Characters/Animators/PlayerAnimator.controller";
        private const string MaskPath =
            "Assets/Art/Characters/Animators/PlayerActionMask.mask";
        private const string ClipSourceFbx =
            "Assets/Art/Characters/Quaternius-UltimateAnimatedCharacterPack/FBX/BaseCharacter.fbx";

        // Speed (m/s) at which each locomotion clip is at full weight --
        // walkSpeed 5 / sprintSpeed 8 on FirstPersonController.
        private const float WalkSpeed = 5f;
        private const float RunSpeed = 8f;

        // Bones the Action layer is allowed to drive (everything from the
        // spine up, plus both arms). Anything not listed -- hips, legs,
        // feet, the armature root, the mesh -- stays owned by the base
        // layer so you keep running/walking while your arms do a one-shot.
        private static readonly HashSet<string> UpperBodyBones = new()
        {
            "Torso", "Neck", "Head", "Head_end",
            "Shoulder.L", "Shoulder.R",
            "UpperArm.L", "UpperArm.R",
            "LowerArm.L", "LowerArm.R",
            "Fist.L", "Fist.R", "Fist.L_end", "Fist.R_end",
        };

        [MenuItem("Assets/Rob Everyone/Rebuild Player Animator")]
        private static void Rebuild()
        {
            Dictionary<string, AnimationClip> clips = LoadClips();
            string[] required =
            {
                "Idle", "Walk", "Run", "Jump",
                "Walk_Carry", "Run_Carry",
                "PickUp", "Shoot_OneHanded", "SwordSlash", "RecieveHit",
            };
            string missing = string.Join(", ", required.Where(n => !clips.ContainsKey(n)));
            if (!string.IsNullOrEmpty(missing))
            {
                EditorUtility.DisplayDialog("Rebuild Player Animator",
                    $"BaseCharacter.fbx is missing clips: {missing}\n\n" +
                    "Nothing was changed.", "OK");
                return;
            }

            AvatarMask mask = BuildUpperBodyMask();
            AnimatorController controller = ResetController();

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("JumpSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Carrying", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Action", AnimatorControllerParameterType.Trigger);
            // Float, not Int -- blend trees warn if their blend parameter
            // isn't a float. PlayerAnimationDriver still only ever sets it
            // to whole PlayerActionAnim values (0..3).
            controller.AddParameter("ActionType", AnimatorControllerParameterType.Float);

            BuildBaseLayer(controller, clips);
            BuildActionLayer(controller, clips, mask);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[PlayerAnimatorBuilder] Rebuilt " + ControllerPath +
                      " and " + MaskPath + ".");
        }

        // ---- base (full-body) layer -----------------------------------

        private static void BuildBaseLayer(AnimatorController controller,
                                           Dictionary<string, AnimationClip> clips)
        {
            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // Locomotion blend tree -- added first so it's the default state.
            controller.CreateBlendTreeInController("Locomotion", out BlendTree locoTree, 0);
            Configure1D(locoTree, "Speed",
                (clips["Idle"], 0f),
                (clips["Walk"], WalkSpeed),
                (clips["Run"], RunSpeed));
            AnimatorState locomotion = FindState(sm, "Locomotion");

            AnimatorState carryIdle = sm.AddState("CarryIdle");
            carryIdle.motion = clips["Walk_Carry"];
            carryIdle.speed = 0f; // hold frame 0 -- the "arms full, standing" pose

            controller.CreateBlendTreeInController("CarryMove", out BlendTree carryTree, 0);
            Configure1D(carryTree, "Speed",
                (clips["Walk_Carry"], 0f),
                (clips["Walk_Carry"], WalkSpeed),
                (clips["Run_Carry"], RunSpeed));
            AnimatorState carryMove = FindState(sm, "CarryMove");

            AnimatorState jump = sm.AddState("Jump");
            jump.motion = clips["Jump"];
            jump.speedParameterActive = true;
            jump.speedParameter = "JumpSpeed";

            // carry toggle
            AddBool(locomotion.AddTransition(carryIdle), "Carrying", true, 0.25f);
            AddBool(carryIdle.AddTransition(locomotion), "Carrying", false, 0.25f);
            AddBool(carryMove.AddTransition(locomotion), "Carrying", false, 0.25f);

            // carry idle <-> carry move on speed
            AddFloat(carryIdle.AddTransition(carryMove), "Speed", true, 0.5f, 0.15f);
            AddFloat(carryMove.AddTransition(carryIdle), "Speed", false, 0.5f, 0.15f);

            // jump: only when NOT carrying (both hands are on the body)
            AnimatorStateTransition toJump = sm.AddAnyStateTransition(jump);
            toJump.hasExitTime = false;
            toJump.hasFixedDuration = true;
            toJump.duration = 0.1f;
            toJump.canTransitionToSelf = false;
            toJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
            toJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "Carrying");

            AnimatorStateTransition jumpToLoco = jump.AddTransition(locomotion);
            jumpToLoco.hasExitTime = false;
            jumpToLoco.hasFixedDuration = true;
            jumpToLoco.duration = 0.15f;
            jumpToLoco.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            jumpToLoco.AddCondition(AnimatorConditionMode.IfNot, 0f, "Carrying");

            AnimatorStateTransition jumpToCarry = jump.AddTransition(carryIdle);
            jumpToCarry.hasExitTime = false;
            jumpToCarry.hasFixedDuration = true;
            jumpToCarry.duration = 0.15f;
            jumpToCarry.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            jumpToCarry.AddCondition(AnimatorConditionMode.If, 0f, "Carrying");
        }

        // ---- action (upper-body) layer -------------------------------

        private static void BuildActionLayer(AnimatorController controller,
                                             Dictionary<string, AnimationClip> clips,
                                             AvatarMask mask)
        {
            // Build the layer explicitly (its own state-machine sub-asset)
            // rather than AddLayer(name) + get/modify/set the layers array
            // -- the latter loses the state machine often enough to be not
            // worth it.
            var sm = new AnimatorStateMachine { name = "Action", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(sm, controller);
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "Action",
                defaultWeight = 0f, // PlayerAnimationDriver fades this 0<->1
                blendingMode = AnimatorLayerBlendingMode.Override,
                avatarMask = mask,
                stateMachine = sm,
            });

            // Empty default -- base layer shows through the mask while
            // idle. WriteDefaults OFF so it writes nothing at all (an
            // empty WD-on state would fight the base layer by stamping
            // the bind pose onto the masked bones every frame).
            AnimatorState none = sm.AddState("None");
            none.writeDefaultValues = false;

            controller.CreateBlendTreeInController("Action", out BlendTree tree, 1);
            Configure1D(tree, "ActionType",
                (clips["PickUp"], 0f),
                (clips["Shoot_OneHanded"], 1f),
                (clips["SwordSlash"], 2f),
                (clips["RecieveHit"], 3f));
            AnimatorState actionState = FindState(sm, "Action");
            actionState.tag = "Action"; // PlayerAnimationDriver.UpdateActionLayerWeight keys off this

            AnimatorStateTransition toAction = sm.AddAnyStateTransition(actionState);
            toAction.hasExitTime = false;
            toAction.hasFixedDuration = true;
            toAction.duration = 0.05f;
            toAction.canTransitionToSelf = true; // re-trigger restarts the clip
            toAction.AddCondition(AnimatorConditionMode.If, 0f, "Action");

            AnimatorStateTransition backToNone = actionState.AddTransition(none);
            backToNone.hasExitTime = true;
            backToNone.exitTime = 0.8f;
            backToNone.hasFixedDuration = true;
            backToNone.duration = 0.15f;
        }

        // ---- helpers -------------------------------------------------

        private static void Configure1D(BlendTree tree, string parameter,
                                        params (AnimationClip clip, float threshold)[] children)
        {
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = parameter;
            tree.useAutomaticThresholds = false;
            foreach ((AnimationClip clip, float threshold) in children)
            {
                tree.AddChild(clip, threshold);
            }
        }

        private static void AddBool(AnimatorStateTransition t, string param, bool value, float duration)
        {
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = duration;
            t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
        }

        private static void AddFloat(AnimatorStateTransition t, string param, bool greater,
                                     float threshold, float duration)
        {
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = duration;
            t.AddCondition(greater ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less,
                           threshold, param);
        }

        private static AnimatorState FindState(AnimatorStateMachine sm, string name) =>
            sm.states.First(s => s.state.name == name).state;

        private static Dictionary<string, AnimationClip> LoadClips()
        {
            var map = new Dictionary<string, AnimationClip>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ClipSourceFbx))
            {
                if (asset is not AnimationClip clip) continue;
                if (clip.name.StartsWith("__preview__")) continue;

                // Clips import as "CharacterArmature|Walk" etc.
                int bar = clip.name.IndexOf('|');
                string shortName = bar >= 0 ? clip.name[(bar + 1)..] : clip.name;
                map[shortName] = clip;
            }
            return map;
        }

        // Wipe the existing controller (keeping its asset/GUID) and every
        // sub-asset it owns, so a rebuild doesn't pile duplicate state
        // machines / blend trees into the file.
        private static AnimatorController ResetController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                return AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            for (int i = controller.layers.Length - 1; i >= 0; i--)
                controller.RemoveLayer(i);
            controller.parameters = new AnimatorControllerParameter[0];

            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            {
                if (sub != controller && sub != null) Object.DestroyImmediate(sub, true);
            }

            controller.AddLayer("Base Layer");
            return controller;
        }

        private static AvatarMask BuildUpperBodyMask()
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ClipSourceFbx);
            if (fbx == null)
            {
                Debug.LogError("[PlayerAnimatorBuilder] Can't load " + ClipSourceFbx);
                return null;
            }

            GameObject instance = Object.Instantiate(fbx);
            try
            {
                var paths = new List<(string path, bool active)>();
                CollectTransforms(instance.transform, instance.transform, paths);

                var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
                if (mask == null)
                {
                    mask = new AvatarMask();
                    AssetDatabase.CreateAsset(mask, MaskPath);
                }

                mask.transformCount = paths.Count;
                for (int i = 0; i < paths.Count; i++)
                {
                    mask.SetTransformPath(i, paths[i].path);
                    mask.SetTransformActive(i, paths[i].active);
                }
                EditorUtility.SetDirty(mask);
                return mask;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void CollectTransforms(Transform root, Transform t,
                                              List<(string, bool)> outPaths)
        {
            if (t != root)
            {
                string path = AnimationUtility.CalculateTransformPath(t, root);
                outPaths.Add((path, UpperBodyBones.Contains(t.name)));
            }
            foreach (Transform child in t)
            {
                CollectTransforms(root, child, outPaths);
            }
        }
    }
}
