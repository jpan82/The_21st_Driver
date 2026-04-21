using UnityEngine;
using UnityEngine.UI;

namespace The21stDriver.Gameplay
{
    /// <summary>
    /// Modern speed meter displayed at the bottom-right corner.
    /// Layout (top → bottom):
    ///   ┌─────────────────────┐
    ///   │ ▌ cyan accent bar   │
    ///   │      237            │  ← large speed number
    ///   │      MPH            │  ← small unit label
    ///   │ ─────────────────── │  ← thin separator
    ///   │    SPEED            │  ← small footer label
    ///   └─────────────────────┘
    /// </summary>
    public class SpeedMeterUI : MonoBehaviour
    {
        static readonly Color AccentCyan   = new Color(0.18f, 0.90f, 0.95f, 1f);  // #2DE6F2
        static readonly Color PanelDark    = new Color(0.05f, 0.06f, 0.08f, 0.88f);
        static readonly Color SeparatorCol = new Color(1f,    1f,    1f,    0.10f);

        PlayerCarController playerCar;
        Text  speedNumber;
        float displayedSpeed;   // smoothed for readability

        void Start()
        {
            playerCar = FindObjectOfType<PlayerCarController>();
            BuildUI();
        }

        void Update()
        {
            if (playerCar == null || speedNumber == null) return;
            float targetMph = Mathf.Abs(playerCar.CurrentSpeedMs) * 2.23694f;
            // Smooth display so the number doesn't flicker every physics tick
            displayedSpeed = Mathf.Lerp(displayedSpeed, targetMph, Time.deltaTime * 8f);
            speedNumber.text = $"{displayedSpeed:0}";
        }

        void BuildUI()
        {
            // ── Canvas ──────────────────────────────────────────────────────────
            GameObject canvasGO = new GameObject("SpeedMeterCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 99;
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGO.AddComponent<GraphicRaycaster>();

            // ── Outer panel — bottom-right ───────────────────────────────────
            GameObject panel = new GameObject("SpeedPanel");
            panel.transform.SetParent(canvasGO.transform, false);
            panel.AddComponent<Image>().color = PanelDark;

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.80f, 0.02f);
            panelRect.anchorMax = new Vector2(0.97f, 0.22f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // ── Cyan accent bar — top edge ───────────────────────────────────
            MakeImage(panel, "AccentBar",
                anchorMin: new Vector2(0f, 0.88f), anchorMax: Vector2.one,
                color: AccentCyan);

            // ── Speed number (large) ─────────────────────────────────────────
            speedNumber = MakeText(panel, "SpeedNumber",
                anchorMin: new Vector2(0f, 0.38f), anchorMax: new Vector2(1f, 0.88f),
                text: "0", fontSize: 72, style: FontStyle.Bold,
                color: Color.white, align: TextAnchor.MiddleCenter,
                overflow: true);

            // ── "MPH" unit label ─────────────────────────────────────────────
            MakeText(panel, "UnitLabel",
                anchorMin: new Vector2(0f, 0.22f), anchorMax: new Vector2(1f, 0.42f),
                text: "MPH", fontSize: 28, style: FontStyle.Bold,
                color: AccentCyan, align: TextAnchor.MiddleCenter);

            // ── Thin separator ───────────────────────────────────────────────
            MakeImage(panel, "Separator",
                anchorMin: new Vector2(0.08f, 0.18f), anchorMax: new Vector2(0.92f, 0.20f),
                color: SeparatorCol);

            // ── "SPEED" footer label ─────────────────────────────────────────
            MakeText(panel, "FooterLabel",
                anchorMin: new Vector2(0f, 0.01f), anchorMax: new Vector2(1f, 0.18f),
                text: "SPEED", fontSize: 20, style: FontStyle.Normal,
                color: new Color(1f, 1f, 1f, 0.45f), align: TextAnchor.MiddleCenter);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        static Image MakeImage(GameObject parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            Image img = go.AddComponent<Image>();
            img.color = color;
            RectTransform r = go.GetComponent<RectTransform>();
            r.anchorMin  = anchorMin;
            r.anchorMax  = anchorMax;
            r.offsetMin  = Vector2.zero;
            r.offsetMax  = Vector2.zero;
            return img;
        }

        static Text MakeText(GameObject parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            string text, int fontSize, FontStyle style, Color color, TextAnchor align,
            bool overflow = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            Text t = go.AddComponent<Text>();
            t.text             = text;
            t.font             = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize         = fontSize;
            t.fontStyle        = style;
            t.color            = color;
            t.alignment        = align;
            t.horizontalOverflow = overflow ? HorizontalWrapMode.Overflow : HorizontalWrapMode.Wrap;
            t.verticalOverflow   = overflow ? VerticalWrapMode.Overflow   : VerticalWrapMode.Truncate;
            RectTransform r = go.GetComponent<RectTransform>();
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            return t;
        }
    }
}
