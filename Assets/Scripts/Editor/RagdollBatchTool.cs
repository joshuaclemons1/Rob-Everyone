using System.Collections.Generic;
using System.IO;
using RobEveryone.Player;
using UnityEditor;
using UnityEngine;

namespace RobEveryone.EditorTools
{
    // Batch-applies a ragdoll built on one skin prefab (the "template") to
    // any number of other skins, matching bones by name -- since the
    // Quaternius character variants share the same rig, this avoids
    // running the Ragdoll Wizard by hand on every one of them, and (more
    // importantly) avoids manually re-fixing each CharacterJoint's
    // connectedBody reference, which plain Unity component copy-paste
    // would leave pointing at the template's own Rigidbody instances
    // instead of the target's.
    //
    // The template is an explicit drag-in field, not "whichever object
    // was clicked last in the Project window" -- an earlier version tried
    // that (Selection.activeGameObject), and it turned out too easy to get
    // wrong when the template prefab lives in a different folder than the
    // targets, since switching folders via the tree panel vs. clicking in
    // the content list changes what counts as "active" in ways that are
    // hard to predict reliably.
    //
    // Usage: build the ragdoll on ONE skin normally first (Ragdoll Wizard
    // + Is Kinematic + RagdollHips, per stage3j-traffic-hazard.md). Then
    // open this window (Assets > Rob Everyone > Ragdoll Batch Tool), drag
    // that prefab into the Template field, select the *other* skins you
    // want it copied to in the Project window (the template does not need
    // to be part of that selection at all), and click the button.
    //
    // Raw FBX targets are auto-wrapped into a new .prefab first (FBX
    // "Model" prefabs are read-only -- components can't be added directly
    // to them). The new prefab is created next to the original FBX; you
    // still need to manually re-point PlayerSkinRoster at it afterward,
    // this tool doesn't touch that asset.
    public class RagdollBatchTool : EditorWindow
    {
        private GameObject template;
        private GameObject lastCheckedTemplate;
        private List<string> templateBoneNames = new();
        private bool forceOverwrite;

        [MenuItem("Assets/Rob Everyone/Ragdoll Batch Tool")]
        private static void Open()
        {
            GetWindow<RagdollBatchTool>("Ragdoll Batch Tool");
        }

        private void OnSelectionChange()
        {
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "1. Drag the already-ragdolled template prefab below.\n" +
                "2. Select the target skins in the Project window (the template does not need to be part of that selection).\n" +
                "3. Click Copy Ragdoll To Targets.",
                MessageType.Info);

            EditorGUILayout.Space();
            template = (GameObject)EditorGUILayout.ObjectField("Template (already ragdolled)", template, typeof(GameObject), false);

            if (template != lastCheckedTemplate)
            {
                RefreshTemplateBoneNames();
                lastCheckedTemplate = template;
            }

            if (template != null)
            {
                EditorGUILayout.LabelField($"Template bones with Rigidbody ({templateBoneNames.Count}):", EditorStyles.boldLabel);
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    if (templateBoneNames.Count == 0)
                    {
                        EditorGUILayout.HelpBox("No CharacterJoints found -- this doesn't look like a ragdolled prefab.", MessageType.Error);
                    }
                    else
                    {
                        EditorGUILayout.LabelField(string.Join(", ", templateBoneNames), EditorStyles.wordWrappedLabel);
                        EditorGUILayout.HelpBox("The Ragdoll Wizard's standard output is 11 bodies: Hips (pelvis), Torso (spine), Head, UpperLeg.L/.R, LowerLeg.L/.R, UpperArm.L/.R, LowerArm.L/.R. Feet and elbows are wizard *slots* used to size the shin/forearm, not separate bodies of their own -- 11 here is complete, not a gap. If this list is shorter than 11, something else is missing.", MessageType.Info);
                    }

                    if (GUILayout.Button("Strip Ragdoll From Template (to redo the Wizard cleanly)"))
                    {
                        StripTemplate(template);
                        RefreshTemplateBoneNames();
                    }
                }
            }

            List<GameObject> targets = GetTargetsFromSelection();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Targets from current Project selection ({targets.Count}):", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                if (targets.Count == 0)
                {
                    EditorGUILayout.LabelField("(none -- select some skins in the Project window)");
                }
                else
                {
                    foreach (GameObject target in targets)
                    {
                        EditorGUILayout.LabelField(" • " + target.name);
                    }
                }
            }

            EditorGUILayout.Space();
            forceOverwrite = EditorGUILayout.ToggleLeft(
                "Force (remove and redo any target that already has a ragdoll -- use this to fix targets from a previous, incomplete template)",
                forceOverwrite);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(template == null || targets.Count == 0))
            {
                if (GUILayout.Button("Copy Ragdoll To Targets", GUILayout.Height(30)))
                {
                    CopyRagdollToTargets(template, targets, forceOverwrite);
                }
            }
        }

        private static void StripTemplate(GameObject template)
        {
            string path = AssetDatabase.GetAssetPath(template);
            if (string.IsNullOrEmpty(path)) return;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            RemoveExistingRagdoll(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log($"Stripped all ragdoll components from {template.name} -- it's a plain model again, ready to run GameObject > 3D Object > Ragdoll... on from scratch.");
        }

        private void RefreshTemplateBoneNames()
        {
            templateBoneNames = new List<string>();
            if (template == null) return;

            string path = AssetDatabase.GetAssetPath(template);
            if (string.IsNullOrEmpty(path)) return;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true))
            {
                templateBoneNames.Add(rb.transform.name);
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        // Everything currently selected in the Project window, minus the
        // template itself if it happens to also be selected -- no need to
        // carefully exclude it up front, this just won't double-count it.
        private List<GameObject> GetTargetsFromSelection()
        {
            List<GameObject> targets = new();
            foreach (GameObject obj in Selection.gameObjects)
            {
                if (obj != template) targets.Add(obj);
            }
            return targets;
        }

        private static void CopyRagdollToTargets(GameObject template, List<GameObject> targets, bool forceOverwrite)
        {
            string templatePath = AssetDatabase.GetAssetPath(template);
            if (string.IsNullOrEmpty(templatePath))
            {
                Debug.LogError($"{template.name} isn't a Project asset.");
                return;
            }

            GameObject templateRoot = PrefabUtility.LoadPrefabContents(templatePath);
            if (templateRoot.GetComponentInChildren<CharacterJoint>() == null)
            {
                Debug.LogError($"{template.name} doesn't have any CharacterJoints -- build its ragdoll with the Ragdoll Wizard first, that's what this field expects.");
                PrefabUtility.UnloadPrefabContents(templateRoot);
                return;
            }

            foreach (GameObject targetAsset in targets)
            {
                CopyOntoTarget(templateRoot, targetAsset, forceOverwrite);
            }

            PrefabUtility.UnloadPrefabContents(templateRoot);
            AssetDatabase.SaveAssets();
            Debug.Log($"Ragdoll copy pass finished on {targets.Count} target(s) -- check the log above for any per-skin warnings, then test each skin in Play mode before trusting it.");
        }

        private static void CopyOntoTarget(GameObject templateRoot, GameObject targetAsset, bool forceOverwrite)
        {
            string targetPath = AssetDatabase.GetAssetPath(targetAsset);

            if (PrefabUtility.GetPrefabAssetType(targetAsset) == PrefabAssetType.Model)
            {
                targetPath = WrapFbxAsPrefab(targetAsset, targetPath);
                if (targetPath == null) return;
            }

            // Several skins share a rig with the same root GameObject name
            // ("BaseCharacter") -- log the file name instead of
            // targetAsset.name so multiple lines in the Console are
            // actually distinguishable from each other.
            string targetName = Path.GetFileNameWithoutExtension(targetPath);

            GameObject targetRoot = PrefabUtility.LoadPrefabContents(targetPath);

            if (targetRoot.GetComponentInChildren<CharacterJoint>() != null)
            {
                if (!forceOverwrite)
                {
                    Debug.LogWarning($"{targetName} ({targetPath}) already has ragdoll joints -- skipped. Check the Force option to remove and redo it (e.g. after fixing an incomplete template).");
                    PrefabUtility.UnloadPrefabContents(targetRoot);
                    return;
                }

                RemoveExistingRagdoll(targetRoot.transform);
            }

            int bonesCopied = CopyRagdollBones(templateRoot.transform, targetRoot.transform, targetName);

            PrefabUtility.SaveAsPrefabAsset(targetRoot, targetPath);
            PrefabUtility.UnloadPrefabContents(targetRoot);

            Debug.Log($"{targetName}: copied ragdoll onto {bonesCopied} bone(s) -> {targetPath}");
        }

        // Strips a previously-copied (possibly incomplete) ragdoll off so
        // Force can redo it cleanly. Order matters -- CharacterJoint
        // requires a Rigidbody to exist, so it has to go first, then the
        // Rigidbody, then the Collider.
        private static void RemoveExistingRagdoll(Transform root)
        {
            foreach (CharacterJoint joint in root.GetComponentsInChildren<CharacterJoint>(true))
            {
                Object.DestroyImmediate(joint, true);
            }
            foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true))
            {
                Object.DestroyImmediate(rb, true);
            }
            foreach (CapsuleCollider collider in root.GetComponentsInChildren<CapsuleCollider>(true))
            {
                Object.DestroyImmediate(collider, true);
            }
            foreach (RagdollHips hips in root.GetComponentsInChildren<RagdollHips>(true))
            {
                Object.DestroyImmediate(hips, true);
            }
        }

        // FBX assets are read-only "Model" prefabs -- Instantiate a linked
        // copy and save *that* as a brand-new regular .prefab next to the
        // original, which components CAN be added to. The source FBX is
        // never modified.
        private static string WrapFbxAsPrefab(GameObject fbxAsset, string fbxPath)
        {
            // Object.Instantiate, not PrefabUtility.InstantiatePrefab --
            // the latter keeps a live "nested prefab instance" link back to
            // the FBX, which makes SaveAsPrefabAsset write a stack of
            // overrides instead of a normal standalone GameObject
            // hierarchy. That has no simple, predictable "this is the
            // GameObject" reference another asset (like PlayerSkinRoster)
            // can point at -- Unity reports it as a type mismatch
            // ('PrefabInstance' where a GameObject is expected) at
            // runtime. A plain, disconnected instantiate here produces a
            // fully flat prefab instead, safe to reference normally.
            GameObject instance = Object.Instantiate(fbxAsset);

            // Named from the source .fbx *file*, not fbxAsset.name (the
            // root GameObject's name) -- several Quaternius skins share a
            // rig and all have their root node literally named
            // "BaseCharacter", so naming off the GameObject would produce
            // "BaseCharacter_Ragdoll", "BaseCharacter_Ragdoll 1", etc. --
            // technically non-colliding (GenerateUniqueAssetPath handles
            // that), but useless for telling the resulting prefabs apart
            // afterward. The file name is what's actually distinct.
            string folder = Path.GetDirectoryName(fbxPath);
            string fileName = Path.GetFileNameWithoutExtension(fbxPath);
            string newPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}_Ragdoll.prefab");

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, newPath);
            Object.DestroyImmediate(instance);

            if (savedPrefab == null)
            {
                Debug.LogError($"Failed to wrap {fileName} as a prefab.");
                return null;
            }

            Debug.Log($"{fileName} was a raw FBX -- wrapped it as a new prefab: {newPath}. Remember to point PlayerSkinRoster at this new prefab instead of the FBX.");
            return newPath;
        }

        // Two passes: Rigidbody + Collider first (so every bone that will
        // need one has one), then CharacterJoints (so connectedBody can be
        // remapped to the *target's* matching Rigidbody, not left pointing
        // at the template's).
        private static int CopyRagdollBones(Transform templateRoot, Transform targetRoot, string targetLabel)
        {
            Dictionary<string, Rigidbody> targetBodiesByName = new();
            int copied = 0;

            foreach (Rigidbody templateBody in templateRoot.GetComponentsInChildren<Rigidbody>(true))
            {
                Transform targetBone = FindByName(targetRoot, templateBody.transform.name);
                if (targetBone == null)
                {
                    Debug.LogWarning($"  [{targetLabel}] No bone named \"{templateBody.transform.name}\" -- skipped.");
                    continue;
                }

                Rigidbody targetBody = targetBone.gameObject.AddComponent<Rigidbody>();
                targetBody.mass = templateBody.mass;
                targetBody.linearDamping = templateBody.linearDamping;
                targetBody.angularDamping = templateBody.angularDamping;
                targetBody.isKinematic = true; // always the safe rest-state default
                targetBodiesByName[templateBody.transform.name] = targetBody;

                CapsuleCollider templateCollider = templateBody.GetComponent<CapsuleCollider>();
                if (templateCollider != null)
                {
                    CapsuleCollider targetCollider = targetBone.gameObject.AddComponent<CapsuleCollider>();
                    targetCollider.radius = templateCollider.radius;
                    targetCollider.height = templateCollider.height;
                    targetCollider.direction = templateCollider.direction;
                    targetCollider.center = templateCollider.center;
                }

                if (templateBody.GetComponent<RagdollHips>() != null)
                {
                    targetBone.gameObject.AddComponent<RagdollHips>();
                }

                copied++;
            }

            foreach (CharacterJoint templateJoint in templateRoot.GetComponentsInChildren<CharacterJoint>(true))
            {
                Transform targetBone = FindByName(targetRoot, templateJoint.transform.name);
                if (targetBone == null) continue;

                CharacterJoint targetJoint = targetBone.gameObject.AddComponent<CharacterJoint>();
                targetJoint.anchor = templateJoint.anchor;
                targetJoint.axis = templateJoint.axis;
                targetJoint.swingAxis = templateJoint.swingAxis;
                targetJoint.lowTwistLimit = templateJoint.lowTwistLimit;
                targetJoint.highTwistLimit = templateJoint.highTwistLimit;
                targetJoint.swing1Limit = templateJoint.swing1Limit;
                targetJoint.swing2Limit = templateJoint.swing2Limit;
                // Forced on regardless of what the template has -- Unity's
                // Ragdoll Wizard leaves this off by default, but a
                // CharacterJoint is a *soft* constraint: under a strong
                // sudden impulse (exactly what a car hit needs, to
                // actually send the ragdoll flying) it can visibly stretch
                // well past its configured swing/twist limits before the
                // solver settles it back, which reads as vertices tearing
                // off toward wherever that limb ended up. Projection is
                // the hard correction that prevents that drift instead of
                // just copying whatever the template happened to have.
                targetJoint.enableProjection = true;
                targetJoint.projectionDistance = 0.1f;
                targetJoint.projectionAngle = 10f;

                if (templateJoint.connectedBody != null)
                {
                    string connectedName = templateJoint.connectedBody.transform.name;
                    if (targetBodiesByName.TryGetValue(connectedName, out Rigidbody targetConnected))
                    {
                        targetJoint.connectedBody = targetConnected;
                    }
                    else
                    {
                        Debug.LogWarning($"  [{targetLabel}] Could not remap connected body \"{connectedName}\" for the joint on \"{targetBone.name}\" -- that bone may not have copied correctly above.");
                    }
                }
            }

            return copied;
        }

        private static Transform FindByName(Transform root, string name)
        {
            if (root.name == name) return root;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) return child;
            }

            return null;
        }
    }
}
