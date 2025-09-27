using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 2–4 игрока. Респаун умершего у ближайшего живого через задержку.
/// Game Over при смерти всех. Показывает totalXpEarned.
/// </summary>
public class GameSessionManager4 : MonoBehaviour
{
    public static GameSessionManager4 I;

    [Header("UI")]
    public TextMeshProUGUI[] timerTexts = new TextMeshProUGUI[4];
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverText;

    [Header("Respawn")]
    public float respawnDelay = 30f;

    // Массив слотов — только для привязки таймеров. Логика живых использует поиск по сцене.
    [Header("Slots (только для сопоставления таймеров)")]
    public PlayerHealth[] players = new PlayerHealth[4];

    readonly Dictionary<PlayerHealth, Coroutine> respawns = new();
    readonly Dictionary<PlayerHealth, Vector3> lastDeathPos = new();

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this; DontDestroyOnLoad(gameObject);

        if (gameOverPanel) gameOverPanel.SetActive(false);
        HideAllTimers();
        Time.timeScale = 1f;
    }

    void Start() => HideAllTimers();

    // Внешний вызов при смерти игрока
    public void NotifyPlayerDied(PlayerHealth who)
    {
        if (!who) return;

        lastDeathPos[who] = who.transform.position;

        if (!respawns.ContainsKey(who))
            respawns[who] = StartCoroutine(RespawnAfter(who));

        if (!AnyAlive()) GameOver();
    }

    IEnumerator RespawnAfter(PlayerHealth dead)
    {
        int idx = GetTimerIndex(dead);
        TextMeshProUGUI txt = (idx >= 0 && idx < timerTexts.Length) ? timerTexts[idx] : null;

        float t = Mathf.Max(0f, respawnDelay);
        if (txt) { txt.gameObject.SetActive(true); txt.text = Mathf.CeilToInt(t).ToString(); }

        while (t > 0f)
        {
            if (!AnyAliveExcept(dead)) { GameOver(); Cleanup(dead, idx); yield break; }

            t -= Time.unscaledDeltaTime;
            if (txt) txt.text = Mathf.CeilToInt(Mathf.Max(0f, t)).ToString();
            yield return null;
        }

        // ближайший живой к позиции смерти
        var mate = NearestAliveTo(dead);
        if (mate && dead) {
            Vector3 spawnPos = mate.transform.position;
            dead.RespawnAt(spawnPos);
        } else {
            Debug.LogWarning("GameSessionManager4: нет живого напарника для респавна.");
        }

        Cleanup(dead, idx);
    }

    void Cleanup(PlayerHealth dead, int idx)
    {
        if (idx >= 0 && idx < timerTexts.Length && timerTexts[idx])
        {
            timerTexts[idx].text = "*";
            timerTexts[idx].gameObject.SetActive(true);
        }
        respawns.Remove(dead);
        lastDeathPos.Remove(dead);
    }

    // === Жизненный статус и поиск напарника — всегда по актуальным объектам в сцене ===
    IEnumerable<PlayerHealth> ScenePlayers()
    {
        // Берём всех, даже если отключены (true) — компонент может быть на неактивном GO.
        return FindObjectsOfType<PlayerHealth>(true);
    }

    bool AnyAlive()
    {
        foreach (var p in ScenePlayers()) if (p && p.IsAlive) return true;
        return false;
    }

    bool AnyAliveExcept(PlayerHealth except)
    {
        foreach (var p in ScenePlayers()) if (p && p != except && p.IsAlive) return true;
        return false;
    }

    PlayerHealth NearestAliveTo(PlayerHealth dead)
    {
        Vector3 refPos = (dead && dead.transform) ? dead.transform.position :
                         (lastDeathPos.TryGetValue(dead, out var pos) ? pos : Vector3.zero);

        PlayerHealth best = null; float bestD = float.PositiveInfinity;
        foreach (var p in ScenePlayers())
        {
            if (!p || !p.IsAlive || p == dead) continue;
            float d = (p.transform.position - refPos).sqrMagnitude;
            if (d < bestD) { bestD = d; best = p; }
        }
        return best;
    }

    // === Таймеры ===
    void HideAllTimers()
    {
        for (int i = 0; i < timerTexts.Length; i++)
            if (timerTexts[i]) { timerTexts[i].gameObject.SetActive(true); timerTexts[i].text = "*"; }
    }

    int GetTimerIndex(PlayerHealth ph)
    {
        // 1) Жёсткое сопоставление со слотами, если задано
        int idx = IndexOf(ph);
        if (idx != -1) return idx;

#if ENABLE_INPUT_SYSTEM
        // 2) Если есть PlayerInput — используем playerIndex
        var pi = ph ? ph.GetComponent<UnityEngine.InputSystem.PlayerInput>() : null;
        if (pi) return Mathf.Clamp(pi.playerIndex, 0, timerTexts.Length - 1);
#endif
        // 3) Фолбэк: стабильный индекс по instanceID
        return Mathf.Abs(ph ? ph.GetInstanceID() : 0) % timerTexts.Length;
    }

    int IndexOf(PlayerHealth ph)
    {
        for (int i = 0; i < players.Length; i++)
            if (players[i] == ph) return i;
        return -1;
    }

    // === Game Over ===
    void GameOver()
    {
        foreach (var kv in respawns) if (kv.Value != null) StopCoroutine(kv.Value);
        respawns.Clear();
        HideAllTimers();

        if (gameOverPanel) gameOverPanel.SetActive(true);
        if (!gameOverText)
            gameOverText = GameObject.Find("GameOverText")?.GetComponent<TextMeshProUGUI>();

        int totalXP = GameProgression.I ? GameProgression.I.totalXpEarned : 0;
        if (gameOverText) gameOverText.text = $"Game Over\nXP: {totalXP}";

        Time.timeScale = 0f;
    }
}
