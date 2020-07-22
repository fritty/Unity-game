using UnityEngine;

namespace Sandbox.ProceduralTerrain.Core
{
    [System.Serializable]
    public class FlatFunction : IHeightMapFunction
    {
        public FlatFunctionSettings Settings;

        public FlatFunction(FlatFunctionSettings settings)
        {
            Settings = settings;
        }

        public float Evaluate(Vector2Int position)
        {
            return Settings.Height;
        }
    }

    [System.Serializable]
    public class FlatFunctionSettings
    {
        [Range(0, 1)]
        public float Height;
    }
}
