#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace StickManFight.Client.TPS
{
    internal sealed class TpsRuntimeDiagnostics : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int captureIndex = Array.IndexOf(args, "-capture-tps");
            int walkIndex = Array.IndexOf(args, "-diagnose-walk");
            if (captureIndex < 0 && walkIndex < 0)
            {
                return;
            }

            int optionIndex = walkIndex >= 0 ? walkIndex : captureIndex;
            string path = optionIndex + 1 < args.Length ? args[optionIndex + 1] : "/tmp/stickmanfight-tps-runtime.png";
            var diagnostics = new GameObject("TPS Runtime Diagnostics").AddComponent<TpsRuntimeDiagnostics>();
            DontDestroyOnLoad(diagnostics.gameObject);
            diagnostics.StartCoroutine(walkIndex >= 0
                ? diagnostics.WalkDiagnosticRoutine(path)
                : diagnostics.CaptureRoutine(path));
        }

        private IEnumerator WalkDiagnosticRoutine(string path)
        {
            TpsPlayerController player = FindFirstObjectByType<TpsPlayerController>();
            Animator animator = player != null ? player.GetComponentInChildren<Animator>() : null;
            Keyboard keyboard = Keyboard.current;
            if (player == null || animator == null || keyboard == null)
            {
                Debug.LogError("[TPS Walk Diagnostic] Player, Animator, or Keyboard was not available.");
                yield break;
            }

            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;
            }

            Vector3 startPosition = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftAlt));
            InputSystem.Update();

            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
            }

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();

            Vector3 displacement = player.transform.position - startPosition;
            Vector3 movementDirection = Vector3.ProjectOnPlane(displacement, Vector3.up).normalized;
            Vector3 bodyForward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            Vector3 visualForward = Vector3.ProjectOnPlane(animator.transform.forward, Vector3.up).normalized;
            float rootAngle = movementDirection.sqrMagnitude > 0f ? Vector3.Angle(bodyForward, movementDirection) : -1f;
            float visualAngle = movementDirection.sqrMagnitude > 0f ? Vector3.Angle(visualForward, movementDirection) : -1f;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

            Debug.Log(
                $"[TPS Walk Diagnostic] distance={displacement.magnitude:F3}m, " +
                $"movement={movementDirection}, rootForward={bodyForward}, visualForward={visualForward}, " +
                $"rootAngle={rootAngle:F2}deg, visualAngle={visualAngle:F2}deg, " +
                $"walkState={state.IsName("Walk")}, speed={animator.GetFloat("Speed"):F2}, " +
                $"moveX={animator.GetFloat("MoveX"):F2}, moveY={animator.GetFloat("MoveY"):F2}");

            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1.5f);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator CaptureRoutine(string path)
        {
            TpsOrbitCamera orbitCamera = FindFirstObjectByType<TpsOrbitCamera>();
            if (orbitCamera != null)
            {
                orbitCamera.SetDiagnosticAim(true);
            }

            TpsPlayerController player = FindFirstObjectByType<TpsPlayerController>();
            TpsAimRig aimRig = FindFirstObjectByType<TpsAimRig>();
            Animator animator = FindFirstObjectByType<Animator>();
            if (animator != null)
            {
                animator.SetBool("Aiming", true);
            }

            if (player != null && aimRig != null)
            {
                player.enabled = false;
                player.transform.Rotate(0f, -45f, 0f, Space.World);
                aimRig.SetAiming(true);
            }

            for (int frame = 0; frame < 45; frame++)
            {
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.25f);

            if (animator != null && orbitCamera != null)
            {
                Vector3 torsoForward = GetTorsoForward(animator, orbitCamera.PlanarForward);
                float yawError = Vector3.Angle(torsoForward, orbitCamera.PlanarForward);
                Vector3 lowerBodyForward = player != null
                    ? Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized
                    : orbitCamera.PlanarForward;
                float lowerBodyYaw = Vector3.Angle(lowerBodyForward, orbitCamera.PlanarForward);
                Debug.Log(
                    $"[TPS Upper Body Diagnostic] torsoForward={torsoForward}, " +
                    $"cameraForward={orbitCamera.PlanarForward}, yawError={yawError:F2}deg, " +
                    $"lowerBodyForward={lowerBodyForward}, lowerBodyYaw={lowerBodyYaw:F2}deg");
            }

            if (aimRig != null && aimRig.AimOrigin != null && aimRig.Muzzle != null)
            {
                Vector3 desiredAimDirection = aimRig.GetDesiredAimDirection();
                Vector3 weaponAimDirection = aimRig.GetCurrentWeaponAimDirection();
                float muzzleAimError = Vector3.Angle(weaponAimDirection, desiredAimDirection);
                Debug.Log(
                    $"[TPS Muzzle Aim Diagnostic] rawForward={aimRig.RawAimForward}, " +
                    $"rawError={aimRig.RawAimError:F2}deg, weaponForward={weaponAimDirection}, " +
                    $"desiredDirection={desiredAimDirection}, aimError={muzzleAimError:F2}deg");
            }

            TpsWeaponPose weaponPose = FindFirstObjectByType<TpsWeaponPose>();
            if (weaponPose != null && animator != null)
            {
                TpsAnimationRig animationRig = animator.GetComponent<TpsAnimationRig>();
                Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                bool hierarchyLocked = weaponPose.WeaponSocket != null
                    && weaponPose.RightHand != null
                    && weaponPose.Weapon != null
                    && weaponPose.TriggerGrip != null
                    && weaponPose.LeftHandGrip != null
                    && weaponPose.WeaponSocket.parent == weaponPose.RightHand
                    && weaponPose.Weapon.parent == weaponPose.WeaponSocket
                    && weaponPose.TriggerGrip.parent == weaponPose.WeaponSocket
                    && weaponPose.LeftHandGrip.parent == weaponPose.WeaponSocket;
                float leftHandError = leftHand != null && weaponPose.LeftHandGrip != null
                    ? Vector3.Distance(leftHand.position, weaponPose.LeftHandGrip.position)
                    : -1f;
                float triggerError = weaponPose.RightHand != null && weaponPose.TriggerGrip != null
                    ? Vector3.Distance(weaponPose.RightHand.position, weaponPose.TriggerGrip.position)
                    : -1f;
                Debug.Log(
                    $"[TPS Weapon Diagnostic] hierarchyLocked={hierarchyLocked}, " +
                    $"triggerError={triggerError:F4}m, leftHandError={leftHandError:F4}m, " +
                    $"aimWeight={(aimRig != null ? aimRig.CurrentWeight : -1f):F2}, " +
                    $"rigWeight={(animationRig != null ? animationRig.CurrentWeight : -1f):F2}, " +
                    $"torsoConstraints={(animationRig != null ? animationRig.TorsoConstraintCount : -1)}, " +
                    $"runtimeConstraints={(animationRig != null ? animationRig.RuntimeConstraintCount : -1)}, " +
                    $"graphValid={(animationRig != null && animationRig.GraphIsValid)}, " +
                    $"evaluationCount={(aimRig != null ? aimRig.EvaluationCount : -1)}");
                Debug.Log(
                    $"[TPS Rig Targets] leftHand={(leftHand != null ? leftHand.position : Vector3.zero)}, " +
                    $"leftGrip={(weaponPose.LeftHandGrip != null ? weaponPose.LeftHandGrip.position : Vector3.zero)}, " +
                    $"aimTarget={(animationRig != null && animationRig.AimTarget != null ? animationRig.AimTarget.position : Vector3.zero)}");
            }

            TpsShooter shooter = FindFirstObjectByType<TpsShooter>();
            if (shooter != null)
            {
                shooter.FireForDiagnostics();
            }

            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1.5f);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static Vector3 GetTorsoForward(Animator animator, Vector3 fallback)
        {
            Transform leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            Transform rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            if (leftShoulder == null || rightShoulder == null)
            {
                return fallback;
            }

            Vector3 shoulderRight = Vector3.ProjectOnPlane(
                rightShoulder.position - leftShoulder.position, Vector3.up).normalized;
            if (shoulderRight.sqrMagnitude < 0.001f)
            {
                return fallback;
            }

            Vector3 forward = Vector3.Cross(shoulderRight, Vector3.up).normalized;
            return Vector3.Dot(forward, fallback) >= 0f ? forward : -forward;
        }
    }
}
#endif
