using UnityEngine;

namespace StickManFight.Client.TPS
{
    [DefaultExecutionOrder(200)]
    public sealed class TpsWeaponPose : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform weapon;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Transform weaponSocket;
        [SerializeField] private Transform triggerGrip;
        [SerializeField] private Transform leftHandGrip;
        [SerializeField] private TpsAimRig aimRig;
        [SerializeField] private TpsAnimationRig animationRig;
        [SerializeField] private float weaponVisualForwardOffset = 0.29f;
        [SerializeField] private float muzzleForwardOffset = 0.62f;
        [SerializeField] private Vector3 leftHandGripLocalPosition = new(0f, -0.07f, 0.22f);
        [SerializeField] private Vector3 leftHandGripLocalEulerAngles;
        [SerializeField, Range(0f, 1f)] private float leftHandIkWeight = 1f;

        private Transform rightHand;
        internal Transform RightHand => rightHand;
        internal Transform Weapon => weapon;
        internal Transform WeaponSocket => weaponSocket;
        internal Transform TriggerGrip => triggerGrip;
        internal Transform LeftHandGrip => leftHandGrip;

        private void Awake()
        {
            EnsureWeaponRig();
        }

        private void LateUpdate()
        {
            if (!IsWeaponLocked())
            {
                EnsureWeaponRig();
            }

        }

        private void EnsureWeaponRig()
        {
            CacheRightHand();
            if (rightHand == null || weapon == null)
            {
                return;
            }

            if (weaponSocket == null)
            {
                weaponSocket = rightHand.Find("WeaponSocket");
            }

            bool createdSocket = weaponSocket == null;
            if (weaponSocket == null)
            {
                var socketObject = new GameObject("WeaponSocket") { layer = weapon.gameObject.layer };
                weaponSocket = socketObject.transform;
            }

            weaponSocket.SetParent(rightHand, false);
            weaponSocket.localPosition = Vector3.zero;
            weaponSocket.localScale = Vector3.one;
            if (createdSocket)
            {
                Vector3 bindForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                weaponSocket.rotation = Quaternion.LookRotation(bindForward, Vector3.up);
            }

            if (weapon.parent != weaponSocket)
            {
                weapon.SetParent(weaponSocket, false);
            }

            weapon.localPosition = Vector3.forward * weaponVisualForwardOffset;
            weapon.localRotation = Quaternion.identity;

            if (muzzle == null)
            {
                muzzle = weapon.Find("Muzzle");
            }

            if (muzzle != null)
            {
                muzzle.SetParent(weaponSocket, false);
                muzzle.localPosition = Vector3.forward * muzzleForwardOffset;
                muzzle.localRotation = Quaternion.identity;
                muzzle.localScale = Vector3.one;
            }

            if (triggerGrip == null)
            {
                triggerGrip = weaponSocket.Find("TriggerGrip");
            }

            if (triggerGrip == null)
            {
                var triggerObject = new GameObject("TriggerGrip") { layer = weapon.gameObject.layer };
                triggerGrip = triggerObject.transform;
            }

            triggerGrip.SetParent(weaponSocket, false);
            triggerGrip.localPosition = Vector3.zero;
            triggerGrip.localRotation = Quaternion.identity;
            triggerGrip.localScale = Vector3.one;

            if (leftHandGrip == null)
            {
                leftHandGrip = weaponSocket.Find("LeftHandGrip");
            }

            if (leftHandGrip == null)
            {
                var gripObject = new GameObject("LeftHandGrip") { layer = weapon.gameObject.layer };
                leftHandGrip = gripObject.transform;
            }

            leftHandGrip.SetParent(weaponSocket, false);
            leftHandGrip.localPosition = leftHandGripLocalPosition;
            leftHandGrip.localRotation = Quaternion.Euler(leftHandGripLocalEulerAngles);
            leftHandGrip.localScale = Vector3.one;

            if (aimRig == null)
            {
                aimRig = GetComponent<TpsAimRig>();
            }

            if (animationRig == null && animator != null)
            {
                animationRig = animator.GetComponent<TpsAnimationRig>();
                if (animationRig == null)
                {
                    animationRig = animator.gameObject.AddComponent<TpsAnimationRig>();
                }
            }

            if (animationRig != null)
            {
                animationRig.Configure(animator, aimRig, leftHandGrip, weaponSocket, leftHandIkWeight);
            }

            if (aimRig != null && muzzle != null)
            {
                aimRig.ConfigureAimAxis(triggerGrip, muzzle);
            }
        }

        private void CacheRightHand()
        {
            if (rightHand == null && animator != null && animator.isHuman)
            {
                rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
        }

        private bool IsWeaponLocked()
        {
            return rightHand != null
                && weapon != null
                && muzzle != null
                && weaponSocket != null
                && triggerGrip != null
                && leftHandGrip != null
                && animationRig != null
                && animationRig.IsConfigured
                && weaponSocket.parent == rightHand
                && weapon.parent == weaponSocket
                && muzzle.parent == weaponSocket
                && triggerGrip.parent == weaponSocket
                && leftHandGrip.parent == weaponSocket;
        }
    }
}
