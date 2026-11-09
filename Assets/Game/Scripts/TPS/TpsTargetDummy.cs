using System.Collections;
using UnityEngine;

namespace StickManFight.Client.TPS
{
    public sealed class TpsTargetDummy : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private float resetDelay = 2f;
        [SerializeField] private Renderer targetRenderer;

        private int health;
        private Collider targetCollider;
        private Color baseColor;

        private void Awake()
        {
            health = maxHealth;
            targetCollider = GetComponent<Collider>();
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }

            if (targetRenderer != null)
            {
                baseColor = targetRenderer.material.color;
            }
        }

        public void TakeDamage(int damage)
        {
            if (health <= 0)
            {
                return;
            }

            health = Mathf.Max(0, health - damage);
            StopAllCoroutines();
            StartCoroutine(health == 0 ? ResetRoutine() : HitFlashRoutine());
        }

        private IEnumerator HitFlashRoutine()
        {
            if (targetRenderer != null)
            {
                targetRenderer.material.color = Color.white;
            }

            yield return new WaitForSeconds(0.08f);
            if (targetRenderer != null)
            {
                targetRenderer.material.color = baseColor;
            }
        }

        private IEnumerator ResetRoutine()
        {
            if (targetRenderer != null) targetRenderer.enabled = false;
            if (targetCollider != null) targetCollider.enabled = false;
            yield return new WaitForSeconds(resetDelay);
            health = maxHealth;
            if (targetRenderer != null)
            {
                targetRenderer.enabled = true;
                targetRenderer.material.color = baseColor;
            }
            if (targetCollider != null) targetCollider.enabled = true;
        }
    }
}
