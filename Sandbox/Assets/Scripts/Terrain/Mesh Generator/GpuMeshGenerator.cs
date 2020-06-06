using System.Collections.Generic;
using UnityEngine;
using System;

public class GpuMeshGenerator : IMeshGenerator
{
    MeshGeneratorSettings Settings;

    Queue<Vector3Int> requestedCoords = new Queue<Vector3Int>();
    Action<GeneratedDataInfo<MeshData>> dataCallback;
    ProceduralTerrain terrain;

    ComputeBuffer triangleBuffer;
    ComputeBuffer pointsBuffer;
    ComputeBuffer triCountBuffer;
    ComputeBuffer edgeBuffer;  
    

    public GpuMeshGenerator(Action<GeneratedDataInfo<MeshData>> dataCallback, ProceduralTerrain terrain, MeshGeneratorSettings meshGeneratorSettings)
    {
        this.dataCallback = dataCallback;
        this.terrain = terrain;
        Settings = meshGeneratorSettings;
        Initialize();
    }

    public void Destroy()
    {
        ReleaseBuffers();
    }

    public void RequestData(Vector3Int coord)
    {
        requestedCoords.Enqueue(coord);
    }

    public void ManageRequests()
    {
        float dTime = Time.deltaTime;
        int count = 0; // number of chunks generated per frame
        bool repeat = true;

        while (repeat)
        {
            float shaderTime = Time.realtimeSinceStartup;
            bool generated = false;

            // Go through requested coordinates and generate if still relevant
            if (requestedCoords.Count > 0)
            {
                Vector3Int viewerCoord = new Vector3Int(Mathf.FloorToInt(terrain.viewer.position.x / Chunk.size.width), 0, Mathf.FloorToInt(terrain.viewer.position.z / Chunk.size.width));
                Vector3Int requestedCoord = requestedCoords.Dequeue();

                // skip outdated coordinates
                while ((Mathf.Abs(requestedCoord.x - viewerCoord.x) > terrain.viewDistance || Mathf.Abs(requestedCoord.z - viewerCoord.z) > terrain.viewDistance) && requestedCoords.Count > 0)
                {
                    requestedCoord = requestedCoords.Dequeue();
                }

                // generate
                if (Mathf.Abs(requestedCoord.x - viewerCoord.x) <= terrain.viewDistance && Mathf.Abs(requestedCoord.z - viewerCoord.z) <= terrain.viewDistance)
                {
                    Chunk chunk;
                    Chunk chunkX;
                    Chunk chunkZ;
                    Chunk chunkC;
                    if (terrain.existingChunks.TryGetValue(requestedCoord, out chunk) && terrain.existingChunks.TryGetValue(requestedCoord + Vector3Int.right, out chunkX) &&
                        terrain.existingChunks.TryGetValue(requestedCoord + new Vector3Int(0, 0, 1), out chunkZ) && terrain.existingChunks.TryGetValue(requestedCoord + Vector3Int.one - Vector3Int.up, out chunkC))
                    {
                        GenerateChunkMesh(chunk, chunkX, chunkZ, chunkC);

                        // Return requested data
                        dataCallback(new GeneratedDataInfo<MeshData>(CopyMeshData(), requestedCoord));

                        if (Settings.log)
                        { // log number of chunks generated per frame             
                            count++;
                            Debug.Log(count);
                        }
                        generated = true;
                    }
                }
            }

            if (!generated)
                repeat = false; // no more requests

            // estimate time required for generation and stop if it exceedes framerate
            shaderTime = Time.realtimeSinceStartup - shaderTime;
            dTime += shaderTime;
            if (dTime + shaderTime > 1 / Settings.targetFps)
                repeat = false; // no more time
        }
    }
        
    
    //////////////////////////////////    

    /* Generate chunk mesh based on its blocks */
    void GenerateChunkMesh(Chunk chunk, Chunk chunkX, Chunk chunkZ, Chunk chunkC)
    {

        int kernelHandle = Settings.marchShader.FindKernel("March");

        pointsBuffer.SetData(chunk.blocks); // copy blocks data
        GenerateEdgeBuffer(chunkX, chunkZ, chunkC); // get edge points        
        DispatchMarchShader(kernelHandle, chunk.coord); // compute mesh
    }

    /* Copy data from shader output */
    MeshData CopyMeshData()
    {
        // Get number of triangles in the triangle buffer
        ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
        int[] triCountArray = { 0 };
        triCountBuffer.GetData(triCountArray);
        int numTris = triCountArray[0];

        if (numTris > 0)
        {
            MeshData meshData = new MeshData(numTris);
            // Get triangle data from shader

            Triangle[] tris = new Triangle[numTris];
            triangleBuffer.GetData(tris, 0, 0, numTris);

            // add new mesh data
            for (int i = 0; i < numTris; i++)
            {
                meshData.AddTriangle(tris[i]);
            }

            return meshData;
        }
        return null;
    }

    /* Execute mesh generation on GPU */
    void DispatchMarchShader(int kernelHandle, Vector3Int coord)
    {
        uint threadGroupsX;
        uint threadGroupsY;
        uint threadGroupsZ;

        Settings.marchShader.GetKernelThreadGroupSizes(kernelHandle, out threadGroupsX, out threadGroupsY, out threadGroupsZ);

        triangleBuffer.SetCounterValue(0);

        threadGroupsX = (uint)Mathf.CeilToInt((Chunk.size.width) / (float)threadGroupsX);
        threadGroupsZ = (uint)Mathf.CeilToInt((Chunk.size.width) / (float)threadGroupsZ);
        threadGroupsY = (uint)Mathf.CeilToInt((Chunk.size.height) / (float)threadGroupsY);

        Settings.marchShader.SetBuffer(kernelHandle, "points", pointsBuffer);
        Settings.marchShader.SetBuffer(kernelHandle, "edge", edgeBuffer);
        Settings.marchShader.SetBuffer(kernelHandle, "triangles", triangleBuffer);
        Settings.marchShader.SetInt("Width", Chunk.size.width);
        Settings.marchShader.SetInt("Height", Chunk.size.height);
        //marchShader.SetVector ("Origin", OriginFromCoord(coord));

        Settings.marchShader.Dispatch(kernelHandle, (int)threadGroupsX, (int)threadGroupsY, (int)threadGroupsZ);
    }

    /* Copies edge data from adjacent chunks */
    void GenerateEdgeBuffer(Chunk chunkX, Chunk chunkZ, Chunk chunkC)
    {
        Vector3Int xEdge = new Vector3Int(1, 0, 0);
        Vector3Int zEdge = new Vector3Int(0, 0, 1);

        byte[] edgeArray = new byte[(Chunk.size.width + Chunk.size.width + 1) * Chunk.size.height]; // x*y(zEdge) + z*y(xEdge) + y(Corner)

        for (int j = 0; j < Chunk.size.height; j++)
            for (int i = 0; i < Chunk.size.width; i++)
            {
                edgeArray[Chunk.size.width * Chunk.size.height + j * Chunk.size.width + i] = chunkX.blocks[i, j, 0]; // z*y(xEdge)
                edgeArray[j * Chunk.size.width + i] = chunkZ.blocks[0, j, i]; // x*y(zEdge)
                edgeArray[(Chunk.size.width + Chunk.size.width) * Chunk.size.height + i] = chunkC.blocks[0, i, 0]; // y
            }

        edgeBuffer.SetData(edgeArray);
    }


    /////////////////////////////////

    void Initialize()
    {
        if (pointsBuffer == null)
        {
            int numBlocks = Chunk.size.width * Chunk.size.height * Chunk.size.width;
            int maxTriangleCount = (Chunk.size.width - 1) * (Chunk.size.height - 1) * (Chunk.size.width - 1) * 5;
            triangleBuffer = new ComputeBuffer(maxTriangleCount, sizeof(float) * 3 * 3, ComputeBufferType.Append);
            pointsBuffer = new ComputeBuffer(numBlocks / 4, sizeof(byte) * 4);
            triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
            edgeBuffer = new ComputeBuffer((Chunk.size.width + Chunk.size.width + 1) * Chunk.size.height / 4, sizeof(byte) * 4);
        }
    }

    void ReleaseBuffers()
    {
        if (pointsBuffer != null)
        {
            triangleBuffer.Release();
            pointsBuffer.Release();
            triCountBuffer.Release();
            edgeBuffer.Release();
        }
    }
}
