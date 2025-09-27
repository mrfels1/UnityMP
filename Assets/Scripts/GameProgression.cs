using UnityEngine;
using System;

public class GameProgression : MonoBehaviour
{
    public static GameProgression I;

    [Header("XP / уровни")]
    public int level = 1;
    public int xp = 0;
    public int baseXP = 5;

    // экспонента
    public bool useExpo = true;
    public float expoStart = 2f;                // уровень 1
    public float expoGrowth = 1.41421356f;      // ~sqrt(2)

    // или кривая (опционально)
    public AnimationCurve xpCurve = null;

    // хранение и округление
    public enum Rounding { Floor, Round, Ceil }
    public Rounding xpRounding = Rounding.Round;

    public int totalXpEarned;

    public float xpToNextExact { get; private set; }      // храним как float
    public int   xpToNext
    {
        get
        {
            float v = Mathf.Max(1f, xpToNextExact);
            return xpRounding == Rounding.Floor ? Mathf.FloorToInt(v)
                 : xpRounding == Rounding.Ceil  ? Mathf.CeilToInt(v)
                 : Mathf.RoundToInt(v);
        }
    }

    [Header("Баффы на уровень")]
    public int baseTargets = 1;
    public float baseFireRate = 1f, baseBulletSpeed = 1f, baseBulletRange = 1f;
    public float fireRatePerLevel = 0.06f, bulletSpeedPerLevel = 0.04f, bulletRangePerLevel = 0.04f;
    public int targetsEveryLevels = 5;

    public int targetsPerShot { get; private set; }
    public float fireRateMul { get; private set; }
    public float bulletSpeedMul { get; private set; }
    public float bulletRangeMul { get; private set; }
    public event System.Action OnStatsChanged;

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this; DontDestroyOnLoad(gameObject);
        RecalcXpToNext(); RecalcStats();
    }

    public void AddXP(int amount)
    {
        totalXpEarned += Mathf.Max(0, amount);
        xp += amount;
        // сравнение с ОКРУГЛЁННЫМ порогом уровня
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
        if (useExpo)
            xpToNextExact = expoStart * Mathf.Pow(expoGrowth, Mathf.Max(0, level - 1));
        else
        {
            float k = (xpCurve != null && xpCurve.length > 0) ? xpCurve.Evaluate(level) : 1f;
            xpToNextExact = Mathf.Max(1f, baseXP * (level + 1) * k);
        }
    }

    void RecalcStats()
    {
        targetsPerShot = baseTargets + Mathf.Max(0, targetsEveryLevels > 0 ? (level - 1) / targetsEveryLevels : 0);
        fireRateMul    = baseFireRate    * (1f + fireRatePerLevel    * (level - 1));
        bulletSpeedMul = baseBulletSpeed * (1f + bulletSpeedPerLevel * (level - 1));
        bulletRangeMul = baseBulletRange * (1f + bulletRangePerLevel * (level - 1));
        OnStatsChanged?.Invoke();
    }
}