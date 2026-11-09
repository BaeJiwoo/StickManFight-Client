#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StickManFight.Client.TPS;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace StickManFight.Client.Editor.TPS
{
    public static class TpsPrototypeSetup
    {
        private const string ModelPath = "Assets/Game/Characters/Bob/Models/Bob_Rigged.fbx";
        private const string AnimationDirectory = "Assets/Game/Characters/Bob/Animations";
        private const string ControllerPath = "Assets/Game/Characters/Bob/Animator/Bob_TPS.controller";
        private const string AvatarMaskPath = "Assets/Game/Characters/Bob/Animator/Bob_UpperBody.mask";
        private const string PlayerPrefabPath = "Assets/Game/Characters/Bob/Prefabs/Bob_TPSPlayer.prefab";
        private const string ScenePath = "Assets/Game/Scenes/TPSPlayground.unity";

        private static readonly IReadOnlyDictionary<string, float> LocomotionRotationOffsets =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                ["Bob_Run_Backward_Left"] = -43.0967f,
                ["Bob_Run_Backward_Right"] = -4.3787f,
                ["Bob_Run_Backward"] = -31.1047f,
                ["Bob_Run_Forward_Left"] = -7.9186f,
                ["Bob_Run_Forward_Right"] = -43.2909f,
                ["Bob_Run_Forward"] = -42.8415f,
                ["Bob_Run_Left"] = -52.0437f,
                ["Bob_Run_Right"] = -58.9389f,
                ["Bob_Sprint_Backward_Left"] = -23.8050f,
                ["Bob_Sprint_Backward_Right"] = 8.0794f,
                ["Bob_Sprint_Backward"] = -5.6958f,
                ["Bob_Sprint_Forward_Left"] = 18.5140f,
                ["Bob_Sprint_Forward_Right"] = -24.5265f,
                ["Bob_Sprint_Forward"] = 0.7428f,
                ["Bob_Sprint_Left"] = -40.8377f,
                ["Bob_Sprint_Right"] = -31.6688f,
                ["Bob_Walk_Backward_Left"] = -55.3541f,
                ["Bob_Walk_Backward_Right"] = -16.9670f,
                ["Bob_Walk_Backward"] = -42.5442f,
                ["Bob_Walk_Crouching_Backward_Left"] = -43.5862f,
                ["Bob_Walk_Crouching_Backward_Right"] = -10.2311f,
                ["Bob_Walk_Crouching_Backward"] = -35.4484f,
                ["Bob_Walk_Crouching_Forward_Left"] = -9.6861f,
                ["Bob_Walk_Crouching_Forward_Right"] = -40.2178f,
                ["Bob_Walk_Crouching_Forward"] = -27.6962f,
                ["Bob_Walk_Crouching_Left"] = -59.2943f,
                ["Bob_Walk_Crouching_Right"] = -58.4934f,
                ["Bob_Walk_Forward_Left"] = -23.5571f,
                ["Bob_Walk_Forward_Right"] = -32.8381f,
                ["Bob_Walk_Forward"] = -29.7293f,
                ["Bob_Walk_Left"] = -45.5616f,
                ["Bob_Walk_Right"] = -49.9071f
            };

        [MenuItem("StickManFight/TPS/Build Client Prototype")]
        public static void Build()
        {
            EnsureFolders();
            Avatar sharedAvatar = ConfigureSharedAvatar();
            ConfigureAnimationImports(sharedAvatar);
            AnimatorController controller = BuildAnimatorController();
            AvatarMask upperBodyMask = BuildUpperBodyMask();
            ConfigureUpperBodyLayer(controller, upperBodyMask);

            Material playerMaterial = CreateMaterial("M_Bob_Client", new Color(0.82f, 0.86f, 0.92f), false);
            Material weaponMaterial = CreateMaterial("M_Weapon_Prototype", new Color(0.08f, 0.10f, 0.12f), false);
            Material floorMaterial = CreateMaterial("M_Arena_Floor", new Color(0.12f, 0.15f, 0.18f), false);
            Material coverMaterial = CreateMaterial("M_Arena_Cover", new Color(0.15f, 0.34f, 0.48f), false);
            Material targetMaterial = CreateMaterial("M_Target_Dummy", new Color(0.82f, 0.22f, 0.12f), false);
            Material impactMaterial = CreateMaterial("M_Client_Impact", new Color(1f, 0.72f, 0.08f), true);

            GameObject playerPrefab = BuildPlayerPrefab(controller, sharedAvatar, playerMaterial, weaponMaterial, impactMaterial);
            BuildPlaygroundScene(playerPrefab, floorMaterial, coverMaterial, targetMaterial);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TPS Setup] Client-only TPS prototype generated at {ScenePath}");
        }

        public static void CapturePreview()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            TpsPlayerController player = UnityEngine.Object.FindFirstObjectByType<TpsPlayerController>();
            if (camera == null || player == null)
            {
                throw new InvalidOperationException("TPS preview requires a camera and player in TPSPlayground.");
            }

            Vector3 focus = player.transform.position + Vector3.up * 1.1f;
            camera.transform.position = focus + new Vector3(4.8f, 2.5f, -6.5f);
            camera.transform.LookAt(focus);

            var renderTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            File.WriteAllBytes("/tmp/stickmanfight-tps-preview.png", texture.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(renderTexture);
            Debug.Log("[TPS Setup] Preview captured at /tmp/stickmanfight-tps-preview.png");
        }

        [MenuItem("StickManFight/TPS/Analyze Locomotion Root Motion")]
        public static void AnalyzeLocomotionRootMotion()
        {
            string[] animationGuids = AssetDatabase.FindAssets("t:Model", new[] { AnimationDirectory });
            foreach (string guid in animationGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!IsDirectionalLocomotion(name))
                {
                    continue;
                }

                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(candidate => !candidate.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase));
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                ModelImporterClipAnimation settings = importer?.clipAnimations.FirstOrDefault();
                if (clip == null || settings == null)
                {
                    Debug.LogWarning($"[TPS Root Motion] {name}: clip or import settings unavailable");
                    continue;
                }

                Vector3 planarSpeed = Vector3.ProjectOnPlane(clip.averageSpeed, Vector3.up);
                float measuredYaw = planarSpeed.sqrMagnitude > 0.0001f
                    ? Mathf.Atan2(planarSpeed.x, planarSpeed.z) * Mathf.Rad2Deg
                    : float.NaN;
                float expectedYaw = GetExpectedLocomotionYaw(name);
                float correction = float.IsNaN(measuredYaw)
                    ? float.NaN
                    : Mathf.DeltaAngle(measuredYaw, expectedYaw);

                Debug.Log(
                    $"[TPS Root Motion] {name}|speed={planarSpeed.magnitude:F4}|" +
                    $"vector=({planarSpeed.x:F4},{planarSpeed.z:F4})|measured={measuredYaw:F2}|" +
                    $"expected={expectedYaw:F2}|offset={settings.rotationOffset:F2}|correction={correction:F2}|" +
                    $"rootCurves={clip.hasRootCurves}|motionCurves={clip.hasMotionCurves}");
            }
        }

        public static void BuildDevelopmentDiagnostic()
        {
            Directory.CreateDirectory("/tmp/StickManFightTPSDiagnostic");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "/tmp/StickManFightTPSDiagnostic/StickManFightClient.x86_64",
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"TPS diagnostic build failed: {report.summary.result}");
            }
            Debug.Log("[TPS Setup] Development diagnostic build completed.");
        }

        private static Avatar ConfigureSharedAvatar()
        {
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Bob model importer not found: {ModelPath}");
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.SaveAndReimport();

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<Avatar>()
                .FirstOrDefault(candidate => candidate.isValid && candidate.isHuman);

            if (avatar == null)
            {
                throw new InvalidOperationException("Bob_Rigged did not produce a valid Humanoid Avatar.");
            }

            return avatar;
        }

        private static void ConfigureAnimationImports(Avatar sharedAvatar)
        {
            string[] animationGuids = AssetDatabase.FindAssets("t:Model", new[] { AnimationDirectory });
            foreach (string guid in animationGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    continue;
                }

                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = sharedAvatar;
                importer.importAnimation = true;

                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                bool loop = IsLoopingMotion(path);
                string animationName = Path.GetFileNameWithoutExtension(path);
                bool hasRotationOffset = LocomotionRotationOffsets.TryGetValue(animationName, out float rotationOffset);
                foreach (ModelImporterClipAnimation clip in clips)
                {
                    clip.loopTime = loop;
                    clip.loopPose = loop;
                    if (hasRotationOffset)
                    {
                        clip.rotationOffset = rotationOffset;
                    }
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        private static bool IsLoopingMotion(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            return name.Contains("Idle", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Walk", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Run", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Sprint", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Jump_Loop", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDirectionalLocomotion(string name)
        {
            return name.StartsWith("Bob_Run_", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Bob_Sprint_", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Bob_Walk_", StringComparison.OrdinalIgnoreCase);
        }

        private static float GetExpectedLocomotionYaw(string name)
        {
            if (name.EndsWith("Forward_Left", StringComparison.OrdinalIgnoreCase)) return -45f;
            if (name.EndsWith("Forward_Right", StringComparison.OrdinalIgnoreCase)) return 45f;
            if (name.EndsWith("Backward_Left", StringComparison.OrdinalIgnoreCase)) return -135f;
            if (name.EndsWith("Backward_Right", StringComparison.OrdinalIgnoreCase)) return 135f;
            if (name.EndsWith("Backward", StringComparison.OrdinalIgnoreCase)) return 180f;
            if (name.EndsWith("Left", StringComparison.OrdinalIgnoreCase)) return -90f;
            if (name.EndsWith("Right", StringComparison.OrdinalIgnoreCase)) return 90f;
            return 0f;
        }

        private static AnimatorController BuildAnimatorController()
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Crouching", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Sprinting", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Aiming", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            stateMachine.name = "Base Locomotion";

            AnimatorState walk = AddBlendState(controller, stateMachine, "Walk", BuildWalkBlend(controller));
            AnimatorState run = AddBlendState(controller, stateMachine, "Run", BuildRunBlend(controller));
            AnimatorState crouch = AddBlendState(controller, stateMachine, "Crouch", BuildCrouchBlend(controller));
            AnimatorState sprint = AddBlendState(controller, stateMachine, "Sprint", BuildSprintBlend(controller));
            AnimatorState jumpUp = stateMachine.AddState("Jump Up");
            jumpUp.motion = LoadClip("Bob_Jump_Up");
            AnimatorState airLoop = stateMachine.AddState("Air Loop");
            airLoop.motion = LoadClip("Bob_Jump_Loop");
            AnimatorState land = stateMachine.AddState("Land");
            land.motion = LoadClip("Bob_Jump_Down");
            stateMachine.defaultState = walk;

            AddTransition(walk, run, false, 0.15f, (AnimatorConditionMode.Greater, 3.25f, "Speed"));
            AddTransition(run, walk, false, 0.15f, (AnimatorConditionMode.Less, 3.25f, "Speed"));
            AddTransition(walk, crouch, false, 0.12f, (AnimatorConditionMode.If, 0f, "Crouching"));
            AddTransition(run, crouch, false, 0.12f, (AnimatorConditionMode.If, 0f, "Crouching"));
            AddTransition(crouch, walk, false, 0.12f, (AnimatorConditionMode.IfNot, 0f, "Crouching"));
            AddTransition(walk, sprint, false, 0.12f, (AnimatorConditionMode.If, 0f, "Sprinting"));
            AddTransition(run, sprint, false, 0.12f, (AnimatorConditionMode.If, 0f, "Sprinting"));
            AddTransition(sprint, run, false, 0.12f, (AnimatorConditionMode.IfNot, 0f, "Sprinting"));

            AnimatorStateTransition jumpTransition = stateMachine.AddAnyStateTransition(jumpUp);
            jumpTransition.hasExitTime = false;
            jumpTransition.duration = 0.08f;
            jumpTransition.canTransitionToSelf = false;
            jumpTransition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            jumpTransition.AddCondition(AnimatorConditionMode.Greater, 0.05f, "VerticalSpeed");

            AddTransition(jumpUp, airLoop, false, 0.12f, (AnimatorConditionMode.Less, 0f, "VerticalSpeed"));
            AddTransition(airLoop, land, false, 0.08f, (AnimatorConditionMode.If, 0f, "Grounded"));
            AddTransition(land, walk, true, 0.12f);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AvatarMask BuildUpperBodyMask()
        {
            AssetDatabase.DeleteAsset(AvatarMaskPath);
            var mask = new AvatarMask { name = "Bob_UpperBody" };
            for (AvatarMaskBodyPart part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
            {
                mask.SetHumanoidBodyPartActive(part, false);
            }

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
            AssetDatabase.CreateAsset(mask, AvatarMaskPath);
            return mask;
        }

        private static void ConfigureUpperBodyLayer(AnimatorController controller, AvatarMask mask)
        {
            var stateMachine = new AnimatorStateMachine { name = "Upper Body Aim" };
            AssetDatabase.AddObjectToAsset(stateMachine, controller);

            AnimatorState empty = stateMachine.AddState("Empty");
            AnimatorState aim = stateMachine.AddState("Aim Standing");
            aim.motion = LoadClip("Bob_Idle_Aiming");
            AnimatorState aimCrouch = stateMachine.AddState("Aim Crouching");
            aimCrouch.motion = LoadClip("Bob_Idle_Crouching_Aiming");
            stateMachine.defaultState = empty;

            AddTransition(empty, aim, false, 0.15f, (AnimatorConditionMode.If, 0f, "Aiming"));
            AddTransition(aim, empty, false, 0.15f, (AnimatorConditionMode.IfNot, 0f, "Aiming"));
            AddTransition(aim, aimCrouch, false, 0.12f, (AnimatorConditionMode.If, 0f, "Crouching"));
            AddTransition(aimCrouch, aim, false, 0.12f, (AnimatorConditionMode.IfNot, 0f, "Crouching"));
            AddTransition(aimCrouch, empty, false, 0.12f, (AnimatorConditionMode.IfNot, 0f, "Aiming"));

            var layer = new AnimatorControllerLayer
            {
                name = "Upper Body Aim",
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 1f,
                iKPass = false,
                stateMachine = stateMachine
            };
            controller.AddLayer(layer);
            EditorUtility.SetDirty(controller);
        }

        private static BlendTree BuildWalkBlend(AnimatorController controller)
        {
            return BuildDirectionalBlend(controller, "Walk Blend", "Bob_Idle", "Bob_Walk_Forward", "Bob_Walk_Backward",
                "Bob_Walk_Left", "Bob_Walk_Right", "Bob_Walk_Forward_Left", "Bob_Walk_Forward_Right",
                "Bob_Walk_Backward_Left", "Bob_Walk_Backward_Right");
        }

        private static BlendTree BuildRunBlend(AnimatorController controller)
        {
            return BuildDirectionalBlend(controller, "Run Blend", "Bob_Idle", "Bob_Run_Forward", "Bob_Run_Backward",
                "Bob_Run_Left", "Bob_Run_Right", "Bob_Run_Forward_Left", "Bob_Run_Forward_Right",
                "Bob_Run_Backward_Left", "Bob_Run_Backward_Right");
        }

        private static BlendTree BuildCrouchBlend(AnimatorController controller)
        {
            return BuildDirectionalBlend(controller, "Crouch Blend", "Bob_Idle_Crouching", "Bob_Walk_Crouching_Forward",
                "Bob_Walk_Crouching_Backward", "Bob_Walk_Crouching_Left", "Bob_Walk_Crouching_Right",
                "Bob_Walk_Crouching_Forward_Left", "Bob_Walk_Crouching_Forward_Right",
                "Bob_Walk_Crouching_Backward_Left", "Bob_Walk_Crouching_Backward_Right");
        }

        private static BlendTree BuildSprintBlend(AnimatorController controller)
        {
            return BuildDirectionalBlend(controller, "Sprint Blend", "Bob_Idle", "Bob_Sprint_Forward", "Bob_Sprint_Backward",
                "Bob_Sprint_Left", "Bob_Sprint_Right", "Bob_Sprint_Forward_Left", "Bob_Sprint_Forward_Right",
                "Bob_Sprint_Backward_Left", "Bob_Sprint_Backward_Right");
        }

        private static BlendTree BuildDirectionalBlend(AnimatorController controller, string name, string idle,
            string forward, string backward, string left, string right, string forwardLeft, string forwardRight,
            string backwardLeft, string backwardRight)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveY",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            AddBlendChild(tree, idle, Vector2.zero);
            AddBlendChild(tree, forward, new Vector2(0f, 1f));
            AddBlendChild(tree, backward, new Vector2(0f, -1f));
            AddBlendChild(tree, left, new Vector2(-1f, 0f));
            AddBlendChild(tree, right, new Vector2(1f, 0f));
            AddBlendChild(tree, forwardLeft, new Vector2(-0.7f, 0.7f));
            AddBlendChild(tree, forwardRight, new Vector2(0.7f, 0.7f));
            AddBlendChild(tree, backwardLeft, new Vector2(-0.7f, -0.7f));
            AddBlendChild(tree, backwardRight, new Vector2(0.7f, -0.7f));
            return tree;
        }

        private static void AddBlendChild(BlendTree tree, string clipName, Vector2 position)
        {
            AnimationClip clip = LoadClip(clipName);
            if (clip != null)
            {
                tree.AddChild(clip, position);
            }
        }

        private static AnimatorState AddBlendState(AnimatorController controller, AnimatorStateMachine stateMachine, string name, BlendTree tree)
        {
            AnimatorState state = stateMachine.AddState(name);
            state.motion = tree;
            return state;
        }

        private static void AddTransition(AnimatorState from, AnimatorState to, bool hasExitTime, float duration,
            params (AnimatorConditionMode mode, float threshold, string parameter)[] conditions)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = hasExitTime;
            transition.exitTime = hasExitTime ? 0.8f : 0f;
            transition.duration = duration;
            foreach ((AnimatorConditionMode mode, float threshold, string parameter) in conditions)
            {
                transition.AddCondition(mode, threshold, parameter);
            }
        }

        private static AnimationClip LoadClip(string clipName)
        {
            string path = $"{AnimationDirectory}/{clipName}.fbx";
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase));
        }

        private static GameObject BuildPlayerPrefab(AnimatorController controller, Avatar avatar, Material playerMaterial,
            Material weaponMaterial, Material impactMaterial)
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null)
            {
                throw new InvalidOperationException($"Bob model not found: {ModelPath}");
            }

            GameObject root = new GameObject("Bob_TPSPlayer") { layer = 2 };
            CharacterController characterController = root.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.34f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.stepOffset = 0.3f;

            GameObject visual = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
            visual.name = "Bob_Visual";
            visual.transform.SetParent(root.transform, false);
            SetLayerRecursively(visual, 2);

            Animator animator = visual.GetComponent<Animator>();
            if (animator == null)
            {
                animator = visual.AddComponent<Animator>();
            }
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = playerMaterial;
            }

            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            GameObject weaponSocketObject = new GameObject("WeaponSocket") { layer = 2 };
            weaponSocketObject.transform.SetParent(rightHand != null ? rightHand : visual.transform, false);
            weaponSocketObject.transform.localPosition = Vector3.zero;
            weaponSocketObject.transform.rotation = Quaternion.LookRotation(root.transform.forward, Vector3.up);

            GameObject triggerGripObject = new GameObject("TriggerGrip") { layer = 2 };
            triggerGripObject.transform.SetParent(weaponSocketObject.transform, false);

            GameObject weapon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            weapon.name = "Rifle_Prototype";
            UnityEngine.Object.DestroyImmediate(weapon.GetComponent<Collider>());
            weapon.layer = 2;
            weapon.GetComponent<Renderer>().sharedMaterial = weaponMaterial;
            weapon.transform.SetParent(weaponSocketObject.transform, false);
            weapon.transform.localPosition = Vector3.forward * 0.29f;
            weapon.transform.localRotation = Quaternion.identity;
            weapon.transform.localScale = new Vector3(0.09f, 0.11f, 0.58f);

            GameObject leftHandGripObject = new GameObject("LeftHandGrip") { layer = 2 };
            leftHandGripObject.transform.SetParent(weaponSocketObject.transform, false);
            leftHandGripObject.transform.localPosition = new Vector3(0f, -0.07f, 0.22f);

            GameObject muzzleObject = new GameObject("Muzzle");
            muzzleObject.layer = 2;
            muzzleObject.transform.SetParent(weaponSocketObject.transform, false);
            muzzleObject.transform.localPosition = Vector3.forward * 0.62f;
            ParticleSystem muzzleFlash = muzzleObject.AddComponent<ParticleSystem>();
            ConfigureMuzzleParticles(muzzleFlash, impactMaterial);

            LineRenderer tracer = weapon.AddComponent<LineRenderer>();
            tracer.sharedMaterial = impactMaterial;
            tracer.startWidth = 0.025f;
            tracer.endWidth = 0.005f;
            tracer.startColor = new Color(1f, 0.85f, 0.2f, 1f);
            tracer.endColor = new Color(1f, 0.25f, 0.05f, 0f);
            tracer.useWorldSpace = true;
            tracer.enabled = false;

            GameObject cameraObject = new GameObject("TPSCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(root.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 65f;
            camera.nearClipPlane = 0.08f;
            cameraObject.AddComponent<AudioListener>();
            TpsOrbitCamera orbitCamera = cameraObject.AddComponent<TpsOrbitCamera>();
            SetReference(orbitCamera, "target", root.transform);

            TpsAimRig aimRig = root.AddComponent<TpsAimRig>();
            SetReference(aimRig, "orbitCamera", orbitCamera);
            SetReference(aimRig, "aimOrigin", triggerGripObject.transform);
            SetReference(aimRig, "muzzle", muzzleObject.transform);

            TpsPlayerController playerController = root.AddComponent<TpsPlayerController>();
            SetReference(playerController, "animator", animator);
            SetReference(playerController, "orbitCamera", orbitCamera);
            SetReference(playerController, "aimRig", aimRig);

            TpsShooter shooter = root.AddComponent<TpsShooter>();
            SetReference(shooter, "orbitCamera", orbitCamera);
            SetReference(shooter, "aimRig", aimRig);
            SetReference(shooter, "muzzle", muzzleObject.transform);
            SetReference(shooter, "muzzleFlash", muzzleFlash);
            SetReference(shooter, "tracer", tracer);
            SetReference(shooter, "impactMaterial", impactMaterial);

            TpsWeaponPose weaponPose = root.AddComponent<TpsWeaponPose>();
            SetReference(weaponPose, "animator", animator);
            SetReference(weaponPose, "weapon", weapon.transform);
            SetReference(weaponPose, "muzzle", muzzleObject.transform);
            SetReference(weaponPose, "weaponSocket", weaponSocketObject.transform);
            SetReference(weaponPose, "triggerGrip", triggerGripObject.transform);
            SetReference(weaponPose, "leftHandGrip", leftHandGripObject.transform);
            SetReference(weaponPose, "aimRig", aimRig);

            TpsAnimationRig animationRig = visual.AddComponent<TpsAnimationRig>();
            animationRig.Configure(animator, aimRig, leftHandGripObject.transform, weaponSocketObject.transform, 1f);
            SetReference(weaponPose, "animationRig", animationRig);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildPlaygroundScene(GameObject playerPrefab, Material floorMaterial, Material coverMaterial,
            Material targetMaterial)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "TPSPlayground";

            BuildLighting();
            CreateCube("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(32f, 1f, 32f), floorMaterial);
            CreateCube("Wall_North", new Vector3(0f, 2f, 16f), new Vector3(33f, 5f, 1f), coverMaterial);
            CreateCube("Wall_South", new Vector3(0f, 2f, -16f), new Vector3(33f, 5f, 1f), coverMaterial);
            CreateCube("Wall_East", new Vector3(16f, 2f, 0f), new Vector3(1f, 5f, 33f), coverMaterial);
            CreateCube("Wall_West", new Vector3(-16f, 2f, 0f), new Vector3(1f, 5f, 33f), coverMaterial);
            CreateCube("Cover_Center", new Vector3(0f, 1f, 2f), new Vector3(4f, 2f, 2f), coverMaterial);
            CreateCube("Cover_Left", new Vector3(-6f, 1f, 6f), new Vector3(2f, 2f, 5f), coverMaterial);
            CreateCube("Cover_Right", new Vector3(6f, 1f, 6f), new Vector3(2f, 2f, 5f), coverMaterial);

            GameObject player = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -8f), Quaternion.identity);

            CreateTargetDummy(new Vector3(0f, 1f, 8f), targetMaterial);
            CreateTargetDummy(new Vector3(-7f, 1f, 10f), targetMaterial);
            CreateTargetDummy(new Vector3(7f, 1f, 10f), targetMaterial);
            CreateTargetDummy(new Vector3(-11f, 1f, 2f), targetMaterial);
            CreateTargetDummy(new Vector3(11f, 1f, 2f), targetMaterial);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene("Assets/Game/Scenes/PrototypeArena.unity", true)
            };
        }

        private static void CreateTargetDummy(Vector3 position, Material material)
        {
            GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.name = "TargetDummy";
            dummy.transform.position = position;
            dummy.transform.localScale = new Vector3(0.7f, 1f, 0.7f);
            Renderer renderer = dummy.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            TpsTargetDummy target = dummy.AddComponent<TpsTargetDummy>();
            SetReference(target, "targetRenderer", renderer);
        }

        private static void BuildLighting()
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.40f, 0.53f);
            RenderSettings.ambientEquatorColor = new Color(0.14f, 0.16f, 0.20f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.07f, 0.08f);
        }

        private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private static Material CreateMaterial(string name, Color color, bool unlit)
        {
            string path = $"Assets/Game/Art/Materials/Prototype/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureMuzzleParticles(ParticleSystem particles, Material material)
        {
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.1f;
            main.startLifetime = 0.06f;
            main.startSpeed = 2.5f;
            main.startSize = 0.055f;
            main.startColor = new Color(1f, 0.78f, 0.18f, 1f);
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10f;
            shape.radius = 0.01f;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
        }

        private static void SetReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on {target.name}.");
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory("Assets/Game/Characters/Bob/Animator");
            Directory.CreateDirectory("Assets/Game/Characters/Bob/Prefabs");
            Directory.CreateDirectory("Assets/Game/Art/Materials/Prototype");
            Directory.CreateDirectory("Assets/Game/Scenes");
        }
    }
}
#endif
