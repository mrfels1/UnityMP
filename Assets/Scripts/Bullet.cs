using UnityEngine;

public class Bullet : MonoBehaviour
{
    public int damage = 5;
    public float life = 2f;

    void Start() => Destroy(gameObject, life);

    void OnTriggerEnter2D(Collider2D other)
    {
        var hp = other.GetComponent<Health>();
        if (hp)
        {
            hp.Apply(damage);
            Destroy(gameObject);
        }
    }
}
