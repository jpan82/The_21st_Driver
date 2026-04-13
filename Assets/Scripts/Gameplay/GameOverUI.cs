using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace The21stDriver.Gameplay
{
    /// <summary>
    /// Builds a runtime game-over overlay (no prefab required).
    /// Watches Race_Controller.IsGameOver and shows a panel with a restart button.
    /// </summary>
    public class GameOverUI : MonoBehaviour
    {
        Race_Controller ctrl;
        GameObject panel;
        bool shown;

        void Start()
        {
            ctrl = FindObjectOfType<Race_Controller>();
            BuildUI();
            panel.SetActive(false);
        }

        void Update()
        {
            if (!shown && ctrl != null && ctrl.IsGameOver)
            {
                shown = true;
                panel.SetActive(true);
            }
        }

        void BuildUI()
        {
            // EventSystem (required for button clicks; create only if none exists)
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }

            // Canvas
            GameObject canvasGO = new GameObject("GameOverCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            canvasGO.AddComponent<GraphicRaycaster>();

            // Panel (semi-transparent black overlay)
            panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGO.transform, false);
            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // "GAME OVER" text
            GameObject textGO = new GameObject("GameOverText");
            textGO.transform.SetParent(panel.transform, false);
            Text label = textGO.AddComponent<Text>();
            label.text = "GAME OVER\nYou left the track!";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 72;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            RectTransform labelRect = textGO.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.2f, 0.55f);
            labelRect.anchorMax = new Vector2(0.8f, 0.8f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            // Restart button
            GameObject btnGO = new GameObject("RestartButton");
            btnGO.transform.SetParent(panel.transform, false);
            Image btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.6f, 0.2f, 1f);
            Button btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(OnRestartClicked);
            RectTransform btnRect = btnGO.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.35f, 0.35f);
            btnRect.anchorMax = new Vector2(0.65f, 0.47f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            // Button label
            GameObject btnTextGO = new GameObject("ButtonText");
            btnTextGO.transform.SetParent(btnGO.transform, false);
            Text btnLabel = btnTextGO.AddComponent<Text>();
            btnLabel.text = "Restart";
            btnLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnLabel.fontSize = 48;
            btnLabel.fontStyle = FontStyle.Bold;
            btnLabel.color = Color.white;
            btnLabel.alignment = TextAnchor.MiddleCenter;
            RectTransform btnTextRect = btnTextGO.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;
        }

        void OnRestartClicked()
        {
            if (ctrl != null) ctrl.RestartRace();
        }
    }
}
