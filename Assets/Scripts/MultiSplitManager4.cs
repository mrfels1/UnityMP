using UnityEngine;
using UnityEngine.UI;   // RawImage, Image, Canvas, CanvasScaler
using System.Collections.Generic;

/// <summary>
/// MultiSplitManager4 — 1..4 игроков.
/// 1P → groupCam на весь экран.
/// 2P → диагональный сплит (два RT + материал), игроки «прижаты» к шву.
/// 3–4P → фиксированная сетка 2×2. В квадранте: живой — камера игрока; мёртвый — чёрная панель.
/// Панели создаются автоматически в верхнем Overlay-Canvas. Таймеры управляются вне этого скрипта.
/// KeepAlive-камера всегда включена, чтобы не было "Display 1 no cameras rendering".
/// </summary>
public class MultiSplitManager4 : MonoBehaviour
{
    [Header("Players (max 4)")]
    public Transform[] playerTransforms = new Transform[4];

    [Header("Cameras")]
    public Camera groupCam;                 // общая камера
    public Camera[] cams = new Camera[4];   // камеры игроков A..D (ортографические)

    [Header("UI (auto)")]
    public Canvas uiCanvas;                 // назначь вручную, чтобы использовать существующий Canvas
    public bool forceDedicatedCanvas = true; // если true и uiCanvas не задан — будет создан отдельный Canvas
    public string splitCanvasName = "SplitUI_Overlay";
    [Header("UI sorting")] public int splitUiSortingOrder = -100;
    public GameObject[] deadPanels = new GameObject[4];     // авто-создаваемые чёрные квадраты
    public Color deadColor = Color.black;

    [Header("2P Diagonal Output (optional)")]
    public RawImage output;                 // фуллскрин RawImage под композит 2P
    public Material combineMaterial;        // шейдер диагонали (URP или Built-in)

    [Header("Thresholds")]
    public float splitDistance = 18f;       // порог разделения для 2P
    public float mergeDistance = 14f;

    [Header("Framing (group)")]
    public float minOrthoSize = 6f;
    public float padding = 3f;
    public float centerSmooth = 8f;
    public float sizeSmooth = 8f;

    [Header("Split/Grid per-camera")]
    public float splitCenterSmooth = 12f;   // сглаживание позиции в Split/Grid
    public float splitSizeSmooth  = 8f;     // сглаживание зума в Split/Grid
    public float splitOrtho       = 8f;     // целевой orthographicSize

    [Header("2P diagonal seam")]
    public float feather = 0.004f;          // ширина мягкой границы в UV
    public float edgeBiasUV = 0.12f;        // смещение игрока к шву
    public float camBiasSmooth = 10f;

    [Header("Keep-alive camera")]
    public Camera keepAliveCam;             // всегда включена, cullingMask=0

    enum Mode { Merged, Split2Diag, Grid4 }
    Mode mode = Mode.Merged;

    RenderTexture rtA, rtB; int curW, curH;

    static readonly Rect[] QUADS = new Rect[] {
        new Rect(0f, 0.5f, 0.5f, 0.5f), // 0 TL
        new Rect(0.5f,0.5f, 0.5f, 0.5f), // 1 TR
        new Rect(0f, 0f,   0.5f, 0.5f), // 2 BL
        new Rect(0.5f,0f,  0.5f, 0.5f)  // 3 BR
    };

    void Start()
    {
        SetMergedInstant();
        EnsureKeepAliveCam();
        EnsureCanvas();
        EnsureDeadPanels();
        ReparentSplitUIToOurCanvas();
        EnsureOutputFullScreen();
    }

    void LateUpdate()
    {
        EnsureOutputFullScreen();
        int slots = AssignedSlots();
        if (slots == 0) return;

        // Режимы: ≥3 слота → Grid4; 2 слота → диагональ; 1 слот → Merged
        if (slots >= 3) SetMode(Mode.Grid4);
        else if (slots == 2)
        {
            var a = playerTransforms[FirstAssignedIndex()];
            var b = playerTransforms[SecondAssignedIndex()];
            float dist = Vector2.Distance(a.position, b.position);
            if (mode == Mode.Merged && dist > splitDistance) SetMode(Mode.Split2Diag);
            else if (mode == Mode.Split2Diag && dist < mergeDistance) SetMode(Mode.Merged);
        }
        else SetMode(Mode.Merged);

        if (mode == Mode.Merged)
        {
            UpdateGroupCamToFit(AllAlive());
            SetAllPanelsActive(false);
            if (output) output.enabled = false;
        }
        else if (mode == Mode.Split2Diag)
        {
            UpdateGroupCamToFit(AllAlive());
            TwoPlayerDiagonal();
            SetAllPanelsActive(false);
        }
        else // Grid4
        {
            GridLayout4();
        }
    }

    // =================== РЕЖИМЫ ===================
    void SetMode(Mode m)
    {
        if (mode == m) return;
        mode = m;
        if (m == Mode.Merged)
        {
            TeardownRT(); if (output) output.enabled = false;
            if (groupCam){ groupCam.enabled = true; groupCam.rect = new Rect(0,0,1,1); groupCam.cullingMask = ~0; }
            for (int i=0;i<4;i++) if (cams[i]) { cams[i].enabled=false; cams[i].rect=new Rect(0,0,1,1); cams[i].targetTexture=null; }
        }
        else if (m == Mode.Split2Diag)
        {
            EnsureRT(); if (output) output.enabled = true;
            if (groupCam){ groupCam.enabled = true; groupCam.cullingMask = 0; groupCam.clearFlags = CameraClearFlags.SolidColor; }
            for (int i=0;i<4;i++) if (cams[i]) { cams[i].enabled = (i<2); cams[i].rect = new Rect(0,0,1,1); cams[i].targetTexture = (i==0?rtA: i==1?rtB:null); }
        }
        else // Grid4
        {
            TeardownRT(); if (output) output.enabled = false;
            if (groupCam) groupCam.enabled = false; // не нужен при сетке 2×2
            EnsureDeadPanels();
        }
    }

    void SetMergedInstant()
    {
        mode = Mode.Merged;
        TeardownRT(); if (output) output.enabled = false;
        if (groupCam){ groupCam.enabled = true; groupCam.rect = new Rect(0,0,1,1); groupCam.cullingMask = ~0; }
        for (int i=0;i<4;i++) if (cams[i]) { cams[i].enabled=false; cams[i].rect=new Rect(0,0,1,1); cams[i].targetTexture=null; }
        SetAllPanelsActive(false);
    }

    // =================== 2P ДИАГОНАЛЬ ===================
    void TwoPlayerDiagonal()
    {
        int ia = FirstAssignedIndex();
        int ib = SecondAssignedIndex();
        if (ia<0 || ib<0 || combineMaterial==null || output==null || groupCam==null) return;
        var aT = playerTransforms[ia]; var bT = playerTransforms[ib];

        Vector2 va = groupCam.WorldToViewportPoint(aT.position);
        Vector2 vb = groupCam.WorldToViewportPoint(bT.position);
        Vector2 mid = 0.5f*(va+vb);
        Vector2 n = (vb-va); float L = n.magnitude; n = (L<1e-6f? Vector2.right : n/L);

        combineMaterial.SetVector("_Point", new Vector4(mid.x, mid.y, 0, 0));
        combineMaterial.SetVector("_Normal", new Vector4(n.x, n.y, 0, 0));
        combineMaterial.SetFloat("_Feather", Mathf.Max(0.0001f, feather));

        PlaceNearSeam(cams[ia], aT, mid, n, -1f);
        PlaceNearSeam(cams[ib], bT, mid, n, +1f);
        UpdateSplitZoom(cams[ia]);
        UpdateSplitZoom(cams[ib]);
    }

    void PlaceNearSeam(Camera cam, Transform t, Vector2 seamUV, Vector2 seamN, float sideSign)
    {
        if (!cam || !t) return;
        var uvt = seamUV + seamN * sideSign * Mathf.Clamp(edgeBiasUV, 0.02f, 0.45f);
        float s = cam.orthographicSize, a = Mathf.Max(0.0001f, cam.aspect);
        float px = t.position.x - (uvt.x - 0.5f) * 2f * s * a;
        float py = t.position.y - (uvt.y - 0.5f) * 2f * s;
        var p = cam.transform.position;
        p.x = Mathf.Lerp(p.x, px, Time.deltaTime * camBiasSmooth);
        p.y = Mathf.Lerp(p.y, py, Time.deltaTime * camBiasSmooth);
        cam.transform.position = new Vector3(p.x, p.y, -10f);
    }

    void UpdateSplitZoom(Camera cam)
    {
        if (!cam) return;
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, splitOrtho, Time.deltaTime * splitSizeSmooth);
    }

    // =================== 3–4P СЕТКА ===================
    void GridLayout4()
    {
        EnsureDeadPanels();
        for (int i=0;i<4;i++)
        {
            var t = playerTransforms[i];
            Rect r = QUADS[i];
            bool alive = IsAlive(i);

            if (alive)
            {
                if (cams[i])
                {
                    cams[i].enabled = true; cams[i].rect = r; cams[i].targetTexture = null;
                    Follow(cams[i], t);
                    UpdateSplitZoom(cams[i]);
                }
                if (i < deadPanels.Length && deadPanels[i]) deadPanels[i].SetActive(false);
            }
            else
            {
                if (cams[i]) cams[i].enabled = false;
                if (i < deadPanels.Length && deadPanels[i])
                {
                    var rt = deadPanels[i].GetComponent<RectTransform>();
                    ApplyPanelToRect(rt, r);
                    deadPanels[i].SetActive(true);
                    deadPanels[i].transform.SetAsLastSibling(); // панель поверх прочего UI
                }
            }
        }
    }

    void Follow(Camera cam, Transform t)
    {
        if (!cam || !t) return;
        var p = cam.transform.position;
        p.x = Mathf.Lerp(p.x, t.position.x, Time.deltaTime * splitCenterSmooth);
        p.y = Mathf.Lerp(p.y, t.position.y, Time.deltaTime * splitCenterSmooth);
        cam.transform.position = new Vector3(p.x, p.y, -10f);
    }

    // =================== ГРУППОВАЯ КАМЕРА ===================
    void UpdateGroupCamToFit(List<Transform> list)
    {
        if (!groupCam || list==null || list.Count==0) return;
        Vector3 mid = Vector3.zero; foreach (var t in list) mid += t.position; mid /= list.Count;
        var pos = groupCam.transform.position;
        pos.x = Mathf.Lerp(pos.x, mid.x, Time.deltaTime * centerSmooth);
        pos.y = Mathf.Lerp(pos.y, mid.y, Time.deltaTime * centerSmooth);
        groupCam.transform.position = new Vector3(pos.x, pos.y, -10f);
        float minX=float.PositiveInfinity, maxX=float.NegativeInfinity, minY=float.PositiveInfinity, maxY=float.NegativeInfinity;
        foreach (var t in list){ var p=t.position; if(p.x<minX)minX=p.x; if(p.x>maxX)maxX=p.x; if(p.y<minY)minY=p.y; if(p.y>maxY)maxY=p.y; }
        float width = (maxX-minX)+padding*2f, height=(maxY-minY)+padding*2f;
        float halfH = Mathf.Max(minOrthoSize, height*0.5f);
        float halfW = (width*0.5f)/Mathf.Max(0.0001f, groupCam.aspect);
        float target = Mathf.Max(halfH, halfW);
        groupCam.orthographicSize = Mathf.Lerp(groupCam.orthographicSize, target, Time.deltaTime * sizeSmooth);
    }

    // =================== ВСПОМОГАТЕЛЬНЫЕ ===================
    int AssignedSlots(){ int c=0; for(int i=0;i<4;i++) if(playerTransforms[i]) c++; return c; }
    int FirstAssignedIndex(){ for(int i=0;i<4;i++) if(playerTransforms[i]) return i; return -1; }
    int SecondAssignedIndex(){ int seen=0; for(int i=0;i<4;i++){ if(playerTransforms[i]){ if(seen==0){seen=1; continue;} return i; } } return -1; }

    bool IsAlive(int i)
    {
        var t = playerTransforms[i]; if (!t) return false;
        var ph = t.GetComponent<PlayerHealth>();
        return ph == null || ph.IsAlive;
    }

    List<Transform> AllAlive()
    {
        var list = new List<Transform>(4);
        for (int i=0;i<4;i++) if (IsAlive(i) && playerTransforms[i]) list.Add(playerTransforms[i]);
        return list;
    }

    void EnsureRT()
    {
        if (!output || !combineMaterial) return;
        int w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
        if (rtA!=null && w==curW && h==curH) return;
        TeardownRT(); curW=w; curH=h;
        rtA = new RenderTexture(w,h,0,RenderTextureFormat.ARGB32){ name="Split_RT_A" };
        rtB = new RenderTexture(w,h,0,RenderTextureFormat.ARGB32){ name="Split_RT_B" };
        if (cams[0]) cams[0].targetTexture = rtA;
        if (cams[1]) cams[1].targetTexture = rtB;
        combineMaterial.SetTexture("_TexA", rtA);
        combineMaterial.SetTexture("_TexB", rtB);
        output.texture = rtA; output.enabled = true;
    }

    void TeardownRT()
    {
        if (cams!=null){ if (cams[0]) cams[0].targetTexture=null; if (cams[1]) cams[1].targetTexture=null; }
        if (rtA){ rtA.Release(); Destroy(rtA); rtA=null; }
        if (rtB){ rtB.Release(); Destroy(rtB); rtB=null; }
    }

    void EnsureKeepAliveCam()
    {
        if (keepAliveCam) { keepAliveCam.enabled = true; keepAliveCam.cullingMask = 0; return; }
        var go = new GameObject("KeepAliveCamera");
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.cullingMask = 0;                         // ничего не рисует, но «камера есть»
        cam.clearFlags  = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.depth = -1000;
        keepAliveCam = cam;
    }

    void EnsureDeadPanels()
    {
        var c = EnsureCanvas(); if (!c) return;
        if (deadPanels == null || deadPanels.Length < 4) deadPanels = new GameObject[4];
        for (int i=0;i<4;i++)
        {
            if (deadPanels[i]) continue;
            var go = new GameObject($"DeadPanel_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(c.transform, false);
            var img = go.GetComponent<Image>();
            img.color = deadColor; img.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            ApplyPanelToRect(rt, QUADS[i]);
            go.SetActive(false);
            deadPanels[i] = go;
        }
    }

    void ReparentSplitUIToOurCanvas()
    {
        if (!uiCanvas) return;
        var parent = uiCanvas.transform;
        for (int i=0;i<deadPanels.Length;i++) if (deadPanels[i]) deadPanels[i].transform.SetParent(parent, false);
    }

    Canvas EnsureCanvas()
    {
        if (uiCanvas) { SetupCanvas(uiCanvas); return uiCanvas; }

        if (forceDedicatedCanvas || FindObjectOfType<Canvas>() == null)
        {
            var go = new GameObject(splitCanvasName, typeof(Canvas), typeof(CanvasScaler));
            uiCanvas = go.GetComponent<Canvas>();
        }
        else
        {
            uiCanvas = FindObjectOfType<Canvas>();
        }
        SetupCanvas(uiCanvas);
        return uiCanvas;
    }

    void SetupCanvas(Canvas c)
    {
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.overrideSorting = true;
        c.sortingOrder   = splitUiSortingOrder; // ниже HUD, если HUD.order >= 0
        var scaler = c.GetComponent<CanvasScaler>();
        if (!scaler) scaler = c.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080);
        scaler.matchWidthOrHeight = 0.5f;
    }

    void ApplyPanelToRect(RectTransform rt, Rect r)
    {
        rt.anchorMin = new Vector2(r.xMin, r.yMin);
        rt.anchorMax = new Vector2(r.xMax, r.yMax);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0.5f);
    }

    void SetAllPanelsActive(bool on)
    {
        for (int i=0;i<deadPanels.Length;i++) if (deadPanels[i]) deadPanels[i].SetActive(on);
    }

    void EnsureOutputFullScreen()
    {
        if (!output) return;
        var rt = output.rectTransform;
        if (rt.anchorMin != Vector2.zero || rt.anchorMax != Vector2.one || rt.offsetMin != Vector2.zero || rt.offsetMax != Vector2.zero)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; rt.pivot = new Vector2(0.5f,0.5f);
        }
        output.raycastTarget = false;
        if (combineMaterial && output.material != combineMaterial) output.material = combineMaterial;
    }
}
