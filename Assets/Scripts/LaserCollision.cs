using UnityEngine;

/// <summary>
/// Standalone trigger collision script for a laser projectile.
/// Use this only if collision is not already handled by LaserBehavior on the same prefab.
/// </summary>
public class LaserCollision : MonoBehaviour
{
    [Header("VFX")]
    [Tooltip("Cyan spark burst spawned when the laser hits a Shield.")]
    public GameObject shieldSparksPrefab;

    [Tooltip("Optional hit effect spawned when the laser hits the Player body.")]
    public GameObject bodyHitPrefab;

    private bool resolved;

    private void Awake()
    {
        // LaserBehavior is the main EverMotion laser controller. Avoid double collision handling if both are present.
        if (GetComponent<LaserBehavior>() != null)
        {
            enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (resolved)
        {
            return;
        }

        if (col.CompareTag("Shield"))
        {
            resolved = true;
            Vector2 impactPosition = col.ClosestPoint(transform.position);
            NotifyShieldImpact(col, impactPosition);
            SpawnVfx(shieldSparksPrefab, impactPosition);
            Debug.Log("Perfect Block! +1 Energy");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddBlock();
            }

            Destroy(gameObject);
            return;
        }

        if (col.CompareTag("Player"))
        {
            resolved = true;
            SpawnVfx(bodyHitPrefab, col.ClosestPoint(transform.position));
            Debug.Log("Hit Body! Missed!");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterBodyHit();
            }

            Destroy(gameObject);
        }
    }

    private void NotifyShieldImpact(Collider2D shieldCollider, Vector2 impactPosition)
    {
        ShieldManager shieldManager = shieldCollider.GetComponentInParent<ShieldManager>();
        if (shieldManager != null)
        {
            shieldManager.RegisterShieldImpact(impactPosition);
        }

        PushShieldVfx pushShieldVfx = shieldCollider.GetComponentInParent<PushShieldVfx>();
        if (pushShieldVfx != null)
        {
            pushShieldVfx.PlayImpact(impactPosition);
        }
    }

    private void SpawnVfx(GameObject prefab, Vector2 position)
    {
        if (prefab == null)
        {
            return;
        }

        Instantiate(prefab, position, Quaternion.identity);
    }
}
