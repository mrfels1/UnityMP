using UnityEngine;
using System;

public class GameProgression : MonoBehaviour
{
    public static GameProgression I;

    [Header("XP / уровни")]
    public int level = 1;
    public int xp = 0;
    public int xpToNext = 5;
    public int baseXP = 5;
    // Рост требования по кривой: множитель к (level+1)
    public AnimationCurve xpCurve = new AnimationCurve(
        new Keyframe(1, 1f),   // ур.1 → 1×
        new Keyframe(10, 2.2f),
        new Keyframe(20, 3.5f),
        new Keyframe(30, 5.0f)
    );

    [Header("Баффы на уровень")]
    public int baseTargets = 1;           // одновременных целей на ур.1
    public float baseFireRate = 1f;       // множитель скорострельности на ур.1
    public float baseBulletSpeed = 1f;    // множитель скорости пули на ур.
    public float baseBulletRange = 1f;
    public float fireRatePerLevel = 0.06f;
    public float bulletSpeedPerLevel = 0.04f;
    public float bulletRangePerLevel = 0.04f;
    public int targetsEveryLevels = 5;    // +1 цель каждые N уровней

    // Текущие значения
    public int targetsPerShot { get; private set; }
    public float fireRateMul { get; private set; }
    public float bulletSpeedMul { get; private set; }

    public float bulletRangeMul { get; private set; }

    public event Action OnStatsChanged;

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        RecalcXpToNext();
        RecalcStats();
    }

    public void AddXP(int amount)
    {
        xp += amount;
        while (xp >= xpToNext)
        {
            xp -= xpToNext;
            level++;
            RecalcXpToNext();
            RecalcStats();
        }
    }

    void RecalcXpToNext()
    {
        float k = Mathf.Max(0.5f, xpCurve.Evaluate(level)); // страховка
        xpToNext = Mathf.Max(1, Mathf.RoundToInt(baseXP * (level + 1) * k));
    }

    void RecalcStats()
    {
        targetsPerShot = baseTargets + Mathf.Max(0, targetsEveryLevels > 0 ? (level - 1) / targetsEveryLevels : 0);
        fireRateMul = baseFireRate * (1f + fireRatePerLevel * (level - 1));
        bulletSpeedMul = baseBulletSpeed * (1f + bulletSpeedPerLevel * (level - 1));
        bulletRangeMul = baseBulletRange * (1f + bulletRangePerLevel * (level - 1));
        OnStatsChanged?.Invoke();
    }
}
