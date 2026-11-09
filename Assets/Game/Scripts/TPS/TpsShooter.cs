using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StickManFight.Client.TPS
{
    public sealed class TpsShooter : MonoBehaviour
    {
        [SerializeField] private TpsOrbitCamera orbitCamera;
        [SerializeField] private TpsAimRig aimRig;
        [SerializeField] private Transform muzzle;
        [SerializeField] private ParticleSystem muzzleFlash;
        [SerializeField] private LineRenderer tracer;
        [SerializeField] private Material impactMaterial;
        [SerializeField] private int magazineSize = 30;
        [SerializeField] private int damage = 34;
        [SerializeField] private float roundsPerSecond = 8f;
        [SerializeField] private float range = 150f;
        [SerializeField] private float reloadDuration = 1.15f;
        [SerializeField] private LayerMask hitMask = ~(1 << 2);

        private int ammo;
        private float nextShotTime;
        private bool reloading;
        private float hitMarkerUntil;

        private void Awake()
        {
            ammo = magazineSize;
            if (tracer != null)
            {
                tracer.enabled = false;
            }
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse == null || keyboard == null || Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame && ammo < magazineSize && !reloading)
            {
                StartCoroutine(ReloadRoutine());
            }

            if (!reloading && orbitCamera != null && orbitCamera.IsAiming && mouse.leftButton.isPressed && Time.time >= nextShotTime)
            {
                Fire();
            }
        }

        private void Fire()
        {
            if (ammo <= 0)
            {
                StartCoroutine(ReloadRoutine());
                return;
            }

            ammo--;
            nextShotTime = Time.time + 1f / roundsPerSecond;

            Camera cameraComponent = orbitCamera.CameraComponent;
            Vector3 origin = cameraComponent.transform.position;
            Vector3 direction = cameraComponent.transform.forward;
            Vector3 hitPoint = origin + direction * range;
            Vector3 hitNormal = -direction;
            bool hitSomething = Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore);

            if (hitSomething)
            {
                hitPoint = hit.point;
                hitNormal = hit.normal;
                TpsTargetDummy dummy = hit.collider.GetComponentInParent<TpsTargetDummy>();
                if (dummy != null)
                {
                    dummy.TakeDamage(damage);
                    hitMarkerUntil = Time.time + 0.12f;
                }

                CreateImpact(hitPoint, hitNormal);
            }

            if (muzzleFlash != null)
            {
                muzzleFlash.Emit(8);
            }

            if (tracer != null && muzzle != null)
            {
                StartCoroutine(TracerRoutine(muzzle.position, hitPoint));
            }

            orbitCamera.AddRecoil(1.2f, Random.Range(-0.35f, 0.35f));
            if (aimRig != null)
            {
                aimRig.AddRecoil(3.2f);
            }
        }

        internal void FireForDiagnostics()
        {
            if (!reloading && Time.time >= nextShotTime)
            {
                Fire();
            }
        }

        private IEnumerator ReloadRoutine()
        {
            reloading = true;
            yield return new WaitForSeconds(reloadDuration);
            ammo = magazineSize;
            reloading = false;
        }

        private IEnumerator TracerRoutine(Vector3 start, Vector3 end)
        {
            tracer.enabled = true;
            tracer.positionCount = 2;
            tracer.SetPosition(0, start);
            tracer.SetPosition(1, end);
            yield return new WaitForSeconds(0.045f);
            tracer.enabled = false;
        }

        private void CreateImpact(Vector3 position, Vector3 normal)
        {
            GameObject impact = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            impact.name = "ClientImpact";
            impact.transform.position = position + normal * 0.01f;
            impact.transform.localScale = Vector3.one * 0.08f;
            Destroy(impact.GetComponent<Collider>());
            Renderer renderer = impact.GetComponent<Renderer>();
            if (impactMaterial != null)
            {
                renderer.sharedMaterial = impactMaterial;
            }
            Destroy(impact, 0.25f);
        }

        private void OnGUI()
        {
            if (orbitCamera == null)
            {
                return;
            }

            GUIStyle centered = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Time.time < hitMarkerUntil ? Color.red : Color.white }
            };

            string reticle = Time.time < hitMarkerUntil ? "×" : orbitCamera.IsAiming ? "+" : "·";
            GUI.Label(new Rect(Screen.width * 0.5f - 20f, Screen.height * 0.5f - 20f, 40f, 40f), reticle, centered);

            GUIStyle hud = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            string ammoText = reloading ? "RELOADING" : $"{ammo} / {magazineSize}";
            GUI.Label(new Rect(Screen.width - 185f, Screen.height - 55f, 165f, 35f), ammoText, hud);
            GUI.Label(new Rect(18f, Screen.height - 78f, 560f, 60f),
                "WASD 이동  Shift 질주  Alt 걷기  C 웅크리기  Space 점프\nRMB 조준  LMB 사격  R 재장전  Q 어깨 전환  Esc 커서 해제",
                GUI.skin.label);
        }
    }
}
