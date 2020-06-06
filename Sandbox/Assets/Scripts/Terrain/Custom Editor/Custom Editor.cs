using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ProceduralTerrain))]
public class TerrainEditor : Editor
{
    ProceduralTerrain terrain;
    Editor blocksGeneratorEditor;
    Editor meshGeneratorEditor;

    public override void OnInspectorGUI ()
    {
        base.OnInspectorGUI();

        DrawSettingsEditor(terrain.blocksGeneratorSettings, ref terrain.blocksGeneratorSettingsFoldout, ref blocksGeneratorEditor);
        DrawSettingsEditor(terrain.meshGeneratorSettings, ref terrain.meshGeneratorSettingsFoldout, ref meshGeneratorEditor); 
    }       

    void DrawSettingsEditor (Object settings, ref bool foldout, ref Editor editor)
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

    private void OnEnable ()
    {
        terrain = (ProceduralTerrain)target;
    }
}
