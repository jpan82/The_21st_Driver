using UnityEngine;
using Unity.Sentis;
using System.IO;

namespace The21stDriver.Gameplay
{
    [System.Serializable]
    public class ScalerParams
    {
        public float[] mean;
        public float[] scale;
        public float[] imputer_statistics;
    }

    public class Model_Script: MonoBehaviour
    {
        [Header("AI Assets")]
        public ModelAsset onnxModel;
        public TextAsset scalerJson;

        [Header("Targeting")]
        public Transform playerCar;

        private Worker engine;
        private ScalerParams scaler;

        void Awake()
        {
            // 1. Load ONNX model
            if (onnxModel != null)
            {
                Model runtimeModel = ModelLoader.Load(onnxModel);
                engine = new Worker(runtimeModel, BackendType.GPUCompute);
            }

            // 2. Load scaler params
            if (scalerJson != null)
            {
                scaler = JsonUtility.FromJson<ScalerParams>(scalerJson.text);
                Debug.Log($"[AI] 成功加载预处理参数，维度: {scaler.mean.Length}");
            }
        }

        public int GetAIAction(float[] rawFeatures)
        {
            if (engine == null || scaler == null || rawFeatures.Length != 16)
                return 4;

            // 3. Impute then normalize into a local copy so rawFeatures is not mutated.
            float[] normalized = new float[16];
            for (int i = 0; i < 16; i++)
            {
                float v = rawFeatures[i];
                if (scaler.imputer_statistics != null && i < scaler.imputer_statistics.Length && float.IsNaN(v))
                    v = scaler.imputer_statistics[i];
                normalized[i] = (v - scaler.mean[i]) / scaler.scale[i];
            }

            // 4. Inference
            using var inputTensor = new Tensor<float>(new TensorShape(1, 16), normalized);
            engine.Schedule(inputTensor);
                        
            // 5. Get output
            // 这里的 TensorFloat 也改成了 Tensor<float>
            var outputTensor = engine.PeekOutput() as Tensor<float>;
						
            float[] logits = outputTensor.DownloadToArray();

            // 5. Argmax
            int bestAction = 4;
            float maxVal = float.NegativeInfinity;

            for (int i = 0; i < logits.Length; i++)
            {
                if (logits[i] > maxVal)
                {
                    maxVal = logits[i];
                    bestAction = i;
                }
            }

            return bestAction;
        }

        private void OnDestroy()
        {
            engine?.Dispose();
        }
    }
}