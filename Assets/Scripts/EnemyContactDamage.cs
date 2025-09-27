using UnityEngine;

/// <summary>
/// Вешается на врага. При столкновении с игроком наносит урон игроку
/// и (опционально) себе. Работает с 2D триггерами и коллизией.
/// Для урона врагу ищет компонент "Health" и пытается вызвать TakeDamage(int).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class EnemyContactDamage : MonoBehaviour
{
    public int damageToPlayer = 1;
    public int selfDamageOnHit = 1; // 0 чтобы враг не страдал

    void OnCollisionEnter2D(Collision2D c)  { TryHit(c.collider); }
    void OnTriggerEnter2D(Collider2D other){ TryHit(other); }

    void TryHit(Collider2D other)
    {
        if (!other || !other.CompareTag("Player")) return;

        var ph = other.GetComponent<PlayerHealth>();
        if (ph) ph.ApplyDamage(damageToPlayer);

        if (selfDamageOnHit > 0)
        {
            // найти Health и вызвать TakeDamage(int)
            var h = GetComponent("Health");
            if (h != null)
            {
                var mi = h.GetType().GetMethod("TakeDamage");
                if (mi != null) mi.Invoke(h, new object[]{ selfDamageOnHit });
            }
        }
    }
}
