using UnityEngine;

namespace Sandbox.ProceduralTerrain.Core
{
    [System.Serializable]
    public class LayeredNoiseFunction : IHeightMapFunction
    {
        public LayeredNoiseFunctionSettings Settings;

        private SimplexNoise _noise = new SimplexNoise();

        public LayeredNoiseFunction(LayeredNoiseFunctionSettings settings)
        {
            Settings = settings;
        }

        public float Evaluate(Vector2Int position)
        {
            Vector3 point = new Vector3(position.x, position.y);
            float value = 0;
            float frequency = Settings.Frequency;
            float amplitude = 1;
            float maxAmplitude = 0;

            for (int i = 0; i < Settings.NumberOfLayers; i++)
            {
                value += (_noise.Evaluate(point * frequency) + 1) * .5f * amplitude;
                frequency /= (1 - Settings.Roughness);
                maxAmplitude += amplitude;
                amplitude *= Settings.Persistence;
            }

            if (Settings.NumberOfLayers > 0)
                return value / maxAmplitude;
            return 0;
        }
    }

    [System.Serializable]
    public class LayeredNoiseFunctionSettings
    {
        [Range(.001f, .25f)]
        public float Frequency = .025f;
        [Range(.1f, 1)]
        public float Persistence = .5f;        
        [Range(0, 1 - .001f)]
        public float Roughness = .5f;
        [Space]
        [Range(1, 10)]
        public int NumberOfLayers = 1;
    }
}