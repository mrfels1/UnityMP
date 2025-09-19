using UnityEngine;
using System.Linq;

public class AutoShooter : MonoBehaviour
{
    public GameObject bulletPrefab;
    public float fireRate = 2f;
    public float range = 8f;
    public float bulletSpeed = 16f;
    public LayerMask enemyMask;

    float nextFire;

    void Update()
    {
        if (Time.time < nextFire) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, range, enemyMask);
        if (hits.Length == 0) return;

        // цель — ближайший
        Transform t = hits
            .OrderBy(h => (h.transform.position - transform.position).sqrMagnitude)
            .First().transform;

        Vector2 dir = (t.position - transform.position).normalized;

        var bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
        var rb = bullet.GetComponent<Rigidbody2D>();
        rb.linearVelocity = dir * bulletSpeed;

        nextFire = Time.time + 1f / fireRate;
    }
}
