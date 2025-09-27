using UnityEngine;
using TMPro;

/// <summary>
/// Надголовная полоска HP. Показывается только если HP < maxHP и игрок жив.
/// Без UnityEngine.UI. Текст только через TextMeshPro (3D, World Space).
/// Вешайте на объект игрока рядом с PlayerHealth.
/// </summary>
[DefaultExecutionOrder(50)]
public class HealthBarAbove : MonoBehaviour
{
    public PlayerHealth source;                   // если не задан, найдём на этом объекте

    [Header("Layout")]
    public Vector2 size = new Vector2(1.6f, 0.2f); // ширина×высота в мировых единицах
    public Vector3 offset = new Vector3(0, 1.2f, 0); // сдвиг над головой; (0,0,0) → авто по коллайдеру
    public float hideDelay = 0.75f;               // задержка скрытия после полного HP

    [Header("Style")]
    public Color bgColor = new Color(0, 0, 0, 0.5f);
    public Color fillColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    public string sortingLayerName = "Default";  // можно свой слой, например "UIWorld"
    public int sortingOrder = 100;                // поверх спрайта игрока
    public float smooth = 12f;                    // сглаживание заполнения

    [Header("Text (TMP)")]
    public bool showText = false;
    public float textSize = 2.5f;                 // размер шрифта TMP в мире
    public Color textColor = Color.white;

    Transform root;            // контейнер
    SpriteRenderer bg, fill;
    TextMeshPro tmp;

    float target01 = 1f, visUntil;
    static Sprite whiteSprite;

    void Awake()
    {
        if (!source) source = GetComponent<PlayerHealth>();
        if (!source) { enabled = false; return; }
        CreateVisuals();
        ComputeOffsetFromColliderIfZero();
        RefreshImmediate();
    }

    void CreateVisuals()
    {
        if (!whiteSprite) whiteSprite = MakeWhiteSprite();

        root = new GameObject("_HPBar").transform;
        root.SetParent(transform, false);
        root.localPosition = offset;

        bg = NewSR("BG", bgColor, size);
        fill = NewSR("Fill", fillColor, size);
        // заполнение от левого края: масштабируем по X и двигаем центр
        fill.drawMode = SpriteDrawMode.Simple;

        if (showText)
        {
            var go = new GameObject("HPText");
            go.transform.SetParent(root, false);
            tmp = go.AddComponent<TextMeshPro>(); // 3D-вариант, world-space
            tmp.text = "";
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = textSize;
            tmp.rectTransform.localPosition = new Vector3(0, 0.35f, 0);
            // сортировка поверх спрайтов
            tmp.sortingLayerID = SortingLayer.NameToID(sortingLayerName);
            tmp.sortingOrder   = sortingOrder + 1;
            var mr = tmp.GetComponent<MeshRenderer>();
            if (mr) { mr.sortingLayerID = SortingLayer.NameToID(sortingLayerName); mr.sortingOrder = sortingOrder + 1; }
        }

        SetVisible(false, true);
    }

    SpriteRenderer NewSR(string name, Color col, Vector2 sz)
    {
        var t = new GameObject(name).transform; t.SetParent(root, false);
        var sr = t.gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = whiteSprite;
        sr.color = col;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder;
        t.localScale = new Vector3(sz.x, sz.y, 1);
        return sr;
    }

    static Sprite MakeWhiteSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.name = "_hpbar_white"; tex.filterMode = FilterMode.Point; tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixel(0, 0, Color.white); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
    }

    void ComputeOffsetFromColliderIfZero()
    {
        if (offset != Vector3.zero) return;
        var col = GetComponent<Collider2D>();
        if (col)
        {
            var b = col.bounds; // мировые координаты
            offset = new Vector3(0, b.extents.y + 0.3f, 0);
            root.localPosition = offset;
        }
    }

    void Update()
    {
        if (!source) return;
        float cur01 = source.maxHP > 0 ? (float)source.currentHP / source.maxHP : 0f;
        target01 = Mathf.Clamp01(cur01);

        // сгладить ширину заливки, левый край фиксирован
        float curScaleX = fill.transform.localScale.x;
        float wantScaleX = size.x * target01;
        float sx = Mathf.Lerp(curScaleX, wantScaleX, Time.deltaTime * smooth);
        fill.transform.localScale = new Vector3(Mathf.Max(0.0001f, sx), size.y, 1f);
        float dx = 0.5f * (sx - size.x); // отрицательно при уменьшении
        fill.transform.localPosition = new Vector3(dx, 0f, 0f);

        if (tmp) tmp.text = source.currentHP + "/" + source.maxHP;

        bool shouldShow = source.IsAlive && source.currentHP < source.maxHP && source.currentHP > 0;
        if (shouldShow) visUntil = Time.time + hideDelay;
        SetVisible(shouldShow || Time.time < visUntil, false);

        // если offset авто — держать над коллайдером
        if (root && offset == Vector3.zero)
        {
            var col = GetComponent<Collider2D>();
            if (col) { var b = col.bounds; root.position = new Vector3(transform.position.x, b.max.y + 0.3f, 0f); }
        }
    }

    void RefreshImmediate()
    {
        if (!fill) return;
        float ratio = source.maxHP > 0 ? (float)source.currentHP / source.maxHP : 0f;
        float sx = size.x * Mathf.Clamp01(ratio);
        fill.transform.localScale = new Vector3(Mathf.Max(0.0001f, sx), size.y, 1f);
        fill.transform.localPosition = new Vector3(0.5f * (sx - size.x), 0f, 0f);
        root.localPosition = offset;
    }

    void SetVisible(bool v, bool force)
    {
        if (!root) return;
        if (force || (bg.enabled != v))
        {
            bg.enabled = v; fill.enabled = v;
            if (tmp) tmp.enabled = v;
        }
    }
}
