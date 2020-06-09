using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public class CpuMeshGenerator : IMeshGenerator
{
    MeshGeneratorSettings Settings;

    Queue<Vector3Int> requestedCoords = new Queue<Vector3Int>();
    Action<GeneratedDataInfo<MeshData>> dataCallback;
    ProTerra terrain;

    static int maxJobsPerUpdate = 4;

    ChunkMeshBlocks[] blocksForMesh;  // blocks data for generating mesh
    ChunkMeshVerticies[] verticesExpanded;
    NativeQueue<float3>[] verticies;
    NativeQueue<int>[] indicies;
    MarchTablesBurst marchTables;
        
    Queue<int> availableJobIndicies;
    Dictionary<int, JobHandle> jobsAtWork;
    Vector3Int[] jobsAssignedCoords;


    public CpuMeshGenerator(Action<GeneratedDataInfo<MeshData>> dataCallback, ProTerra terrain, MeshGeneratorSettings meshGeneratorSettings)
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
        //Return requested data       
        if (jobsAtWork.Count > 0)
            for (int jobIndex = 0; jobIndex < maxJobsPerUpdate; jobIndex++)
            {
                JobHandle jobHandle;
                if (jobsAtWork.TryGetValue(jobIndex, out jobHandle))
                {
                    if (jobHandle.IsCompleted)
                    {
                        Vector3Int coord = jobsAssignedCoords[jobIndex];
                        jobHandle.Complete();
                        dataCallback(new GeneratedDataInfo<MeshData>(CopyCpuMeshData(jobIndex), coord));

                        jobsAtWork.Remove(jobIndex);
                        availableJobIndicies.Enqueue(jobIndex);
                    }
                }
            }

        // Go through requested coordinates and start generation threads if still relevant
        if (requestedCoords.Count > 0)
        {
            Vector3Int viewerCoord = new Vector3Int(Mathf.FloorToInt(terrain.viewer.position.x / Chunk.size.width), 0, Mathf.FloorToInt(terrain.viewer.position.z / Chunk.size.width));

            int maxJobs = Mathf.Min(maxJobsPerUpdate, availableJobIndicies.Count);
            for (int i = 0; i < maxJobs && requestedCoords.Count > 0; i++)
            {
                Vector3Int coord = requestedCoords.Dequeue();

                // skip outdated coordinates
                while ((Mathf.Abs(coord.x - viewerCoord.x) > terrain.viewDistance || Mathf.Abs(coord.z - viewerCoord.z) > terrain.viewDistance) && requestedCoords.Count > 0)
                {
                    coord = requestedCoords.Dequeue();
                }

                // check last coordinate
                if (Mathf.Abs(coord.x - viewerCoord.x) <= terrain.viewDistance && Mathf.Abs(coord.z - viewerCoord.z) <= terrain.viewDistance)
                {
                    // check for adjacent chunks
                    bool generate = true;
                    int y = 0;
                    for (int x = -1; x <= 1; x++)
                        //for (int y = -1; y <= 1; y++)
                        for (int z = -1; z <= 1; z++)
                            if (!terrain.existingChunks.ContainsKey(new Vector3Int(coord.x + x, coord.y + y, coord.z + z)))
                                generate = false;

                    // start generation
                    if (generate)
                    {
                        int jobIndex = availableJobIndicies.Dequeue();

                        FillBlocksArray(coord, jobIndex);

                        jobsAtWork.Add(jobIndex, ScheduleJob(jobIndex));
                        jobsAssignedCoords[jobIndex] = coord;
                    }
                }
            }
        }
    }


    //////////////////////////////////////////

    MeshData CopyCpuMeshData(int jobIndex)
    {
        MeshData meshData;
        if (verticies[jobIndex].Count > 0)
        {
            meshData = new MeshData();

            meshData.vertices = new Vector3[verticies[jobIndex].Count];
            verticies[jobIndex].ToArray(Allocator.Temp).Reinterpret<Vector3>().CopyTo(meshData.vertices);

            meshData.triangles = new int[indicies[jobIndex].Count];
            indicies[jobIndex].ToArray(Allocator.Temp).CopyTo(meshData.triangles);

            verticies[jobIndex].Clear();
            indicies[jobIndex].Clear();
        }
        else
        {
            meshData = new MeshData(0);
        }

        return meshData;
    }

    void FillBlocksArray(Vector3Int coord, int jobIndex)
    {
        Chunk[] chunks = new Chunk[27];

        int i = 0;
        for (int z = 0; z < 3; z++)
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                    terrain.existingChunks.TryGetValue(new Vector3Int(coord.x + x - 1, coord.y + y - 1, coord.z + z - 1), out chunks[i++]);

        i = 0;
        for (int z = -1; z < Chunk.size.width + 2; z++)
            for (int y = -1; y < Chunk.size.height + 2; y++)
                for (int x = -1; x < Chunk.size.width + 2; x++)
                {
                    int chunkIndex = (x + Chunk.size.width) / Chunk.size.width + ((y + Chunk.size.height) / Chunk.size.height) * 3 + ((z + Chunk.size.width) / Chunk.size.width) * 9;

                    if (chunks[chunkIndex] != null)
                        blocksForMesh[jobIndex][i++] = chunks[chunkIndex].blocks[(z + Chunk.size.width) % Chunk.size.width, (y + Chunk.size.height) % Chunk.size.height, (x + Chunk.size.width) % Chunk.size.width];
                    else
                        blocksForMesh[jobIndex][i++] = 0;
                }
    }

    JobHandle ScheduleJob(int jobIndex)
    {
        FillVerticiesArrayJob fillJob = new FillVerticiesArrayJob
        {
            blocks = blocksForMesh[jobIndex],
            verticesExpanded = this.verticesExpanded[jobIndex],
        };
        JobHandle fillJobHandle = fillJob.Schedule(verticesExpanded[jobIndex].length, 32);

        CollapseIndiciesJob collapseJob = new CollapseIndiciesJob
        {
            blocks = blocksForMesh[jobIndex],
            verticiesExpanded = this.verticesExpanded[jobIndex],
            verticies = this.verticies[jobIndex],
            indicies = this.indicies[jobIndex],
            marchTables = this.marchTables,
        };

        return collapseJob.Schedule(fillJobHandle);
    }

    
    ////////////////////////////////////////// 

    void Initialize()
    {
        availableJobIndicies = new Queue<int>();
        jobsAtWork = new Dictionary<int, JobHandle>();
        jobsAssignedCoords = new Vector3Int[maxJobsPerUpdate];

        blocksForMesh = new ChunkMeshBlocks[maxJobsPerUpdate];
        verticesExpanded = new ChunkMeshVerticies[maxJobsPerUpdate];
        verticies = new NativeQueue<float3>[maxJobsPerUpdate];
        indicies = new NativeQueue<int>[maxJobsPerUpdate];
        marchTables = new MarchTablesBurst(1);

        for (int jobIndex = 0; jobIndex < maxJobsPerUpdate; jobIndex++)
        {
            availableJobIndicies.Enqueue(jobIndex);

            if (!blocksForMesh[jobIndex].blocks.IsCreated)
            {
                blocksForMesh[jobIndex] = new ChunkMeshBlocks(Chunk.size.width, Chunk.size.height);
            }
            if (!verticesExpanded[jobIndex].verticies.IsCreated)
            {
                verticesExpanded[jobIndex] = new ChunkMeshVerticies(Chunk.size.width, Chunk.size.height);
            }
            if (!verticies[jobIndex].IsCreated)
                verticies[jobIndex] = new NativeQueue<float3>(Allocator.Persistent);
            if (!indicies[jobIndex].IsCreated)
                indicies[jobIndex] = new NativeQueue<int>(Allocator.Persistent);
        }
    }

    void ReleaseBuffers()
    {   
        for (int jobIndex = 0; jobIndex < maxJobsPerUpdate; jobIndex++)
        {
            JobHandle job;
            if (jobsAtWork.TryGetValue(jobIndex, out job))
                job.Complete();

            if (blocksForMesh[jobIndex].blocks.IsCreated)
                blocksForMesh[jobIndex].blocks.Dispose();
            if (verticesExpanded[jobIndex].verticies.IsCreated)
                verticesExpanded[jobIndex].verticies.Dispose();
            if (verticies[jobIndex].IsCreated)
                verticies[jobIndex].Dispose();
            if (indicies[jobIndex].IsCreated)
                indicies[jobIndex].Dispose();
        }

        marchTables.Dispose();
    }
}
