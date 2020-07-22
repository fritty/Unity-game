using UnityEngine;
using UnityEditor;
using Sandbox.ProceduralTerrain;
using Sandbox.ProceduralTerrain.Core;
using Sandbox.Editing;

[CustomEditor(typeof(TerrainSettings))]
public class TerrainSettingsEditor : Editor
{
    TerrainSettings Settings;
    Editor _mapGeneratorEditor;
    Editor _meshGeneratorEditor;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        DrawSettingsEditor(Settings.MapGeneratorSettings, ref Settings.mapGeneratorSettingsFoldout, ref _mapGeneratorEditor);
        DrawSettingsEditor(Settings.MeshGeneratorSettings, ref Settings.meshGeneratorSettingsFoldout, ref _meshGeneratorEditor);
    }

    void DrawSettingsEditor(Object settings, ref bool foldout, ref Editor editor)
    {
        if (settings != null)
        {
            foldout = EditorGUILayout.InspectorTitlebar(foldout, settings);
            if (foldout)
            {
                CreateCachedEditor(settings, null, ref editor);
                editor.OnInspectorGUI();
            }
        }
    }

    private void OnEnable()
    {
        Settings = (TerrainSettings)target;
    }
}

[CustomEditor(typeof(MapGeneratorSettings))]
public class MapGeneratorSettingsEditor : Editor
{
    MapGeneratorSettings Settings;
    Editor _editor;

    public override void OnInspectorGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Map Settings", EditorStyles.boldLabel);
        base.OnInspectorGUI();
        if (Settings.HeightMapSettings != null)
        {
            CreateCachedEditor(Settings.HeightMapSettings, null, ref _editor);
            _editor.OnInspectorGUI();
        }
        if (GUI.changed)
            Settings.OnValidate();
    }
    private void OnEnable()
    {
        Settings = (MapGeneratorSettings)target;
    }
}

[CustomEditor(typeof(HeightMapSettings))]
public class HeightMapSettingsEditor : Editor
{
    HeightMapSettings Settings;
    public override void OnInspectorGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("HeightMap Settings", EditorStyles.boldLabel);
        base.OnInspectorGUI();
        if (GUI.changed)
            Settings.OnValidate();
    }

    private void OnEnable()
    {
        Settings = (HeightMapSettings)target;
    }
}


[CustomEditor(typeof(ProTerra))]
public class TerrainEditor : Editor
{
    ProTerra Terrain;
    Editor _settingsEditor;

    public override void OnInspectorGUI()
    {
        DrawSettingsEditor();

        base.OnInspectorGUI();
    }

    void DrawSettingsEditor()
    {
        if (Terrain.Settings != null)
        {
            CreateCachedEditor(Terrain.Settings, null, ref _settingsEditor);
            _settingsEditor.OnInspectorGUI();
        }
    }

    private void OnEnable()
    {
        Terrain = (ProTerra)target;
    }
}

[CustomEditor(typeof(TerrainPreview))]
public class TerrainPreviewEditor : Editor
{
    TerrainPreview Terrain;
    Editor _settingsEditor;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (!Terrain.AutoUpdate_)
            if (GUILayout.Button("Update") && Application.isPlaying)
                Terrain.Redraw();
        DrawSettingsEditor();         
    }

    void DrawSettingsEditor()
    {
        if (Terrain.Settings_ != null)
        {
            CreateCachedEditor(Terrain.Settings_, null, ref _settingsEditor);
            _settingsEditor.OnInspectorGUI();
        }
    }

    private void OnEnable()
    {
        Terrain = (TerrainPreview)target;
    }
}