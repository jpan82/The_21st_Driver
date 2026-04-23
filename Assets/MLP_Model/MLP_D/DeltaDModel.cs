using UnityEngine;
using Unity.Sentis;

namespace The21stDriver.Gameplay
{
    public class DeltaDModel : MonoBehaviour
    {
        [Header("Regression Model Assets")]
        public ModelAsset onnxModel;
        public TextAsset scalerJson;

        private Worker engine;
        private ScalerParams scaler;

        void Awake()
        {
            if (onnxModel != null)
            {
                Model runtimeModel = ModelLoader.Load(onnxModel);
                engine = new Worker(runtimeModel, BackendType.GPUCompute);
            }

            if (scalerJson != null)
            {
                scaler = JsonUtility.FromJson<ScalerParams>(scalerJson.text);
                Debug.Log($"[DeltaD] Loaded regression scaler, dim: {scaler.mean.Length}");
            }
        }

        /// <summary>
        /// Returns predicted delta_d (continuous lateral offset), or 0 on failure.
        /// rawFeatures must be length 16 in the standard feature order.
        /// </summary>
        public float GetDeltaD(float[] rawFeatures)
        {
            if (engine == null || scaler == null || rawFeatures.Length != 16)
                return 0f;

            float[] normalized = new float[16];
            for (int i = 0; i < 16; i++)
            {
                float v = rawFeatures[i];
                if (scaler.imputer_statistics != null && i < scaler.imputer_statistics.Length && float.IsNaN(v))
                    v = scaler.imputer_statistics[i];
                normalized[i] = (v - scaler.mean[i]) / scaler.scale[i];
            }

            using var inputTensor = new Tensor<float>(new TensorShape(1, 16), normalized);
            engine.Schedule(inputTensor);

            var outputTensor = engine.PeekOutput() as Tensor<float>;
            float[] result = outputTensor.DownloadToArray();
            return result[0];
        }

        void OnDestroy()
        {
            engine?.Dispose();
        }
    }
}
