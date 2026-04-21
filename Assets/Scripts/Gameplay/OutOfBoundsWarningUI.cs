using UnityEngine;
using UnityEngine.UI;

namespace The21stDriver.Gameplay
{
    /// <summary>
    /// Shows a "WARNING: Off Track!" banner at the top-center of the screen
    /// while the player car is outside the track boundaries.
    /// Automatically hides when the player returns to the track.
    /// </summary>
    public class OutOfBoundsWarningUI : MonoBehaviour
    {
        PlayerCarController playerCar;
        GameObject warningPanel;

        void Start()
        {
            playerCar = FindObjectOfType<PlayerCarController>();
            BuildUI();
            warningPanel.SetActive(false);
        }

        void Update()
        {
            if (playerCar == null) return;
            bool shouldShow = playerCar.IsOutOfBounds;
            if (warningPanel.activeSelf != shouldShow)
                warningPanel.SetActive(shouldShow);
        }

        void BuildUI()
        {
            // Canvas
            GameObject canvasGO = new GameObject("OutOfBoundsCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 99;
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGO.AddComponent<GraphicRaycaster>();

            // Warning banner — anchored to the top-center
            warningPanel = new GameObject("OutOfBoundsWarning");
            warningPanel.transform.SetParent(canvasGO.transform, false);

            Image bg = warningPanel.AddComponent<Image>();
            bg.color = new Color(0.9f, 0.15f, 0.05f, 0.85f);

            RectTransform rect = warningPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.25f, 0.88f);
            rect.anchorMax = new Vector2(0.75f, 0.97f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Warning text
            GameObject textGO = new GameObject("WarningText");
            textGO.transform.SetParent(warningPanel.transform, false);
            Text label = textGO.AddComponent<Text>();
            label.text = "WARNING: Off Track!";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 52;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;

            RectTransform labelRect = textGO.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }
    }
}
