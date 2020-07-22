using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.ProceduralTerrain.Core
{
    [CreateAssetMenu(menuName = "Terrain/HeightMapSettings")]
    public class HeightMapSettings : ScriptableObject
    {
        public Vector2 ElevationBoundary = new Vector2(128, 256);
        public HeightMapLayerSettings[] LayersSettings;

        [HideInInspector]
        public bool IsChanged = false;
        public void OnValidate() => IsChanged = true;
    }

    [System.Serializable]
    public class HeightMapLayerSettings
    {
        public bool Enabled;
        public HeightMapOption UsedFunction;
        public SumOption UsedAs;
        [Range(0, 1)]
        public float LayerScale = 1;
        [Range(0, 1 - .001f)]
        public float Clamping = 0;


        [ConditionalHide("UsedFunction", 0)]
        public FlatFunctionSettings FlatFunctionSettings;
        [ConditionalHide("UsedFunction", 1)]
        public RegularTiltFunctionSettings RegularTiltFunctionSettings;
        [ConditionalHide("UsedFunction", 2)]
        public SinFunctionSettings SinFunctionSettings;
        [ConditionalHide("UsedFunction", 3)]
        public PerlinFunctionSettings PerlinFunctionSettings;
        [ConditionalHide("UsedFunction", 4)]
        public LayeredNoiseFunctionSettings LayeredNoiseFunctionSettings;

        public enum HeightMapOption
        {
            Flat,
            Tilt,
            Sin,
            Perlin,
            LayeredNoise,
        }

        public enum SumOption
        {
            Additive,
            Multiplicative,
            Overlap,
        }
    }
}