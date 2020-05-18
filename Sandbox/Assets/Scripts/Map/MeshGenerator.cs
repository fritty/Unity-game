using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/* Handles mesh generation */
public class MeshGenerator : MonoBehaviour {

    const int threadGroupSize = 8;  
  
    [Header ("Mesh properties")]
    public Material mat;
    public bool generateColliders;

    public ComputeShader marchShader; // Shader for mesh generation

    MapGenerator mapGenerator; // Reference for chunks data

    // Buffers
    ComputeBuffer triangleBuffer; 
    ComputeBuffer pointsBuffer;
    ComputeBuffer triCountBuffer;
    ComputeBuffer edgeBuffer;

    bool[] toGenerate = {false,false,false}; // flags for generating edges


    void Awake () {
        if (Application.isPlaying) {
            mapGenerator = FindObjectOfType<MapGenerator>();
        }
    }


    /* Generate chunk mesh based on its blocks */
    public void UpdateChunkMesh (Chunk chunk) {
        CreateBuffers ();
        chunk.SetUpMesh(mat, generateColliders);
        
        // Current chunk mesh
        int kernelHandle = marchShader.FindKernel("March");
        
        CreateMesh(chunk, kernelHandle, false);

        // Edge mesh for adjacent chunks
        kernelHandle = marchShader.FindKernel("MarchEdge");
        Chunk edgeChunk;
        Vector3Int xEdge = new Vector3Int(1,0,0);
        Vector3Int zEdge = new Vector3Int(0,0,1);
        if (mapGenerator.existingChunks.TryGetValue(chunk.coord - zEdge, out edgeChunk))
        {
            if (!edgeChunk.generated[0])
            {
                CreateMesh(edgeChunk, kernelHandle, true);
            }
        }
        if (mapGenerator.existingChunks.TryGetValue(chunk.coord - xEdge, out edgeChunk))
        {
            if (!edgeChunk.generated[1])
            {
                CreateMesh(edgeChunk, kernelHandle, true);
            }
        }
        if (mapGenerator.existingChunks.TryGetValue(chunk.coord - xEdge - zEdge, out edgeChunk))
        {
            if (!edgeChunk.generated[2])
            {                
                CreateMesh(edgeChunk, kernelHandle, true);
            }
        }
    }

    void CreateMesh (Chunk chunk, int kernelHandle, bool edge) {
        pointsBuffer.SetData(chunk.blocks); // copy blocks data
        GenerateEdgeBuffer(chunk); // try to get edge points        
        DispatchMarchShader(kernelHandle, chunk.coord, edge); // compute mesh
        CopyMeshData (chunk);        
    }

    /* Add data from shader output to chunk mesh */
    void CopyMeshData (Chunk chunk)
    {  
        // Get number of triangles in the triangle buffer
        ComputeBuffer.CopyCount (triangleBuffer, triCountBuffer, 0);
        int[] triCountArray = { 0 };
        triCountBuffer.GetData (triCountArray);
        int numTris = triCountArray[0];

        if (numTris > 0)
        {
            Mesh mesh = new Mesh();

            // Get triangle data from shader
            Triangle[] tris = new Triangle[numTris];
            triangleBuffer.GetData (tris, 0, 0, numTris);

            var meshVertices = new Vector3[(numTris) * 3];
            var meshTriangles = new int[(numTris) * 3];

            CombineInstance[] combine = new CombineInstance[2];

            // add new mesh data
            for (int i = 0; i < numTris; i++) {
                for (int j = 0; j < 3; j++) {
                    meshTriangles[i * 3 + j] = i * 3 + j;
                    meshVertices[i * 3 + j] = tris[i][j];
                }
            } 

            mesh.vertices = meshVertices;
            mesh.triangles = meshTriangles;

            // combine old and new meshes
            combine[0].mesh = Mesh.Instantiate(chunk.mesh);
            combine[1].mesh = mesh;   

            chunk.mesh.CombineMeshes(combine, true, false, false);
            chunk.mesh.RecalculateNormals ();            

            chunk.generated[0] |= toGenerate[0];
            chunk.generated[1] |= toGenerate[1];
            chunk.generated[2] |= toGenerate[2];
            toGenerate[0] = toGenerate[1] = toGenerate[2] = false; // reset edge flag
        }
    }

    /* Execute mesh generation on GPU */
    void DispatchMarchShader (int kernelHandle, Vector3Int coord, bool edge)
    {
        int threadGroupsX;
        int threadGroupsY;
        int threadGroupsZ;        

        triangleBuffer.SetCounterValue (0);

        if (edge)
        {
            if (!toGenerate[0] && !toGenerate[1] && !toGenerate[2]) return;

            threadGroupsX = Mathf.CeilToInt ((Chunk.size.width + Chunk.size.width - 1) / (float) threadGroupSize);
            threadGroupsY = Mathf.CeilToInt ((Chunk.size.height) / (float) threadGroupSize);
            threadGroupsZ = 1;
        }            
        else
        {
            threadGroupsX = threadGroupsZ = Mathf.CeilToInt ((Chunk.size.width) / (float) threadGroupSize);        
            threadGroupsY = Mathf.CeilToInt ((Chunk.size.height) / (float) threadGroupSize);        
        }        

        marchShader.SetBuffer (kernelHandle, "points", pointsBuffer);
        marchShader.SetBuffer (kernelHandle, "edge", edgeBuffer);
        marchShader.SetBuffer (kernelHandle, "triangles", triangleBuffer);
        marchShader.SetInt ("Width", Chunk.size.width);
        marchShader.SetInt ("Height", Chunk.size.height);
        marchShader.SetVector ("Origin", OriginFromCoord(coord));
        marchShader.SetBool ("ZEdgeGenerated", toGenerate[0]);
        marchShader.SetBool ("XEdgeGenerated", toGenerate[1]);
        marchShader.SetBool ("CornerEdgeGenerated", toGenerate[2]);
            
        marchShader.Dispatch (kernelHandle, threadGroupsX, threadGroupsY, threadGroupsZ);
    }

    /* Copies edge data from adjacent chunks */
    void GenerateEdgeBuffer (Chunk chunk)
    {
        Vector3Int xEdge = new Vector3Int(1,0,0);
        Vector3Int zEdge = new Vector3Int(0,0,1);
        Chunk edgeChunk;
        byte[] edgeArray = new byte[(Chunk.size.width + Chunk.size.width + 1) * Chunk.size.height]; // x*y(zEdge) + z*y(xEdge) + y(Corner)

        if (!chunk.generated[0])
            if (mapGenerator.existingChunks.TryGetValue(chunk.coord + zEdge, out edgeChunk))
            {
                for (int j = 0; j < Chunk.size.height; j++)
                    for (int i = 0; i < Chunk.size.width; i++)
                    {
                        edgeArray[j*Chunk.size.width + i] = edgeChunk.blocks[0, j, i]; // x*y(zEdge)
                    }
                toGenerate[0] = true;
            }
        if (!chunk.generated[1])
            if (mapGenerator.existingChunks.TryGetValue(chunk.coord + xEdge, out edgeChunk))
            {
                for (int j = 0; j < Chunk.size.height; j++)
                    for (int i = 0; i < Chunk.size.width; i++)
                    {
                        edgeArray[Chunk.size.width*Chunk.size.height + j*Chunk.size.width + i] = edgeChunk.blocks[i, j, 0]; // z*y(xEdge)
                    }
                toGenerate[1] = true;    
            }        
        if (!chunk.generated[2])
            if (mapGenerator.existingChunks.TryGetValue(chunk.coord + xEdge + zEdge, out edgeChunk) && 
                mapGenerator.existingChunks.ContainsKey(chunk.coord + xEdge) &&
                mapGenerator.existingChunks.ContainsKey(chunk.coord + zEdge))
            {
                for (int i = 0; i < Chunk.size.height; i++)
                {
                    edgeArray[(Chunk.size.width + Chunk.size.width)*Chunk.size.height + i] = edgeChunk.blocks[0, i, 0]; // y
                }
                if (!toGenerate[0]) {
                    mapGenerator.existingChunks.TryGetValue(chunk.coord + zEdge, out edgeChunk);
                    for (int i = 0; i < Chunk.size.height; i++)
                    {
                        edgeArray[i*Chunk.size.width + Chunk.size.width-1] = edgeChunk.blocks[0, i, Chunk.size.width-1];
                    }
                }
                if (!toGenerate[1]) {
                    mapGenerator.existingChunks.TryGetValue(chunk.coord + xEdge, out edgeChunk);
                    for (int i = 0; i < Chunk.size.height; i++)
                    {
                        edgeArray[Chunk.size.width*Chunk.size.height + i*Chunk.size.width + Chunk.size.width-1] = edgeChunk.blocks[Chunk.size.width-1, i, 0];
                    }
                }
                toGenerate[2] = true; 
            }

        if (toGenerate[0] || toGenerate[1] || toGenerate[2]) edgeBuffer.SetData(edgeArray);
    }
   

    void OnDestroy () {
        if (Application.isPlaying) {
            ReleaseBuffers ();
        }
    }

    void CreateBuffers () {
        int numPoints = Chunk.size.width * Chunk.size.height * Chunk.size.width;

        // Always create buffers in editor (since buffers are released immediately to prevent memory leak)
        // Otherwise, only create if null or if size has changed
        if (!Application.isPlaying || (pointsBuffer == null || numPoints != pointsBuffer.count)) {
            if (Application.isPlaying) {
                ReleaseBuffers ();
            }
            int maxTriangleCount = (Chunk.size.width-1) * (Chunk.size.height-1) * (Chunk.size.width-1) * 5;
            triangleBuffer = new ComputeBuffer (maxTriangleCount, sizeof (float) * 3 * 3, ComputeBufferType.Append);
            pointsBuffer = new ComputeBuffer (numPoints/4, sizeof(byte)*4);
            triCountBuffer = new ComputeBuffer (1, sizeof (int), ComputeBufferType.Raw);
            edgeBuffer = new ComputeBuffer ((Chunk.size.width + Chunk.size.width + 1)*Chunk.size.height/4, sizeof(byte)*4);
        }
    }

    void ReleaseBuffers () {
        if (triangleBuffer != null) {
            triangleBuffer.Release();
            pointsBuffer.Release();
            triCountBuffer.Release();
            edgeBuffer.Release();
        }
    }

    Vector3 OriginFromCoord (Vector3Int coord) {
        return new Vector3 (coord.x * Chunk.size.width, coord.y * Chunk.size.height, coord.z * Chunk.size.width);
    }    

    struct Triangle {
#pragma warning disable 649 // disable unassigned variable warning
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;

        public Vector3 this [int i] {
            get {
                switch (i) {
                    case 0:
                        return a;
                    case 1:
                        return b;
                    default:
                        return c;
                }
            }
        }
    }  
}