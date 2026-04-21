using UnityEngine;
using UnityEngine.UI;

namespace The21stDriver.Gameplay
{
    /// <summary>
    /// F1-style start-lights sequence.
    ///
    /// Each light is a 4-layer lens stack (glow → housing → body → specular highlight)
    /// built with fixed-pixel RectTransforms so circles are always perfectly round.
    ///
    /// Timeline (mirrors Race_Controller 5 s freeze):
    ///   t = 0.8 s … 4.0 s  → lights 1-5 turn red one by one
    ///   t = 5.0 s           → all lights turn green simultaneously
    ///   t = 5.0 – 6.5 s    → panel fades out, then destroyed
    /// </summary>
    public class StartLightsUI : MonoBehaviour
    {
        // ── Timing ───────────────────────────────────────────────────────
        const float RACE_START   = 5f;
        const int   COUNT        = 5;
        const float INTERVAL     = 0.8f;
        const float FADE_SECS    = 1.5f;

        // ── Sizing (pixels at 1920×1080 reference) ────────────────────────
        const float PANEL_W      = 720f;
        const float PANEL_H      = 140f;
        const float GLOW_D       = 118f;   // outer glow bloom
        const float HOUSING_D    = 90f;    // dark metallic rim
        const float BODY_D       = 70f;    // main light disc
        const float SPEC_D       = 16f;    // specular highlight

        // ── Colours ───────────────────────────────────────────────────────
        static readonly Color PanelBg      = new Color(0.05f, 0.05f, 0.07f, 0.95f);
        static readonly Color AccentLine   = new Color(0.85f, 0.85f, 0.90f, 0.35f);
        static readonly Color Housing      = new Color(0.13f, 0.13f, 0.16f, 1.00f);
        static readonly Color RedBodyOff   = new Color(0.18f, 0.03f, 0.03f, 1.00f);
        static readonly Color RedBodyOn    = new Color(0.97f, 0.07f, 0.07f, 1.00f);
        static readonly Color RedGlow      = new Color(1.00f, 0.10f, 0.10f, 0.00f); // alpha set at runtime
        static readonly Color GreenBodyOn  = new Color(0.08f, 0.92f, 0.18f, 1.00f);
        static readonly Color GreenGlow    = new Color(0.10f, 1.00f, 0.25f, 0.00f);
        static readonly Color Specular     = new Color(1.00f, 1.00f, 1.00f, 0.55f);
        // ── State ─────────────────────────────────────────────────────────
        struct LightPod { public Image glow, body; }
        LightPod[] pods;
        Sprite     circle;
        GameObject panelGO;
        int        litCount;
        bool       raceStarted;
        bool       done;

        // ── Pulse animation for "light on" moment ─────────────────────────
        float[] pulseTimer = new float[COUNT];
        const float PULSE_SECS = 0.25f;

        // ─────────────────────────────────────────────────────────────────
        void Start()
        {
            circle = MakeCircleSprite(256);
            BuildUI();
        }

        void Update()
        {
            if (done) return;
            float t = Time.timeSinceLevelLoad;

            // Tick pulse timers
            for (int i = 0; i < COUNT; i++)
            {
                if (pulseTimer[i] > 0f)
                {
                    pulseTimer[i] -= Time.deltaTime;
                    float p = Mathf.Clamp01(pulseTimer[i] / PULSE_SECS);
                    float scale = 1f + 0.18f * p;
                    if (pods[i].glow != null)
                        pods[i].glow.rectTransform.localScale = Vector3.one * scale;
                }
            }

            // Light up red lights one at a time
            int shouldBeLit = Mathf.Clamp(Mathf.FloorToInt(t / INTERVAL), 0, COUNT);
            for (int i = litCount; i < shouldBeLit; i++) TriggerLight(i, LightState.Red);
            litCount = shouldBeLit;

            // Race start → all green
            if (!raceStarted && t >= RACE_START)
            {
                raceStarted = true;
                for (int i = 0; i < COUNT; i++) TriggerLight(i, LightState.Green);
            }

            // Fade everything out
            if (raceStarted)
            {
                float alpha = Mathf.Clamp01(1f - (t - RACE_START) / FADE_SECS);
                SetPanelAlpha(alpha);
                if (alpha <= 0f) { Destroy(gameObject); done = true; }
            }
        }

        // ── Light control ─────────────────────────────────────────────────
        enum LightState { Red, Green }

        void TriggerLight(int i, LightState state)
        {
            if (i < 0 || i >= COUNT) return;
            bool isGreen = state == LightState.Green;

            pods[i].body.color = isGreen ? GreenBodyOn  : RedBodyOn;

            Color glow = isGreen ? GreenGlow : RedGlow;
            glow.a = 0.45f;
            pods[i].glow.color = glow;

            pulseTimer[i] = PULSE_SECS;
        }

        void SetPanelAlpha(float a)
        {
            SetImageAlpha(panelGO.GetComponent<Image>(), a);
            for (int i = 0; i < COUNT; i++)
            {
                if (pods[i].glow != null)
                {
                    Color gc = pods[i].glow.color; gc.a = Mathf.Min(0.45f, a * 0.45f); pods[i].glow.color = gc;
                    SetImageAlpha(pods[i].body, a);
                }
            }
            // Dim housings & specular
            foreach (Transform child in panelGO.transform)
            {
                Image img = child.GetComponent<Image>();
                if (img != null) SetImageAlpha(img, a);
                foreach (Transform grandchild in child)
                {
                    Image img2 = grandchild.GetComponent<Image>();
                    if (img2 != null) SetImageAlpha(img2, a);
                }
            }
        }

        static void SetImageAlpha(Image img, float a)
        {
            if (img == null) return;
            Color c = img.color; c.a = Mathf.Min(c.a, a); img.color = c;
        }

        // ── UI construction ───────────────────────────────────────────────
        void BuildUI()
        {
            // Canvas
            GameObject canvasGO = new GameObject("StartLightsCanvas");
            canvasGO.transform.SetParent(transform);
            Canvas cv = canvasGO.AddComponent<Canvas>();
            cv.renderMode   = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 200;
            CanvasScaler cs = canvasGO.AddComponent<CanvasScaler>();
            cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            canvasGO.AddComponent<GraphicRaycaster>();

            // ── Panel: fraction anchors for position, offsetMin/Max for padding ──
            panelGO = new GameObject("LightsPanel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            panelGO.AddComponent<Image>().color = PanelBg;
            RectTransform pr = panelGO.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.28f, 0.87f);
            pr.anchorMax = new Vector2(0.72f, 0.99f);
            pr.offsetMin = Vector2.zero;
            pr.offsetMax = Vector2.zero;

            // Thin accent line along the bottom of the panel
            MakeRect(panelGO, "AccentLine", Vector2.zero, new Vector2(PANEL_W, 2f),
                new Vector2(0f, 0f), AccentLine);

            // ── 5 light pods ─────────────────────────────────────────────
            pods = new LightPod[COUNT];
            float spacing = PANEL_W / (COUNT + 1f);

            for (int i = 0; i < COUNT; i++)
            {
                float xPos = spacing * (i + 1) - PANEL_W * 0.5f; // centred in panel
                float yPos = 0f;

                // 1 – Glow bloom (behind everything)
                Image glow = MakeCircleImage(panelGO, $"Glow_{i}",
                    new Vector2(xPos, yPos), new Vector2(GLOW_D, GLOW_D),
                    new Color(RedGlow.r, RedGlow.g, RedGlow.b, 0f));

                // 2 – Housing rim
                MakeCircleImage(panelGO, $"Housing_{i}",
                    new Vector2(xPos, yPos), new Vector2(HOUSING_D, HOUSING_D), Housing);

                // 3 – Light body disc
                Image body = MakeCircleImage(panelGO, $"Body_{i}",
                    new Vector2(xPos, yPos), new Vector2(BODY_D, BODY_D), RedBodyOff);

                // 4 – Specular highlight (top-left offset for 3-D lens look)
                MakeCircleImage(panelGO, $"Spec_{i}",
                    new Vector2(xPos - 12f, yPos + 12f), new Vector2(SPEC_D, SPEC_D), Specular);

                pods[i] = new LightPod { glow = glow, body = body };
            }

        }

        // ── Helpers ──────────────────────────────────────────────────────

        Image MakeCircleImage(GameObject parent, string name,
            Vector2 centeredPos, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            Image img  = go.AddComponent<Image>();
            img.sprite = circle;
            img.type   = Image.Type.Simple;
            img.color  = color;
            RectTransform r = go.GetComponent<RectTransform>();
            r.anchorMin        = new Vector2(0.5f, 0.5f);
            r.anchorMax        = new Vector2(0.5f, 0.5f);
            r.pivot            = new Vector2(0.5f, 0.5f);
            r.sizeDelta        = size;           // fixed pixel size → perfect circle
            r.anchoredPosition = centeredPos;
            return img;
        }

        static void MakeRect(GameObject parent, string name,
            Vector2 anchoredPos, Vector2 size, Vector2 pivot, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<Image>().color = color;
            RectTransform r = go.GetComponent<RectTransform>();
            r.anchorMin        = new Vector2(0f, 0f);
            r.anchorMax        = new Vector2(0f, 0f);
            r.pivot            = pivot;
            r.sizeDelta        = size;
            r.anchoredPosition = anchoredPos;
        }

        /// <summary>
        /// Generates a crisp anti-aliased filled circle sprite.
        /// Using a power-of-two texture keeps GPU sampling efficient.
        /// </summary>
        static Sprite MakeCircleSprite(int size)
        {
            Texture2D tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float c = (size - 1) * 0.5f;
            float r = c - 1f;
            const float aa = 1.8f; // anti-alias softness in pixels

            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist  = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                byte  alpha = (byte)(Mathf.Clamp01((r - dist) / aa) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
