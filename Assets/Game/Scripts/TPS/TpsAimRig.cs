using UnityEngine;

namespace StickManFight.Client.TPS
{
    [DefaultExecutionOrder(100)]
    public sealed class TpsAimRig : MonoBehaviour
    {
        [SerializeField] private TpsOrbitCamera orbitCamera;
        [SerializeField] private Transform aimOrigin;
        [SerializeField] private Transform muzzle;
        [SerializeField] private float blendSpeed = 12f;
        [SerializeField] private float aimRange = 150f;
        [SerializeField] private LayerMask aimMask = ~(1 << 2);
        private float targetWeight;
        private float currentWeight;
        private float recoil;

        internal Transform AimOrigin => aimOrigin;
        internal Transform Muzzle => muzzle;
        internal Vector3 RawAimForward { get; private set; }
        internal float RawAimError { get; private set; }
        internal float CurrentWeight => currentWeight;
        internal int EvaluationCount { get; private set; }

        private void Update()
        {
            currentWeight = Mathf.MoveTowards(currentWeight, targetWeight, blendSpeed * Time.deltaTime);
            recoil = Mathf.Lerp(recoil, 0f, 16f * Time.deltaTime);
            if (aimOrigin != null && muzzle != null)
            {
                EvaluationCount++;
                RawAimForward = GetCurrentWeaponAimDirection();
                RawAimError = Vector3.Angle(RawAimForward, GetDesiredAimDirection());
            }
        }

        public void SetAiming(bool aiming)
        {
            targetWeight = aiming ? 1f : 0f;
        }

        public void AddRecoil(float amount)
        {
            recoil -= amount;
        }

        internal void ConfigureAimAxis(Transform origin, Transform end)
        {
            aimOrigin = origin;
            muzzle = end;
        }

        internal Vector3 GetDesiredAimDirection()
        {
            if (aimOrigin == null)
            {
                return transform.forward;
            }

            Vector3 direction = GetAimPoint() - aimOrigin.position;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        }

        internal Vector3 GetAimPoint()
        {
            if (orbitCamera == null || orbitCamera.CameraComponent == null || aimOrigin == null)
            {
                return transform.position + transform.forward * aimRange;
            }

            Transform cameraTransform = orbitCamera.CameraComponent.transform;
            Vector3 aimPoint = cameraTransform.position + cameraTransform.forward * aimRange;
            if (Physics.Raycast(
                    cameraTransform.position,
                    cameraTransform.forward,
                    out RaycastHit hit,
                    aimRange,
                    aimMask,
                    QueryTriggerInteraction.Ignore))
            {
                aimPoint = hit.point;
            }

            Vector3 direction = aimPoint - aimOrigin.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = cameraTransform.forward;
            }

            float distance = Mathf.Max(direction.magnitude, 1f);
            direction.Normalize();
            if (Mathf.Abs(recoil) > 0.001f)
            {
                direction = Quaternion.AngleAxis(recoil, cameraTransform.right) * direction;
            }

            return aimOrigin.position + direction.normalized * distance;
        }

        internal Vector3 GetCurrentWeaponAimDirection()
        {
            if (aimOrigin == null || muzzle == null)
            {
                return transform.forward;
            }

            Vector3 direction = muzzle.position - aimOrigin.position;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : muzzle.forward;
        }

    }
}
