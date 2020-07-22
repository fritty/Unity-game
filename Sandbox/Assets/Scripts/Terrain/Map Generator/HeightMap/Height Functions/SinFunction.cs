using UnityEngine;

namespace Sandbox.ProceduralTerrain.Core
{
    [System.Serializable]
    public class SinFunction : IHeightMapFunction
    {
        public SinFunctionSettings Settings;

        public SinFunction(SinFunctionSettings settings)
        {
            Settings = settings;
        }

        public float Evaluate(Vector2Int position)
        {
            return Mathf.Pow(Mathf.Sin((position.x) * Settings.Frequency), 2);
        }
    }

    [System.Serializable]
    public class SinFunctionSettings
    {
        [Range(.005f, .1f)]
        public float Frequency = 0.025f;
    }
}
                                                        