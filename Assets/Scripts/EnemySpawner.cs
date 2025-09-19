using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public float radius = 14f;
    public float spawnPerSecondStart = 0.8f;
    public float spawnPerSecondMax = 6f;
    public float rampUpSeconds = 240f; // к 4-й минуте

    float accum;
    Transform[] players;

    void Start()
    {
        var ps = GameObject.FindGameObjectsWithTag("Player");
        players = new Transform[ps.Length];
        for (int i = 0; i < ps.Length; i++) players[i] = ps[i].transform;
    }

    void Update()
    {
        float t = Mathf.Clamp01(Time.timeSinceLevelLoad / rampUpSeconds);
        float rate = Mathf.Lerp(spawnPerSecondStart, spawnPerSecondMax, t);

        accum += rate * Time.deltaTime;
        while (accum >= 1f)
        {
            SpawnEnemy();
            accum -= 1f;
        }
    }

    void SpawnEnemy()
    {
        if (players.Length == 0) return;
        // точка относительно среднего игроков (чтоб не терялись)
        Vector3 mid = Vector3.zero;
        foreach (var p in players) mid += p.position;
        mid /= players.Length;

        float angle = Random.value * Mathf.PI * 2f;
        Vector3 pos = mid + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;

        Instantiate(enemyPrefab, pos, Quaternion.identity);
    }
}
