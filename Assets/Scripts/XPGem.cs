using UnityEngine;

public class XPGem : MonoBehaviour
{
    public float attractRangeMerged = 6f;  // когда вместе — собирается легче
    public float attractRangeSplit = 3f;   // когда раздельно
    public float speed = 10f;

    Transform target; // ближайший игрок
    public static bool IsMerged; // флаг от менеджера сплита

    void Update()
    {
        float r = IsMerged ? attractRangeMerged : attractRangeSplit;

        if (!target || Vector2.Distance(transform.position, target.position) > r)
            target = FindClosestPlayerWithin(r);

        if (target)
            transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);
    }

    Transform FindClosestPlayerWithin(float r)
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        Transform best = null;
        float bestSqr = r * r;
        foreach (var p in players)
        {
            float d = (p.transform.position - transform.position).sqrMagnitude;
            if (d <= bestSqr) { bestSqr = d; best = p.transform; }
        }
        return best;
    }

    public int xpValue = 1;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (GameProgression.I) GameProgression.I.AddXP(xpValue);
            Destroy(gameObject);
        }
    }

}
