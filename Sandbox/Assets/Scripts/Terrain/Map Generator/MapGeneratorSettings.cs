using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.ProceduralTerrain.Core
{
    [CreateAssetMenu(menuName = "Terrain/MapGeneratorSettings")]
    public class MapGeneratorSettings : ScriptableObject
    {
        public HeightMapSettings HeightMapSettings;

        [HideInInspector]
        public bool IsChanged;
        public void OnValidate() => IsChanged = true;        
    }
}