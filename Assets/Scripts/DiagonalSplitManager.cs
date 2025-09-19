using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Диагональный сплит: camA и camB рендерят в RenderTexture, материал
/// "Unlit/DiagonalSplitCombine" склеивает их по наклонной границе,
/// проходящей через середину между игроками и перпендикулярной вектору A→B.
/// Выводится в RawImage на оверлей-канвасе. groupCam используется для фрейминга и вычисления UV.
/// </summary>
public class DiagonalSplitManager : MonoBehaviour
{
    [Header("Refs")]
    public Transform playerA;
    public Transform playerB;
    public Camera groupCam;   // основная ортографическая камера под обоих игроков
    public Camera camA;       // камера игрока A (любой тип, Base, targetTexture назначится)
    public Camera camB;       // камера игрока B

    [Header("Output (UI)")]
    public RawImage output;          // Fullscreen RawImage (Screen Space - Overlay)
    public Material combineMaterial; // материал с шейдером Unlit/DiagonalSplitCombine

    [Header("Logic")]
    public float splitDistance = 18f;
    public float mergeDistance = 14f;
    public float transitionTime = 0.2f;
    public float feather = 0.004f;   // ширина мягкой границы в UV (0..1)

    [Header("Group framing")]
    public float minOrthoSize = 6f;
    public float padding = 3f;
    public float centerSmooth = 8f;
    public float sizeSmooth = 8f;

    enum Mode { Merged, Split }
    Mode mode = Mode.Merged;
    bool transitioning;

    int _savedMask;
    CameraClearFlags _savedFlags;
    Color _savedBg;

    RenderTexture rtA, rtB;
    int curW, curH;

    [Header("Edge bias")]
    public float edgeBiasUV = 0.12f;   // 0.05–0.2 — норм
    public float camBiasSmooth = 10f;

    [Header("Split camera framing")]
    public float splitCenterSmooth = 12f;   // сглаживание позиции камер в split
    public float splitSizeSmooth  = 8f;     // сглаживание зума в split
    public float splitOrtho       = 8f;   // целевой orthographicSize в split

    public float splitOrthoA = 8f;
    public float splitOrthoB = 8f;
    public bool usePerCamSplitSize = false;

    void ToggleFollow(bool on){
        var a = camA.GetComponent<FollowCamera2D>();
        var b = camB.GetComponent<FollowCamera2D>();
        if (a) a.enabled = on;
        if (b) b.enabled = on;
    }

    void PlaceNearSeam(Camera cam, Transform target, Vector2 seamUV, Vector2 seamN, float sideSign, float edgeBiasUV, float smooth)
    {
        // целевая позиция игрока в UV около шва
        var uvt = seamUV + seamN * sideSign * Mathf.Clamp(edgeBiasUV, 0.02f, 0.45f);
        uvt.x = Mathf.Clamp01(uvt.x); uvt.y = Mathf.Clamp01(uvt.y);

        float s = cam.orthographicSize;
        float a = Mathf.Max(0.0001f, cam.aspect);

        float px = target.position.x - (uvt.x - 0.5f) * 2f * s * a;
        float py = target.position.y - (uvt.y - 0.5f) * 2f * s;

        var p = cam.transform.position;
        p.x = Mathf.Lerp(p.x, px, Time.deltaTime * smooth);
        p.y = Mathf.Lerp(p.y, py, Time.deltaTime * smooth);
        cam.transform.position = new Vector3(p.x, p.y, -10f);
    }



    void Start()
    {
        EnsureRT();
        SetMergedInstant();
    }

    void OnDestroy(){ ReleaseRT(); }

    void EnsureRT()
    {
        int w = Mathf.Max(1, Screen.width);
        int h = Mathf.Max(1, Screen.height);
        if (rtA != null && w == curW && h == curH) return;
        ReleaseRT();
        curW = w; curH = h;
        rtA = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32){ name = "Split_RT_A", useMipMap = false, autoGenerateMips = false };
        rtB = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32){ name = "Split_RT_B", useMipMap = false, autoGenerateMips = false };
        camA.targetTexture = rtA;
        camB.targetTexture = rtB;
        if (combineMaterial){ combineMaterial.SetTexture("_TexA", rtA); combineMaterial.SetTexture("_TexB", rtB); }
        if (output) output.texture = rtA; // любое, чтобы RawImage рисовал
    }

    void UpdateSplitZoom(Camera cam, bool isA)
    {
        float target = usePerCamSplitSize ? (isA ? splitOrthoA : splitOrthoB) : splitOrtho;
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, target, Time.deltaTime * splitSizeSmooth);
    }

    void ReleaseRT()
    {
        if (rtA){ camA.targetTexture = null; rtA.Release(); Destroy(rtA); rtA = null; }
        if (rtB){ camB.targetTexture = null; rtB.Release(); Destroy(rtB); rtB = null; }
    }

    void LateUpdate()
    {
        if (!playerA || !playerB || !groupCam || !camA || !camB || !output || !combineMaterial) return;
        EnsureRT();

        float dist = Vector2.Distance(playerA.position, playerB.position);
        if (mode == Mode.Merged && dist > splitDistance && !transitioning) StartCoroutine(ToSplit());
        else if (mode == Mode.Split && dist < mergeDistance && !transitioning) StartCoroutine(ToMerged());

        UpdateGroupCamToFitBoth();

        if (mode == Mode.Split)
        {
            // Вьюпорт-координаты игроков и середины
            Vector2 va = groupCam.WorldToViewportPoint(playerA.position);
            Vector2 vb = groupCam.WorldToViewportPoint(playerB.position);
            Vector2 mid = 0.5f * (va + vb);
            Vector2 dir = (vb - va);
            float len = dir.magnitude;
            if (len < 1e-5f) dir = Vector2.right; else dir /= len; // нормаль к линии
            Vector2 n = dir; // нормаль к разделяющей полуплоскости

            // Передаём в материал
            // сместить камеры к шву с нужным сглаживанием
            PlaceNearSeam(camA, playerA, mid, n, -1f, edgeBiasUV, splitCenterSmooth);
            PlaceNearSeam(camB, playerB, mid, n, +1f, edgeBiasUV, splitCenterSmooth);

            // зум только в split
            UpdateSplitZoom(camA, true);
            UpdateSplitZoom(camB, false);

            combineMaterial.SetVector("_Point", new Vector4(mid.x, mid.y, 0, 0));
            combineMaterial.SetVector("_Normal", new Vector4(n.x, n.y, 0, 0));
            combineMaterial.SetFloat("_Feather", Mathf.Max(0.0001f, feather));
        }
    }

    // ===== режимы =====
    IEnumerator ToSplit()
    {
        transitioning = true; mode = Mode.Split;

        // включаем камеры в RT
        camA.enabled = true; 
        camB.enabled = true;

        camA.orthographicSize = usePerCamSplitSize ? splitOrthoA : splitOrtho;
        camB.orthographicSize = usePerCamSplitSize ? splitOrthoB : splitOrtho;


        // НЕ выключаем groupCam: оставляем активной, но пустой
        _savedMask = groupCam.cullingMask;
        _savedFlags = groupCam.clearFlags;
        _savedBg    = groupCam.backgroundColor;

        groupCam.enabled = true;
        groupCam.cullingMask = 0;                         // ничего не рисует, но «камера есть»
        groupCam.clearFlags  = CameraClearFlags.SolidColor;
        groupCam.backgroundColor = Color.black;

        // включаем вывод композитинга
        output.enabled = true;
        ToggleFollow(false);  
        yield return new WaitForSeconds(transitionTime);
        transitioning = false;
    }

    IEnumerator ToMerged()
    {
        transitioning = true; mode = Mode.Merged;

        // возвращаем полноценный рендер groupCam
        groupCam.enabled = true;
        groupCam.cullingMask = _savedMask;
        groupCam.clearFlags  = _savedFlags;
        groupCam.backgroundColor = _savedBg;
        
        yield return new WaitForSeconds(transitionTime);

        // отключаем сплит
        camA.enabled = false; 
        camB.enabled = false;
        output.enabled = false;

        ToggleFollow(true);

        transitioning = false;
    }

    void SetMergedInstant()
    {
        mode = Mode.Merged; transitioning = false;
        camA.enabled = false; camB.enabled = false; output.enabled = false;
        groupCam.enabled = true; groupCam.rect = new Rect(0,0,1,1);
        UpdateGroupCamToFitBoth();
    }

    // ===== фрейминг групповой камеры =====
    void UpdateGroupCamToFitBoth()
    {
        Vector3 mid = (playerA.position + playerB.position) * 0.5f;
        var p = groupCam.transform.position;
        p.x = Mathf.Lerp(p.x, mid.x, Time.deltaTime * centerSmooth);
        p.y = Mathf.Lerp(p.y, mid.y, Time.deltaTime * centerSmooth);
        groupCam.transform.position = new Vector3(p.x, p.y, -10f);

        float minX = Mathf.Min(playerA.position.x, playerB.position.x);
        float maxX = Mathf.Max(playerA.position.x, playerB.position.x);
        float minY = Mathf.Min(playerA.position.y, playerB.position.y);
        float maxY = Mathf.Max(playerA.position.y, playerB.position.y);
        float width = (maxX - minX) + padding * 2f;
        float height = (maxY - minY) + padding * 2f;
        float halfHeight = Mathf.Max(minOrthoSize, height * 0.5f);
        float halfWidth = (width * 0.5f) / Mathf.Max(0.0001f, groupCam.aspect);
        float target = Mathf.Max(halfHeight, halfWidth);
        groupCam.orthographicSize = Mathf.Lerp(groupCam.orthographicSize, target, Time.deltaTime * sizeSmooth);
    }
}
