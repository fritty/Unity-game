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
    public byte[,,] blocks; 

    Mesh mesh;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    MeshCollider meshCollider;
    bool generateCollider;
    bool hasMesh = false;
    

    public void SetCoord (Vector3Int coord) {        
        this.coord = coord;
        this.name = $"Chunk ({coord.x}, {coord.y}, {coord.z})";
        if (hasMesh)
            mesh.Clear(); 
        hasMesh = false;
        transform.position = OriginFromCoord(coord);
    }

    Vector3 OriginFromCoord (Vector3Int coord) {
        return new Vector3 (coord.x * Chunk.size.width, coord.y * Chunk.size.height, coord.z * Chunk.size.width);
    }

    public void SetBlocks (byte[,,] blocks) {
        this.blocks = blocks;
    }

    public void SetUpMesh (MeshData meshData) {
        this.generateCollider = meshData.generateColliders;

        meshFilter = GetComponent<MeshFilter> ();
        meshRenderer = GetComponent<MeshRenderer> ();
        meshCollider = GetComponent<MeshCollider> ();

        if (meshFilter == null) {
            meshFilter = gameObject.AddComponent<MeshFilter> ();            
        }

        if (meshRenderer == null) {
            meshRenderer = gameObject.AddComponent<MeshRenderer> ();
            meshRenderer.material = meshData.mat;
        }

        if (meshCollider == null && generateCollider) {
            meshCollider = gameObject.AddComponent<MeshCollider> ();
        }
        if (meshCollider != null && !generateCollider) {
            DestroyImmediate (meshCollider);
        }

        mesh = meshFilter.sharedMesh = meshData.CreateMesh();
        
        mesh.Optimize();        

        hasMesh = true;

        if (generateCollider) {
            if (meshCollider.sharedMesh == null) {
                meshCollider.sharedMesh = mesh;
            }
            // force update
            meshCollider.enabled = false;
            meshCollider.enabled = true;
        }        
    }
    
    public bool HasMesh () {
        return hasMesh;
    }    

    public void DestroyOrDisable () {
        if (Application.isPlaying) {
            mesh.Clear ();
            gameObject.SetActive (false);
        } else {
            Destroy(gameObject);
        }
    }
}