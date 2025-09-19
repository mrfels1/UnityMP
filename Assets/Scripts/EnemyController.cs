using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public float speed = 2.5f;
    public Rigidbody2D rb;
    Transform target; // ближайший игрок

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        if (!target || !target.gameObject.activeInHierarchy)
            target = FindClosestPlayer();

        if (target)
        {
            Vector2 dir = ((Vector2)target.position - rb.position).normalized;
            rb.MovePosition(rb.position + dir * speed * Time.fixedDeltaTime);
        }
    }

    Transform FindClosestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        Transform best = null;
        float bestSqr = float.MaxValue;
        foreach (var p in players)
        {
            float d = (p.transform.position - transform.position).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = p.transform; }
        }
        return best;
    }
}
