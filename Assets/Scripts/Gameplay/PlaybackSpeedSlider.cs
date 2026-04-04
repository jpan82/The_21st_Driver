using UnityEngine;
using UnityEngine.UI;

namespace The21stDriver.Gameplay
{
    /// <summary>
    /// Binds a UI Slider to <see cref="Race_Controller.speedMultiplier"/>. Add a Canvas + Slider, assign references.
    /// </summary>
    public class PlaybackSpeedSlider : MonoBehaviour
    {
        [SerializeField] private Race_Controller raceController;
        [SerializeField] private Slider slider;

        [Header("Range")]
        [SerializeField] private float minSpeed = 0.1f;
        [SerializeField] private float maxSpeed = 5f;

        private void Awake()
        {
            if (raceController == null)
            {
                raceController = FindFirstObjectByType<Race_Controller>();
            }
        }

        private void Start()
        {
            if (slider == null)
            {
                return;
            }

            slider.minValue = minSpeed;
            slider.maxValue = maxSpeed;
            slider.wholeNumbers = false;

            float initial = raceController != null ? raceController.speedMultiplier : 1f;
            initial = Mathf.Clamp(initial, minSpeed, maxSpeed);
            slider.SetValueWithoutNotify(initial);
            if (raceController != null)
            {
                raceController.speedMultiplier = initial;
            }

            slider.onValueChanged.AddListener(OnSpeedChanged);
        }

        private void OnDestroy()
        {
            if (slider != null)
            {
                slider.onValueChanged.RemoveListener(OnSpeedChanged);
            }
        }

        private void OnSpeedChanged(float value)
        {
            if (raceController != null)
            {
                raceController.speedMultiplier = value;
            }
        }
    }
}
