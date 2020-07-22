using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.ProceduralTerrain.Core
{
    public class HeightMapGenerator
    {
        public HeightMapSettings Settings;
        IHeightMapFunction[] _heightFunctions;

        public HeightMapGenerator(HeightMapSettings Settings)
        {
            ChangeSettings(Settings);
        }

        public void UpdateSettings()
        {
            _heightFunctions = new IHeightMapFunction[Settings.LayersSettings.Length];
            for (int i = 0; i < Settings.LayersSettings.Length; i++)
            {
                _heightFunctions[i] = HeightFunctionsFactory.CreateFunction(Settings.LayersSettings[i]);
            }
        }

        public void ChangeSettings(HeightMapSettings Settings)
        {
            this.Settings = Settings;

            UpdateSettings();
        }

        public HeightMap CreateHeightMap(Vector2Int chunkCoord)
        {
            float[,] heightMap = new float[ChunkSize.Width + 2, ChunkSize.Width + 2];
            float min = float.MaxValue, max = float.MinValue;
            List<int> additives = new List<int>();
            List<int> multiplicatives = new List<int>();
            List<int> overlaps = new List<int>();
            
            // store function ids
            for (int funcId = 0; funcId < _heightFunctions.Length; funcId++ )
            {
                if (Settings.LayersSettings[funcId].Enabled)
                {
                    switch (Settings.LayersSettings[funcId].UsedAs)
                    {
                        case HeightMapLayerSettings.SumOption.Additive: additives.Add(funcId); continue;
                        case HeightMapLayerSettings.SumOption.Multiplicative: multiplicatives.Add(funcId); continue;
                        case HeightMapLayerSettings.SumOption.Overlap: overlaps.Add(funcId); continue;
                    }
                }
            }

            Vector2Int origin = chunkCoord.ToChunkOrigin();
            for (int x = 0; x < ChunkSize.Width + 2; x++)
                for (int z = 0; z < ChunkSize.Width + 2; z++)
                {
                    float value = 0;
                    Vector2Int position = origin.Plus(x - 1, z - 1);

                    // normalized sum of all additives
                    float scaleSum = 0;
                    for (int i = 0; i < additives.Count; i++)
                    {
                        float layerScale = Settings.LayersSettings[additives[i]].LayerScale;
                        scaleSum += layerScale;
                        value += Evaluate(position, additives[i]) * layerScale;
                        //value = (value * i + Evaluate(position, additives[i]) * layerScale ) / (i + 1);                        
                    }
                    if (scaleSum > 1)
                        value /= scaleSum;

                    // sum is multiplied by each multiplicative
                    for (int i = 0; i < multiplicatives.Count; i++)
                    {
                        value *= Evaluate(position, multiplicatives[i]); // multiplicatives are not scaled down
                    }

                    // overlap layers are clamped on top
                    for (int i = 0; i < overlaps.Count; i++)
                    {
                        value = Mathf.Max(value, Evaluate(position, overlaps[i]) * Settings.LayersSettings[overlaps[i]].LayerScale);
                    }

                    // final value
                    float Scale = Settings.ElevationBoundary.y - Settings.ElevationBoundary.x;
                    value = value * Scale + Settings.ElevationBoundary.x;

                    if (min > value) min = value;
                    if (max < value) max = value;

                    heightMap[z, x] = value;
                }

            return new HeightMap(heightMap, min, max);
        }

        private float Evaluate (Vector2Int position, int id)
        {
            return Mathf.Max((_heightFunctions[id].Evaluate(position) - Settings.LayersSettings[id].Clamping), 0) / (1 - Settings.LayersSettings[id].Clamping);
        }
    }

    public struct HeightMap
    {
        public float this[int z, int x] => array[z + 1, x + 1];
        public float min, max;

        private float[,] array;

        public HeightMap (float[,] array, float min, float max)
        {
            this.array = array;
            this.min = min;
            this.max = max;
        }

        public static HeightMap Create()
        {
            return new HeightMap
            {
                array = new float[ChunkSize.Width, ChunkSize.Width]
            };
        }
    }
}