using System.Collections.Generic;
using System.IO;
using RobEveryone.Player;
using UnityEditor;
using UnityEngine;

namespace RobEveryone.EditorTools
{
    // Batch-applies a ragdoll built on one skin prefab (the "template") to
    // any number of other skins, matching bones by name -- since all 6
    // Quaternius character variants share the same rig, this avoids
    // running the Ragdoll Wizard by hand on every one of them, and (more
    // importantly) avoids manually re-fixing each CharacterJoint's
    // connectedBody reference, which plain Unity component copy-paste
    // would leave pointing at the template's own Rigidbody instances
    // instead of the target's.
    //
    // Usage: build the ragdoll on ONE skin normally first (Ragdoll Wizard
    // + Is Kinematic + RagdollHips, per stage3j-traffic-hazard.md). Then
    // in the Project window, select the *other* skins you want it copied
    // to, and ctrl/cmd-click the already-ragdolled template LAST so it
    // becomes the active selection -- then run
    // Assets > Rob Everyone > Copy Ragdoll To Selected Skins.
    //
    // Raw FBX targets are auto-wrapped into a new .prefab first (FBX
    // "Model" prefabs are read-only -- components can't be added directly
    // to them). The new prefab is created next to the original FBX; you
    // still need to manually re-point PlayerSkinRoster at it afterward,
    // this tool doesn't touch that asset.
    public static class RagdollBatchTool
    {
        [MenuItem("Assets/Rob Everyone/Copy Ragdoll To Selected Skins")]
        private static void CopyRagdollToSelectedSkins()
        {
            GameObject template = Selection.activeGameObject;
            List<GameObject> targets = new(Selection.gameObjects);
            targets.Remove(template);

            if (template == null || targets.Count == 0)
            {
                Debug.LogError("Select the target skin(s) first, then ctrl/cmd-click the already-ragdolled template LAST (so it's the active/highlighted one), then run this again.");
                return;
            }

            string templatePath = AssetDatabase.GetAssetPath(template);
            if (string.IsNullOrEmpty(templatePath))
            {
                Debug.LogError($"{template.name} isn't a Project asset -- select it from the Project window, not the Hierarchy.");
                return;
            }

            GameObject templateRoot = PrefabUtility.LoadPrefabContents(templatePath);
            if (templateRoot.GetComponentInChildren<CharacterJoint>() == null)
            {
                Debug.LogError($"{template.name} doesn't have any CharacterJoints -- did you mean to select it as a target instead of the template? Build its ragdoll with the Ragdoll Wizard first.");
                PrefabUtility.UnloadPrefabContents(templateRoot);
                return;
            }

            foreach (GameObject targetAsset in targets)
            {
                CopyOntoTarget(templateRoot, targetAsset);
            }

            PrefabUtility.UnloadPrefabContents(templateRoot);
            AssetDatabase.SaveAssets();
            Debug.Log("Ragdoll copy pass finished -- check the log above for any per-skin warnings, then test each skin in Play mode before trusting it.");
        }

        private static void CopyOntoTarget(GameObject templateRoot, GameObject targetAsset)
        {
            string targetPath = AssetDatabase.GetAssetPath(targetAsset);

            if (PrefabUtility.GetPrefabAssetType(targetAsset) == PrefabAssetType.Model)
            {
                targetPath = WrapFbxAsPrefab(targetAsset, targetPath);
                if (targetPath == null) return;
            }

            GameObject targetRoot = PrefabUtility.LoadPrefabContents(targetPath);

            if (targetRoot.GetComponentInChildren<CharacterJoint>() != null)
            {
                Debug.LogWarning($"{targetAsset.name} already has ragdoll joints -- skipped, to avoid stacking a second set on top. Remove the existing ones first if you want to redo it.");
                PrefabUtility.UnloadPrefabContents(targetRoot);
                return;
            }

            int bonesCopied = CopyRagdollBones(templateRoot.transform, targetRoot.transform);

            PrefabUtility.SaveAsPrefabAsset(targetRoot, targetPath);
            PrefabUtility.UnloadPrefabContents(targetRoot);

            Debug.Log($"{targetAsset.name}: copied ragdoll onto {bonesCopied} bone(s) -> {targetPath}");
        }

        // FBX assets are read-only "Model" prefabs -- Instantiate a linked
        // copy and save *that* as a brand-new regular .prefab next to the
        // original, which components CAN be added to. The source FBX is
        // never modified.
        private static string WrapFbxAsPrefab(GameObject fbxAsset, string fbxPath)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);

            string folder = Path.GetDirectoryName(fbxPath);
            string newPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fbxAsset.name}_Ragdoll.prefab");

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, newPath);
            Object.DestroyImmediate(instance);

            if (savedPrefab == null)
            {
                Debug.LogError($"Failed to wrap {fbxAsset.name} as a prefab.");
                return null;
            }

            Debug.Log($"{fbxAsset.name} was a raw FBX -- wrapped it as a new prefab: {newPath}. Remember to point PlayerSkinRoster at this new prefab instead of the FBX.");
            return newPath;
        }

        // Two passes: Rigidbody + Collider first (so every bone that will
        // need one has one), then CharacterJoints (so connectedBody can be
        // remapped to the *target's* matching Rigidbody, not left pointing
        // at the template's).
        private static int CopyRagdollBones(Transform templateRoot, Transform targetRoot)
        {
            Dictionary<string, Rigidbody> targetBodiesByName = new();
            int copied = 0;

            foreach (Rigidbody templateBody in templateRoot.GetComponentsInChildren<Rigidbody>(true))
            {
                Transform targetBone = FindByName(targetRoot, templateBody.transform.name);
                if (targetBone == null)
                {
                    Debug.LogWarning($"  No bone named \"{templateBody.transform.name}\" on {targetRoot.name} -- skipped.");
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
                targetJoint.enableProjection = templateJoint.enableProjection;
                targetJoint.projectionDistance = templateJoint.projectionDistance;
                targetJoint.projectionAngle = templateJoint.projectionAngle;

                if (templateJoint.connectedBody != null)
                {
                    string connectedName = templateJoint.connectedBody.transform.name;
                    if (targetBodiesByName.TryGetValue(connectedName, out Rigidbody targetConnected))
                    {
                        targetJoint.connectedBody = targetConnected;
                    }
                    else
                    {
                        Debug.LogWarning($"  Could not remap connected body \"{connectedName}\" for the joint on \"{targetBone.name}\" -- that bone may not have copied correctly above.");
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
