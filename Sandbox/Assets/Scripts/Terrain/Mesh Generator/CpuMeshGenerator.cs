using System.Collections.Generic;                                                                                                                                                  
using UnityEngine;
using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Sandbox.ProceduralTerrain.Core
{
    public class CpuMeshGenerator
    {
        const int _maxColumnsPerUpdate = 2;
        const int _maxChunksPerUpdate = 2;

        readonly TerrainSettings _settings;

        MarchTablesBurst _marchTables;
        Action<GeneratedDataInfo<MeshData[]>> _columnDataCallback;
        Action<GeneratedDataInfo<MeshData>> _chunkDataCallback;
        Queue<Vector2Int> _requestedColumns;
        Queue<Vector3Int> _requestedChunks;
        ColumnMarchData[] _columnData;
        ChunkMarchData[] _chunkData;
        JobsData _columnJobs;
        JobsData _chunkJobs;

        public CpuMeshGenerator(Action<GeneratedDataInfo<MeshData[]>> columnDataCallback, Action<GeneratedDataInfo<MeshData>> chunkDataCallback, TerrainSettings Settings)
        {
            this._columnDataCallback = columnDataCallback;
            this._chunkDataCallback = chunkDataCallback;
            this._settings = Settings;
            Initialize();
        }

        public void Destroy()
        {
            ReleaseBuffers();
        }

        public void RequestChunkData(Vector3Int coord)
        {
            _requestedChunks.Enqueue(coord);
        }

        public void RequestColumnData(Vector2Int coord)
        {
            _requestedColumns.Enqueue(coord);
        }

        public Queue<Vector2Int> GetColumnRequests()
        {
            return _requestedColumns;
        }

        public void ClearColumnRequests()
        {
            _requestedColumns.Clear();
        }

        public void ReplaceColumnRequests(List<Vector2Int> forGeneration)
        {
            _requestedColumns.Clear();
            for (int i = 0; i < forGeneration.Count; i++)
                _requestedColumns.Enqueue(forGeneration[i]);
        }

        public void ManageColumnRequests()
        {
            // Return requested columns data       
            if (_columnJobs.jobsAtWork.Count > 0)
                for (int jobIndex = 0; jobIndex < _maxColumnsPerUpdate; jobIndex++)
                {
                    JobHandle jobHandle;
                    if (_columnJobs.jobsAtWork.TryGetValue(jobIndex, out jobHandle))
                    {
                        if (jobHandle.IsCompleted)
                        {
                            Vector3Int coord = _columnJobs.jobCoords[jobIndex];
                            jobHandle.Complete();
                            _columnDataCallback(new GeneratedDataInfo<MeshData[]>(CopyColumnMeshData(jobIndex), coord));

                            _columnJobs.jobsAtWork.Remove(jobIndex);
                            _columnJobs.availableJobIndicies.Enqueue(jobIndex);
                        }
                    }
                }

            // Go through requested coordinates and start generation threads
            if (_requestedColumns.Count > 0)
            {
                int maxJobs = Mathf.Min(_columnJobs.availableJobIndicies.Count, Mathf.Min(_maxColumnsPerUpdate, _requestedColumns.Count));
                for (int i = 0; i < maxJobs; i++)
                {
                    int jobIndex = _columnJobs.availableJobIndicies.Dequeue();
                    Vector2Int coord = _requestedColumns.Dequeue();
                    FillColumnData(coord, jobIndex);

                    _columnJobs.jobsAtWork.Add(jobIndex, ScheduleColumnJob(_columnData[jobIndex]));
                    _columnJobs.jobCoords[jobIndex] = new Vector3Int(coord.x, -1, coord.y);
                }
            }
        }

        public void ManageChunkRequests()
        {
            // Return requested chunks data
            if (_chunkJobs.jobsAtWork.Count > 0)
                for (int jobIndex = 0; jobIndex < _maxChunksPerUpdate; jobIndex++)
                {
                    JobHandle jobHandle;
                    if (_chunkJobs.jobsAtWork.TryGetValue(jobIndex, out jobHandle))
                    {
                        if (jobHandle.IsCompleted)
                        {
                            Vector3Int coord = _chunkJobs.jobCoords[jobIndex];
                            jobHandle.Complete();
                            _chunkDataCallback(new GeneratedDataInfo<MeshData>(CopyChunkMeshData(_chunkData[jobIndex]), coord));

                            _chunkJobs.jobsAtWork.Remove(jobIndex);
                            _chunkJobs.availableJobIndicies.Enqueue(jobIndex);
                        }
                    }
                }

            // Go through requested coordinates and start generation threads
            if (_requestedChunks.Count > 0)
            {
                int maxJobs = Mathf.Min(_chunkJobs.availableJobIndicies.Count, Mathf.Min(_maxChunksPerUpdate, _requestedChunks.Count));
                for (int i = 0; i < maxJobs; i++)
                {
                    int jobIndex = _chunkJobs.availableJobIndicies.Dequeue();
                    Vector3Int coord = _requestedChunks.Dequeue();
                    FillChunkData(coord, jobIndex);

                    _chunkJobs.jobsAtWork.Add(jobIndex, ScheduleChunkJob(_chunkData[jobIndex]));
                    _chunkJobs.jobCoords[jobIndex] = coord;
                }
            }
        }

        //////////////////////////////////////////

        private MeshData[] CopyColumnMeshData(int jobIndex)
        {
            MeshData[] meshData = new MeshData[_settings.WorldHeight];
            for (int i = 0; i < _settings.WorldHeight; i++)
                meshData[i] = CopyChunkMeshData(_columnData[jobIndex].ChunksData[i]);

            return meshData;
        }

        private MeshData CopyChunkMeshData(ChunkMarchData chunkData)
        {
            MeshData meshData;
            if (chunkData.Verticies.Count > 0)
            {
                meshData = new MeshData();

                meshData.Vertices = new Vector3[chunkData.Verticies.Count];
                NativeArray<float3> tmpVertices = chunkData.Verticies.ToArray(Allocator.Temp);
                tmpVertices.Reinterpret<Vector3>().CopyTo(meshData.Vertices);
                tmpVertices.Dispose();

                meshData.Triangles = new int[chunkData.Indicies.Count];
                NativeArray<int> tmpIndicies = chunkData.Indicies.ToArray(Allocator.Temp);
                tmpIndicies.CopyTo(meshData.Triangles);
                tmpIndicies.Dispose();
            }
            else
            {
                meshData = new MeshData(0);
            }

            chunkData.Clear();

            return meshData;
        }

        private void FillChunkData(Vector3Int coord, int jobIndex)
        {
            byte[,,][] surroundingBlocks = new byte[3, 3, 3][];
            Vector3Int chunkCoord = new Vector3Int();

            for (chunkCoord.x = 0; chunkCoord.x < 3; chunkCoord.x++)
                for (chunkCoord.y = 0; chunkCoord.y < 3; chunkCoord.y++)
                    for (chunkCoord.z = 0; chunkCoord.z < 3; chunkCoord.z++)
                    {
                        ProTerra.Instance.GetBlockValueArray(out surroundingBlocks[chunkCoord.z, chunkCoord.y, chunkCoord.x], chunkCoord + coord - Vector3Int.one);
                    }
            _chunkData[jobIndex].Blocks.SetBlocks(surroundingBlocks);
        }

        private void FillColumnData(Vector2Int coord, int jobIndex)
        {
            Vector3Int relativeCoord = new Vector3Int();

            for (relativeCoord.z = 0; relativeCoord.z < 3; relativeCoord.z++)
                for (relativeCoord.y = 0; relativeCoord.y < _settings.WorldHeight; relativeCoord.y++)
                    for (relativeCoord.x = 0; relativeCoord.x < 3; relativeCoord.x++)
                    {
                        var reference = ProTerra.Instance.GetBlockArrayReference(new Vector3Int(coord.x - 1, 0, coord.y - 1) + relativeCoord);
                        if (reference.Length == 0) 
                            reference.Dispose();
                        else 
                            _columnData[jobIndex].AssignReference(reference, relativeCoord);
                    }
            _columnData[jobIndex].SetReferences();
        }

        JobHandle ScheduleChunkJob(ChunkMarchData chunkData)
        {
            FillVerticiesArrayJob fillJob = new FillVerticiesArrayJob
            {
                blocks = chunkData.Blocks,
                verticesExpanded = chunkData.VerticesExpanded,
            };
            JobHandle fillJobHandle = fillJob.Schedule(ChunkMarchVerticies.Length, 32);

            CollapseIndiciesJob collapseJob = new CollapseIndiciesJob
            {
                blocks = chunkData.Blocks,
                verticiesExpanded = chunkData.VerticesExpanded,
                verticies = chunkData.Verticies,
                indicies = chunkData.Indicies,
                marchTables = this._marchTables,
                //mapping = chunkData.mapping,
            };

            return collapseJob.Schedule(fillJobHandle);
        }

        JobHandle ScheduleColumnJob(ColumnMarchData columnData)
        {
            NativeArray<JobHandle> chunkJobs = new NativeArray<JobHandle>(_settings.WorldHeight, Allocator.Temp);
            for (int i = 0; i < _settings.WorldHeight; i++)
            {
                chunkJobs[i] = ScheduleChunkJob(columnData.ChunksData[i]);
            }

            JobHandle result = JobHandle.CombineDependencies(chunkJobs);
            chunkJobs.Dispose();
            return result;
        }


        ////////////////////////////////////////// 

        void Initialize()
        {
            _requestedChunks = new Queue<Vector3Int>();
            _requestedColumns = new Queue<Vector2Int>();

            _columnJobs.Create(_maxColumnsPerUpdate);
            _chunkJobs.Create(_maxChunksPerUpdate);

            _marchTables = new MarchTablesBurst();
            _marchTables.Create();

            _columnData = new ColumnMarchData[_maxColumnsPerUpdate];
            for (int i = 0; i < _maxColumnsPerUpdate; i++)
            {
                _columnData[i].Create(_settings.WorldHeight);
            }

            _chunkData = new ChunkMarchData[_maxChunksPerUpdate];
            for (int i = 0; i < _maxChunksPerUpdate; i++)
            {
                _chunkData[i].Create(false);
            }
        }

        void ReleaseBuffers()
        {
            _marchTables.Dispose();
            _columnJobs.CompleteAll();
            _chunkJobs.CompleteAll();

            for (int i = 0; i < _maxColumnsPerUpdate; i++)
            {
                _columnData[i].Dispose();
            }
            for (int i = 0; i < _maxChunksPerUpdate; i++)
            {
                _chunkData[i].Dispose();
            }
        }

        private struct JobsData
        {
            public int maxJobsPerUpdate;
            public Queue<int> availableJobIndicies;
            public Dictionary<int, JobHandle> jobsAtWork;
            public Vector3Int[] jobCoords;

            public void Create(int maxJobsPerUpdate)
            {
                this.maxJobsPerUpdate = maxJobsPerUpdate;
                availableJobIndicies = new Queue<int>();
                jobsAtWork = new Dictionary<int, JobHandle>();
                jobCoords = new Vector3Int[maxJobsPerUpdate];

                for (int i = 0; i < maxJobsPerUpdate; i++)
                    availableJobIndicies.Enqueue(i);
            }

            public void CompleteAll()
            {
                for (int i = 0; i < maxJobsPerUpdate; i++)
                {
                    JobHandle job;
                    if (jobsAtWork.TryGetValue(i, out job))
                        job.Complete();
                }
            }
        }
    }
}