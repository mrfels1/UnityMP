using UnityEngine;
public class Health : MonoBehaviour
{
    public int max = 30;
    public int current;
    public GameObject onDeathSpawn; // например, гем

    void Awake() => current = max;

    public void Apply(int dmg)
    {
        current -= dmg;
        if (current <= 0)
        {
            if (onDeathSpawn) Instantiate(onDeathSpawn, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }
}
