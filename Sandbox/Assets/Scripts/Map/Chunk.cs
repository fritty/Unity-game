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
    public byte[,,] blocks; 

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    MeshCollider meshCollider;
    bool generateCollider;
    bool hasMesh = false;
    

    public void DestroyOrDisable () {
        if (Application.isPlaying) {
            mesh.Clear ();
            gameObject.SetActive (false);
        } else {
            Destroy(gameObject);
        }
    }

    public void SetCoord (Vector3Int coord) {        
        this.coord = coord;
        this.name = $"Chunk ({coord.x}, {coord.y}, {coord.z})";   
        hasMesh = false;
    }

    public void SetBlocks (byte[,,] blocks) {
        if (this.blocks == null)
            this.blocks = new byte[size.width, size.height, size.width];
        this.blocks = blocks;
    }

    // Set render properties
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
        //mesh = meshFilter.sharedMesh;
        
        // if (mesh == null) {
        //     mesh = new Mesh ();
        //     mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        //     meshFilter.sharedMesh = mesh;
        // }
        // else {
        //     mesh.Clear();
        // }

        // mesh.vertices = meshData.vertices;
        // mesh.triangles = meshData.triangles;
        // mesh.RecalculateNormals ();
        // mesh.Optimize();
        //mesh = meshData.CreateMesh();

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

    // public void GenerateMap (Vector3Int coord){
    //     SetCoord(coord);

    //     if (blocks == null) {
    //         blocks = new byte[Chunk.size.width,Chunk.size.height,Chunk.size.width];
    //     }

    //     mapGenerator.RequestMapData(coord, OnMapDataReceived);
    // }

    // void OnMapDataReceived(MapData mapData) {
    //     blocks = mapData.blocks;
    // }

    // public void UpdateMesh (){     
    //     meshGenerator.RequestMeshData(OnMeshDataReceived);        
    // }

    // void OnMeshDataReceived(MeshData meshData) {
    //     meshFilter.mesh = meshData.CreateMesh ();
    // }
}