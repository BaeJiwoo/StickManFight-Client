using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace StickManFight.Client.TPS
{
    [DefaultExecutionOrder(300)]
    [RequireComponent(typeof(Animator))]
    public sealed class TpsAnimationRig : MonoBehaviour
    {
        private static readonly HumanBodyBones[] AimBones =
        {
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.UpperChest,
        };

        private static readonly float[] AimWeights = { 0.18f, 0.28f, 0.38f };

        [SerializeField] private Animator animator;
        [SerializeField] private TpsAimRig aimRig;
        [SerializeField] private Transform leftHandGrip;
        [SerializeField] private Transform weaponSocket;
        [SerializeField] private RigBuilder rigBuilder;
        [SerializeField] private Rig upperBodyRig;
        [SerializeField] private Transform aimTarget;
        [SerializeField] private Transform rightHandAimTarget;
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform leftElbowHint;
        [SerializeField] private MultiRotationConstraint rightHandConstraint;
        [SerializeField] private TwoBoneIKConstraint leftHandConstraint;
        [SerializeField] private List<MultiAimConstraint> torsoConstraints = new();
        [SerializeField, Range(0f, 1f)] private float leftHandWeight = 1f;
        private bool weaponSocketCalibrated;

        internal bool IsConfigured => animator != null
            && aimRig != null
            && leftHandGrip != null
            && weaponSocket != null
            && rigBuilder != null
            && upperBodyRig != null
            && aimTarget != null
            && rightHandAimTarget != null
            && leftHandTarget != null
            && rightHandConstraint != null
            && leftHandConstraint != null
            && torsoConstraints.Count > 0;

        internal float CurrentWeight => upperBodyRig != null ? upperBodyRig.weight : 0f;
        internal Transform AimTarget => aimTarget;
        internal int TorsoConstraintCount => torsoConstraints.Count;
        internal bool GraphIsValid => rigBuilder != null && rigBuilder.graph.IsValid();
        internal int RuntimeConstraintCount => rigBuilder != null
            && rigBuilder.layers.Count > 0
            && rigBuilder.layers[0].constraints != null
                ? rigBuilder.layers[0].constraints.Length
                : 0;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (aimRig != null && leftHandGrip != null)
            {
                EnsureRigStructure();
                RebuildRigGraph();
            }
        }

        private void Start()
        {
            if (IsConfigured)
            {
                RebuildRigGraph();
            }
        }

        private void Update()
        {
            if (!IsConfigured)
            {
                return;
            }

            aimTarget.position = aimRig.GetAimPoint();
            rightHandAimTarget.position = weaponSocket.parent.position;
            rightHandAimTarget.rotation = GetDesiredRightHandRotation();
            leftHandTarget.SetPositionAndRotation(leftHandGrip.position, leftHandGrip.rotation);
            upperBodyRig.weight = aimRig.CurrentWeight;
        }

        private void LateUpdate()
        {
            if (!IsConfigured || weaponSocketCalibrated || aimRig.CurrentWeight < 0.95f)
            {
                return;
            }

            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rightHand == null)
            {
                return;
            }

            Quaternion desiredSocketRotation = Quaternion.LookRotation(
                aimRig.GetDesiredAimDirection(),
                animator.transform.up);
            weaponSocket.localRotation = Quaternion.Inverse(rightHand.rotation) * desiredSocketRotation;
            weaponSocketCalibrated = true;
            rightHandConstraint.weight = 1f;
        }

        public void Configure(
            Animator sourceAnimator,
            TpsAimRig sourceAimRig,
            Transform sourceLeftHandGrip,
            Transform sourceWeaponSocket,
            float sourceLeftHandWeight)
        {
            animator = sourceAnimator != null ? sourceAnimator : GetComponent<Animator>();
            if (animator != null)
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            aimRig = sourceAimRig;
            leftHandGrip = sourceLeftHandGrip;
            weaponSocket = sourceWeaponSocket;
            leftHandWeight = Mathf.Clamp01(sourceLeftHandWeight);

            EnsureRigStructure();
            if (Application.isPlaying)
            {
                RebuildRigGraph();
            }
        }

        private void EnsureRigStructure()
        {
            if (animator == null || aimRig == null || leftHandGrip == null || weaponSocket == null || !animator.isHuman)
            {
                return;
            }

            rigBuilder = animator.GetComponent<RigBuilder>();
            if (rigBuilder == null)
            {
                rigBuilder = animator.gameObject.AddComponent<RigBuilder>();
            }

            Transform targetsRoot = GetOrCreateChild(animator.transform, "TPSRigTargets");
            aimTarget = GetOrCreateChild(targetsRoot, "AimTarget");
            if (aimTarget.localPosition.sqrMagnitude < 0.001f)
            {
                aimTarget.localPosition = new Vector3(0f, 1.45f, 25f);
            }

            rightHandAimTarget = GetOrCreateChild(targetsRoot, "RightHandAimTarget");
            leftHandTarget = GetOrCreateChild(targetsRoot, "LeftHandWeaponTarget");
            leftHandTarget.SetPositionAndRotation(leftHandGrip.position, leftHandGrip.rotation);

            leftElbowHint = GetOrCreateChild(targetsRoot, "LeftElbowHint");
            PositionElbowHint();

            Transform rigRoot = GetOrCreateChild(animator.transform, "Rig_TPSUpperBody");
            upperBodyRig = rigRoot.GetComponent<Rig>();
            if (upperBodyRig == null)
            {
                upperBodyRig = rigRoot.gameObject.AddComponent<Rig>();
            }

            ConfigureTorsoConstraints(rigRoot);
            ConfigureRightHandConstraint(rigRoot);
            ConfigureLeftHandConstraint(rigRoot);

            rigBuilder.layers.Clear();
            rigBuilder.layers.Add(new RigLayer(upperBodyRig));
        }

        private void ConfigureRightHandConstraint(Transform rigRoot)
        {
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rightHand == null)
            {
                return;
            }

            Transform constraintTransform = GetOrCreateChild(rigRoot, "Aim_RightHandWeapon");
            rightHandConstraint = constraintTransform.GetComponent<MultiRotationConstraint>();
            if (rightHandConstraint == null)
            {
                rightHandConstraint = constraintTransform.gameObject.AddComponent<MultiRotationConstraint>();
            }

            var sources = new WeightedTransformArray(1);
            sources[0] = new WeightedTransform(rightHandAimTarget, 1f);

            MultiRotationConstraintData data = rightHandConstraint.data;
            data.constrainedObject = rightHand;
            data.sourceObjects = sources;
            data.maintainOffset = false;
            data.offset = Vector3.zero;
            data.constrainedXAxis = true;
            data.constrainedYAxis = true;
            data.constrainedZAxis = true;
            rightHandConstraint.data = data;
            rightHandConstraint.weight = weaponSocketCalibrated ? 1f : 0f;
        }

        private void ConfigureTorsoConstraints(Transform rigRoot)
        {
            torsoConstraints.Clear();
            var usedBones = new HashSet<Transform>();

            for (int index = 0; index < AimBones.Length; index++)
            {
                Transform bone = animator.GetBoneTransform(AimBones[index]);
                if (bone == null || !usedBones.Add(bone))
                {
                    continue;
                }

                Transform constraintTransform = GetOrCreateChild(rigRoot, $"Aim_{AimBones[index]}");
                MultiAimConstraint constraint = constraintTransform.GetComponent<MultiAimConstraint>();
                if (constraint == null)
                {
                    constraint = constraintTransform.gameObject.AddComponent<MultiAimConstraint>();
                }

                var sources = new WeightedTransformArray(1);
                sources[0] = new WeightedTransform(aimTarget, 1f);

                MultiAimConstraintData data = constraint.data;
                data.constrainedObject = bone;
                data.sourceObjects = sources;
                data.aimAxis = MultiAimConstraintData.Axis.Z;
                data.upAxis = MultiAimConstraintData.Axis.Y;
                data.worldUpType = MultiAimConstraintData.WorldUpType.SceneUp;
                data.worldUpAxis = MultiAimConstraintData.Axis.Y;
                data.maintainOffset = true;
                data.limits = new Vector2(-35f, 35f);
                data.constrainedXAxis = true;
                data.constrainedYAxis = true;
                data.constrainedZAxis = true;
                constraint.data = data;
                constraint.weight = AimWeights[Mathf.Min(index, AimWeights.Length - 1)];
                torsoConstraints.Add(constraint);
            }
        }

        private void ConfigureLeftHandConstraint(Transform rigRoot)
        {
            Transform upperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform lowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (upperArm == null || lowerArm == null || hand == null)
            {
                return;
            }

            Transform constraintTransform = GetOrCreateChild(rigRoot, "IK_LeftHandWeaponGrip");
            leftHandConstraint = constraintTransform.GetComponent<TwoBoneIKConstraint>();
            if (leftHandConstraint == null)
            {
                leftHandConstraint = constraintTransform.gameObject.AddComponent<TwoBoneIKConstraint>();
            }

            TwoBoneIKConstraintData data = leftHandConstraint.data;
            data.root = upperArm;
            data.mid = lowerArm;
            data.tip = hand;
            data.target = leftHandTarget;
            data.hint = leftElbowHint;
            data.targetPositionWeight = 1f;
            data.targetRotationWeight = 0f;
            data.hintWeight = 0.75f;
            data.maintainTargetPositionOffset = false;
            data.maintainTargetRotationOffset = true;
            leftHandConstraint.data = data;
            leftHandConstraint.weight = leftHandWeight;
        }

        private void PositionElbowHint()
        {
            Transform elbow = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            if (elbow == null || leftElbowHint == null)
            {
                return;
            }

            Vector3 hintPosition = elbow.position - animator.transform.right * 0.3f - animator.transform.forward * 0.35f;
            leftElbowHint.position = hintPosition;
            leftElbowHint.rotation = animator.transform.rotation;
        }

        private Quaternion GetDesiredRightHandRotation()
        {
            Quaternion desiredSocketRotation = Quaternion.LookRotation(
                aimRig.GetDesiredAimDirection(),
                animator.transform.up);
            return desiredSocketRotation * Quaternion.Inverse(weaponSocket.localRotation);
        }

        private void RebuildRigGraph()
        {
            if (!Application.isPlaying || rigBuilder == null || rigBuilder.layers.Count == 0)
            {
                return;
            }

            rigBuilder.Build();
        }

        private static Transform GetOrCreateChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            var childObject = new GameObject(childName) { layer = parent.gameObject.layer };
            child = childObject.transform;
            child.SetParent(parent, false);
            return child;
        }
    }
}
