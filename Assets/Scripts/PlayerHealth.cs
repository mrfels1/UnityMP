using UnityEngine;
using System.Collections;

/// <summary>
/// Жизнь игрока: урон при контакте с врагом берёт EnemyContactDamage.
/// Смерть отключает управление/стрельбу и оповещает GameSessionManager.
/// Respawn делает сам GameSessionManager, вызывая RespawnAt().
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlayerHealth : MonoBehaviour
{
    [Header("HP")]
    public int maxHP = 5;
    public int currentHP;
    public float hitInvuln = 0.2f; // защита от мульти-коллизий

    [Header("Visuals")]
    public SpriteRenderer[] renderersToHide; // опц., если пусто — найдём сами

    [HideInInspector] public bool IsAlive = true;

    float lastHitTime = -999f;
    Collider2D col;
    Behaviour moveComp;      // PlayerController2D если есть
    Behaviour shootComp;     // AutoShooter если есть

    
    void Awake()
    {
        currentHP = maxHP;
        col = GetComponent<Collider2D>();
        moveComp = GetComponent<Behaviour>(); // подменим в Start
    }

    void Start()
    {
        // Поищем типичные компоненты
        var m = GetComponent("PlayerController2D") as Behaviour; if (m) moveComp = m;
        var s = GetComponent("AutoShooter") as Behaviour;       if (s) shootComp = s; // fileciteturn7file2
        if (renderersToHide == null || renderersToHide.Length == 0)
            renderersToHide = GetComponentsInChildren<SpriteRenderer>(true);
    }

    public void ApplyDamage(int dmg)
    {
        if (!IsAlive) return;
        if (Time.time - lastHitTime < hitInvuln) return;
        lastHitTime = Time.time;
        currentHP -= Mathf.Max(1, dmg);
        if (currentHP <= 0) Die();
    }

    void Die()
    {
        IsAlive = false;
        currentHP = 0;
        if (col) col.enabled = false;
        if (moveComp) moveComp.enabled = false;
        if (shootComp) shootComp.enabled = false;
        SetVisible(false);
        var gsm4 = GameSessionManager4.I ?? FindFirstObjectByType<GameSessionManager4>();
        if (gsm4) gsm4.NotifyPlayerDied(this);
    }

    public void RespawnAt(Vector3 worldPos)
    {
        transform.position = worldPos;
        currentHP = maxHP;
        IsAlive = true;
        if (col) col.enabled = true;
        if (moveComp) moveComp.enabled = true;
        if (shootComp) shootComp.enabled = true;
        SetVisible(true);
    }

    void SetVisible(bool v)
    {
        if (renderersToHide == null) return;
        foreach (var r in renderersToHide) if (r) r.enabled = v;
    }
}
