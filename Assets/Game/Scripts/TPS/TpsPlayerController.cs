using UnityEngine;
using UnityEngine.InputSystem;

namespace StickManFight.Client.TPS
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class TpsPlayerController : MonoBehaviour
    {
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int CrouchingHash = Animator.StringToHash("Crouching");
        private static readonly int SprintingHash = Animator.StringToHash("Sprinting");
        private static readonly int AimingHash = Animator.StringToHash("Aiming");

        [SerializeField] private Animator animator;
        [SerializeField] private TpsOrbitCamera orbitCamera;
        [SerializeField] private TpsAimRig aimRig;
        [SerializeField] private float walkSpeed = 2.4f;
        [SerializeField] private float runSpeed = 4.8f;
        [SerializeField] private float sprintSpeed = 7.2f;
        [SerializeField] private float crouchSpeed = 2.1f;
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float rotationSpeed = 14f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchingHeight = 1.15f;

        private CharacterController controller;
        private Vector3 planarVelocity;
        private float verticalVelocity;
        private bool crouching;

        public bool IsAiming => orbitCamera != null && orbitCamera.IsAiming;
        public bool IsCrouching => crouching;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || orbitCamera == null)
            {
                return;
            }

            if (keyboard.cKey.wasPressedThisFrame)
            {
                crouching = !crouching;
            }

            Vector2 input = ReadMoveInput(keyboard);
            bool aiming = orbitCamera.IsAiming;
            bool walking = keyboard.leftAltKey.isPressed;
            bool sprinting = !aiming && !crouching && input.y > 0.5f && keyboard.leftShiftKey.isPressed;
            bool grounded = controller.isGrounded;

            Vector3 desiredDirection = orbitCamera.PlanarForward * input.y + orbitCamera.PlanarRight * input.x;
            desiredDirection = Vector3.ClampMagnitude(desiredDirection, 1f);

            float targetSpeed = crouching
                ? crouchSpeed
                : sprinting
                    ? sprintSpeed
                    : walking
                        ? walkSpeed
                        : runSpeed;

            Vector3 desiredVelocity = desiredDirection * targetSpeed;
            planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVelocity, acceleration * Time.deltaTime);

            Vector3 facingDirection = aiming ? orbitCamera.PlanarForward : desiredDirection;
            if (facingDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(facingDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            if (grounded)
            {
                if (verticalVelocity < 0f)
                {
                    verticalVelocity = -2f;
                }

                if (!crouching && keyboard.spaceKey.wasPressedThisFrame)
                {
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    grounded = false;
                }
            }

            verticalVelocity += gravity * Time.deltaTime;
            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
            UpdateControllerHeight();
            UpdateAnimator(grounded, aiming, sprinting);

            if (aimRig != null)
            {
                aimRig.SetAiming(aiming);
            }
        }

        private void UpdateControllerHeight()
        {
            float targetHeight = crouching ? crouchingHeight : standingHeight;
            controller.height = Mathf.Lerp(controller.height, targetHeight, 14f * Time.deltaTime);
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
        }

        private void UpdateAnimator(bool grounded, bool aiming, bool sprinting)
        {
            if (animator == null)
            {
                return;
            }

            Vector3 localVelocity = transform.InverseTransformDirection(planarVelocity);
            animator.SetFloat(MoveXHash, Mathf.Clamp(localVelocity.x / runSpeed, -1f, 1f), 0.08f, Time.deltaTime);
            animator.SetFloat(MoveYHash, Mathf.Clamp(localVelocity.z / runSpeed, -1f, 1f), 0.08f, Time.deltaTime);
            animator.SetFloat(SpeedHash, planarVelocity.magnitude, 0.08f, Time.deltaTime);
            animator.SetFloat(VerticalSpeedHash, verticalVelocity);
            animator.SetBool(GroundedHash, grounded);
            animator.SetBool(CrouchingHash, crouching);
            animator.SetBool(SprintingHash, sprinting);
            animator.SetBool(AimingHash, aiming);
        }

        private static Vector2 ReadMoveInput(Keyboard keyboard)
        {
            Vector2 input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            return Vector2.ClampMagnitude(input, 1f);
        }
    }
}
