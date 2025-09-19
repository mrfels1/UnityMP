using UnityEngine;
using System.Linq;

public class AutoShooter : MonoBehaviour
{
    public GameObject bulletPrefab;
    public float fireRate = 2f;     // базовая скорострельность (выстр./с)
    public float range = 8f;
    public float bulletSpeed = 16f; // базовая скорость
    public LayerMask enemyMask;

    int targetsPerShot = 1;
    float fireRateMul = 1f;
    float bulletSpeedMul = 1f;
    float nextFire;

    void OnEnable()
    {
        if (GameProgression.I)
        {
            ApplyStats();
            GameProgression.I.OnStatsChanged += ApplyStats;
        }
    }
    void OnDisable()
    {
        if (GameProgression.I)
            GameProgression.I.OnStatsChanged -= ApplyStats;
    }
    void ApplyStats()
    {
        var gp = GameProgression.I;
        targetsPerShot = gp ? gp.targetsPerShot : 1;
        fireRateMul    = gp ? gp.fireRateMul    : 1f;
        bulletSpeedMul = gp ? gp.bulletSpeedMul : 1f;
    }

    void Update()
    {
        if (Time.time < nextFire) return;

        var hits = Physics2D.OverlapCircleAll(transform.position, range, enemyMask);
        if (hits == null || hits.Length == 0) return;

        // ближайшие N целей
        var targets = hits
            .OrderBy(h => (h.transform.position - transform.position).sqrMagnitude)
            .Take(Mathf.Max(1, targetsPerShot))
            .Select(h => h.transform);

        foreach (var t in targets)
        {
            Vector2 dir = (t.position - transform.position).normalized;
            var bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
            var rb = bullet.GetComponent<Rigidbody2D>();
            if (rb) rb.linearVelocity = dir * (bulletSpeed * bulletSpeedMul);
        }

        nextFire = Time.time + 1f / (fireRate * fireRateMul);
    }
}
