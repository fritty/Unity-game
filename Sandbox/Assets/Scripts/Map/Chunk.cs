using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour {
    public struct size
    {
        static public int width = 32;
        static public int height = 32;
    }
    
    [HideInInspector]
    public Vector3Int coord;
    [HideInInspector]
    public Mesh mesh;
    [HideInInspector]
    public bool[] generated = {false, false, false}; // flag for generated mesh edges

    public byte[,,] blocks; 

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    MeshCollider meshCollider;
    bool generateCollider;

    MeshGenerator meshGenerator;

    public void DestroyOrDisable () {
        if (Application.isPlaying) {
            mesh.Clear ();
            gameObject.SetActive (false);
        } else {
            Destroy(gameObject);
        }
    }

    // Set basic properties
    public void SetUp (Vector3Int coord) {        
        this.coord = coord;
        this.name = $"Chunk ({coord.x}, {coord.y}, {coord.z})";        

        if (blocks == null) {
            blocks = new byte[Chunk.size.width,Chunk.size.height,Chunk.size.width];
        }
        
        if (meshGenerator == null) {
            meshGenerator = FindObjectOfType<MeshGenerator>();
        }            
    }

    // Set render properties
    public void SetUpMesh (Material mat, bool generateCollider) {
        this.generateCollider = generateCollider;

        meshFilter = GetComponent<MeshFilter> ();
        meshRenderer = GetComponent<MeshRenderer> ();
        meshCollider = GetComponent<MeshCollider> ();

        if (meshFilter == null) {
            meshFilter = gameObject.AddComponent<MeshFilter> ();
        }

        if (meshRenderer == null) {
            meshRenderer = gameObject.AddComponent<MeshRenderer> ();
            meshRenderer.material = mat;
        }

        if (meshCollider == null && generateCollider) {
            meshCollider = gameObject.AddComponent<MeshCollider> ();
        }
        if (meshCollider != null && !generateCollider) {
            DestroyImmediate (meshCollider);
        }

        generated[0] = generated[1] = generated[2] = false;

        mesh = meshFilter.sharedMesh;
        if (mesh == null) {
            mesh = new Mesh ();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            meshFilter.sharedMesh = mesh;
        }
        else {
            mesh.Clear();
        }

        if (generateCollider) {
            if (meshCollider.sharedMesh == null) {
                meshCollider.sharedMesh = mesh;
            }
            // force update
            meshCollider.enabled = false;
            meshCollider.enabled = true;
        }        
    }

    public void UpdateMesh (){        
        if (meshGenerator != null)
            meshGenerator.UpdateChunkMesh(this);
        else
            Debug.Log("meshGenerator is not found");
    }
}