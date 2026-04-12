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

            // 3. Normalize
            for (int i = 0; i < rawFeatures.Length; i++)
            {
                rawFeatures[i] = (rawFeatures[i] - scaler.mean[i]) / scaler.scale[i];
            }

            // 4. Inference
            // 这里的 TensorFloat 改成了 Tensor<float>
            using var inputTensor = new Tensor<float>(new TensorShape(1, 16), rawFeatures);
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