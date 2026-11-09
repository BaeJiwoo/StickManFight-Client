using UnityEngine;
using UnityEngine.InputSystem;

namespace StickManFight.Client.TPS
{
    [RequireComponent(typeof(Camera))]
    public sealed class TpsOrbitCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float pivotHeight = 1.45f;
        [SerializeField] private float normalDistance = 4.6f;
        [SerializeField] private float aimDistance = 3.1f;
        [SerializeField] private float normalShoulderOffset = 0.72f;
        [SerializeField] private float aimShoulderOffset = 0.95f;
        [SerializeField] private float verticalOffset = 0.25f;
        [SerializeField] private float sensitivity = 0.12f;
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private float normalFov = 65f;
        [SerializeField] private float aimFov = 52f;
        [SerializeField] private float transitionSpeed = 12f;
        [SerializeField] private float collisionRadius = 0.22f;
        [SerializeField] private LayerMask collisionMask = ~(1 << 2);

        private Camera cameraComponent;
        private float yaw;
        private float pitch = 6f;
        private float shoulderSide = 1f;
        private float recoilPitch;
        private float recoilYaw;
        private bool diagnosticAim;

        public Camera CameraComponent => cameraComponent;
        public bool IsAiming { get; private set; }
        public float AimPitch => pitch + recoilPitch;
        public Vector3 PlanarForward
        {
            get
            {
                Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                return forward.normalized;
            }
        }

        public Vector3 PlanarRight => Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

        private void Awake()
        {
            cameraComponent = GetComponent<Camera>();
            yaw = target != null ? target.eulerAngles.y : transform.eulerAngles.y;
            LockCursor(true);
        }

        private void OnDisable()
        {
            LockCursor(false);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                LockCursor(false);
            }
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor(true);
            }

            IsAiming = diagnosticAim || (mouse != null && mouse.rightButton.isPressed && Cursor.lockState == CursorLockMode.Locked);

            if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
            {
                shoulderSide *= -1f;
            }

            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = mouse.delta.ReadValue() * sensitivity;
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);
            }

            recoilPitch = Mathf.Lerp(recoilPitch, 0f, 14f * Time.deltaTime);
            recoilYaw = Mathf.Lerp(recoilYaw, 0f, 14f * Time.deltaTime);
            cameraComponent.fieldOfView = Mathf.Lerp(
                cameraComponent.fieldOfView,
                IsAiming ? aimFov : normalFov,
                transitionSpeed * Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(pitch + recoilPitch, yaw + recoilYaw, 0f);
            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            float distance = Mathf.Lerp(normalDistance, aimDistance, IsAiming ? 1f : 0f);
            float shoulder = Mathf.Lerp(normalShoulderOffset, aimShoulderOffset, IsAiming ? 1f : 0f) * shoulderSide;
            Vector3 localOffset = new Vector3(shoulder, verticalOffset, -distance);
            Vector3 desiredPosition = pivot + rotation * localOffset;
            Vector3 direction = desiredPosition - pivot;
            float desiredDistance = direction.magnitude;

            if (desiredDistance > 0.001f && Physics.SphereCast(
                    pivot,
                    collisionRadius,
                    direction.normalized,
                    out RaycastHit hit,
                    desiredDistance,
                    collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                desiredPosition = pivot + direction.normalized * Mathf.Max(0.1f, hit.distance - collisionRadius);
            }

            transform.SetPositionAndRotation(desiredPosition, rotation);
        }

        public void AddRecoil(float pitchKick, float yawKick)
        {
            recoilPitch -= pitchKick;
            recoilYaw += yawKick;
        }

        internal void SetDiagnosticAim(bool value)
        {
            diagnosticAim = value;
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
