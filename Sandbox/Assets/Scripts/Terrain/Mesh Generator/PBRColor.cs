using UnityEngine;

namespace Sandbox.ProceduralTerrain.Core
{
    public class PBRColor
    {
        MeshGeneratorSettings _settings;
        Texture2D _colorTexture;
        const int _textureResolution = 128;

        public PBRColor(MeshGeneratorSettings settings)
        {
            _settings = settings;
            _colorTexture = new Texture2D(_textureResolution, 1);
        }

        public void UpdateElevation (Vector2 elevation)
        {
            if (!_settings.ColoredMaterial) return;

            _settings.Material.SetVector("ElevationBoundary", new Vector4(elevation.x, elevation.y));
        }

        public void UpdateColors()
        {
            if (!_settings.ColoredMaterial) return;

            Color[] colors = new Color[_textureResolution];
            for (int i = 0; i < _textureResolution; i++)
            {
                colors[i] = _settings.ColorGradient.Evaluate(i / (_textureResolution - 1f));
            }
            _colorTexture.SetPixels(colors);
            _colorTexture.Apply();
            _settings.Material.SetTexture("ColorTexture", _colorTexture);
        }
    }
}
