using UnityEngine;

namespace Sandbox.ProceduralTerrain.Core
{
    [CreateAssetMenu(menuName = "Terrain/TerrainSettings")]
    public class TerrainSettings : ScriptableObject
    {
        [Header("General Settings")]
        [SerializeField]
        [Range(1, 16)]
        private int worldHeight = 8;
        [SerializeField]
        [Range(1, 32)]
        private int generationDistance = 8;
        [SerializeField]
        [Range(1, 32)]
        private int viewDistance = 8;

        [Header("Generation settings")]
        public MapGeneratorSettings MapGeneratorSettings;
        public MeshGeneratorSettings MeshGeneratorSettings;

        public int GenerationDistance => (generationDistance + 1); // edge chunks can't generate meshes
        public int ViewDistance => (viewDistance < GenerationDistance ? viewDistance : GenerationDistance);
        public int WorldHeight => worldHeight;

        [HideInInspector]
        public bool mapGeneratorSettingsFoldout;
        [HideInInspector]
        public bool meshGeneratorSettingsFoldout;

        PBRColor _shaderColor;

        public void UpdateColors()
        {
            if (!MeshGeneratorSettings.IsChanged) return;

            _shaderColor.UpdateColors();
            MeshGeneratorSettings.IsChanged = false;
        }

        private void OnEnable()
        {
            _shaderColor = new PBRColor(MeshGeneratorSettings);
            _shaderColor.UpdateElevation(MapGeneratorSettings.HeightMapSettings.ElevationBoundary);
            _shaderColor.UpdateColors();
        }
    }
}