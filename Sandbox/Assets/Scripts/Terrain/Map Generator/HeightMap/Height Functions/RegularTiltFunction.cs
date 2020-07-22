using UnityEngine;

namespace Sandbox.ProceduralTerrain.Core
{
    [System.Serializable]
    public class RegularTiltFunction : IHeightMapFunction
    {
        public RegularTiltFunctionSettings Settings;

        public RegularTiltFunction(RegularTiltFunctionSettings settings)
        {
            Settings = settings;
        }

        public float Evaluate(Vector2Int position)
        {
            return (Settings.TiltX * position.ToChunkPosition().x + Settings.TiltZ * position.ToChunkPosition().y) / ChunkSize.Width;
        }
    }

    [System.Serializable]
    public class RegularTiltFunctionSettings
    {
        [Range(0, 1)]
        public float TiltX;
        [Range(0, 1)]
        public float TiltZ;
    }
}
