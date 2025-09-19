using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if TMP_PRESENT || UNITY_TEXTMESHPRO
using TMPro;
#endif

public class LevelCounterUI : MonoBehaviour
{
    [Header("Refs")]
    public TextMeshProUGUI levelText;   // "LV 1"
    public TextMeshProUGUI xpText;      // "3 / 10" (опц.)
    public Image xpFill;                // Image type = Filled, Fill Method = Horizontal

    GameProgression gp;
    int _lvl, _xp, _xpNext;

    void Awake()
    {
        gp = GameProgression.I; // может быть создан на старте
    }

    void Start()
    {
        ForceRefresh();
    }

    void Update()
    {
        if (!gp) gp = GameProgression.I;
        if (!gp) return;

        if (_lvl != gp.level || _xp != gp.xp || _xpNext != gp.xpToNext)
            ForceRefresh();
    }

    void ForceRefresh()
    {
        if (!gp) return;
        _lvl = gp.level; _xp = gp.xp; _xpNext = Mathf.Max(1, gp.xpToNext);
        if (levelText) levelText.text = "LV " + _lvl.ToString();
        if (xpText)    xpText.text    = _xp.ToString() + " / " + _xpNext.ToString();
        if (xpFill)    xpFill.fillAmount = Mathf.Clamp01((float)_xp / _xpNext);
    }
}
