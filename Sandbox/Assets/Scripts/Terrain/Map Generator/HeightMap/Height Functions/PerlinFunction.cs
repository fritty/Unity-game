using UnityEngine;

namespace Sandbox.ProceduralTerrain.Core
{
    [System.Serializable]
    public class PerlinFunction : IHeightMapFunction
    {
        public PerlinFunctionSettings Settings;

        public PerlinFunction(PerlinFunctionSettings settings)
        {
            Settings = settings;
        }

        public float Evaluate(Vector2Int position)
        {
            float xCoord = (position.x) * Settings.Frequency + Settings.Offset.x;
            float zCoord = (position.y) * Settings.Frequency + Settings.Offset.y;
            return Mathf.PerlinNoise(xCoord, zCoord);
        }
    }

    [System.Serializable]
    public class PerlinFunctionSettings
    {
        [Range(.005f, .1f)]
        public float Frequency = 0.025f;
        public Vector2 Offset = new Vector2(0, 0);
    }
}
