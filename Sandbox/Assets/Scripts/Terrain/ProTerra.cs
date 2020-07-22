using UnityEngine;
using System;
using Unity.Collections;
using Sandbox.ProceduralTerrain.Core;

namespace Sandbox.ProceduralTerrain
{
    /* Main Procedural Terrain script for public methods */
    [DisallowMultipleComponent]
    public class ProTerra : ChunksManager
    {
        public static ProTerra Instance;

        [Header("Gizmos")]
        [SerializeField]
        bool showChunksGizmo = false;
        [SerializeField]
        Color chunksGizmoCol = new Color(1, 1, 1, .3f);
        [SerializeField]
        Color generatedChunksGizmoCol = Color.green;

        protected override void Awake()
        {
            CreateSingleton();
            base.Awake();
            SetVariables();
        }

        private void CreateSingleton()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void SetVariables()
        {
            transform.position = Vector3.zero;
        }

        ///////////////
        /* Interface */
        ///////////////

        // Returns single block value
        public byte GetBlockValue(Vector3Int blockPosition)
        {
            Vector3Int localBlockPosition = blockPosition.ToChunkPosition();
            Vector3Int chunkCoord = blockPosition.ToChunkCoord();

            if (ExistingChunks.TryGetValue(chunkCoord, out Chunk chunk))
                return chunk.Blocks[localBlockPosition.z, localBlockPosition.y, localBlockPosition.x];

            return 0;
        }

        public byte GetBlockValue(Vector3Int localBlockPosition, Vector3Int chunkCoord)
        {
            if (ExistingChunks.TryGetValue(chunkCoord, out Chunk chunk))
                return chunk.Blocks[localBlockPosition.z, localBlockPosition.y, localBlockPosition.x];

            return 0;
        }

        // Returns an array of block values
        public void GetBlockValueArray(out byte[,,] blocksArray, Vector3Int fromPosition, Vector3Int toPosition)
        {
            if (fromPosition.x > toPosition.x || fromPosition.y > toPosition.y || fromPosition.z > toPosition.z)
            {
                blocksArray = new byte[0, 0, 0];
                return;
            }

            blocksArray = new byte[toPosition.z - fromPosition.z + 1, toPosition.y - fromPosition.y + 1, toPosition.x - fromPosition.x + 1];
            Vector3Int fromCoord = fromPosition.ToChunkCoord();
            Vector3Int toCoord = toPosition.ToChunkCoord();

            // Go through required chunks and copy data
            for (int zCoord = fromCoord.z; zCoord <= toCoord.z; zCoord++)
                for (int yCoord = fromCoord.y; yCoord <= toCoord.y; yCoord++)
                    for (int xCoord = fromCoord.x; xCoord <= toCoord.x; xCoord++)
                    {
                        Vector3Int chunkOrigin = (new Vector3Int(xCoord, yCoord, zCoord)).ToChunkOrigin(); ;
                        Vector3Int chunkEnd = chunkOrigin + new Vector3Int(ChunkSize.Width - 1, ChunkSize.Height - 1, ChunkSize.Width - 1);

                        Vector3Int from = Vector3Int.Max(fromPosition, chunkOrigin);
                        Vector3Int to = Vector3Int.Min(toPosition, chunkEnd);

                        Vector3Int startResultIndex = from - fromPosition;
                        Vector3Int startChunkIndex = from.ToChunkPosition();
                        Vector3Int maxIndex = to - from;

                        if (ExistingChunks.TryGetValue(new Vector3Int(xCoord, yCoord, zCoord), out Chunk chunk))
                        {
                            for (int z = 0; z <= maxIndex.z; z++)
                                for (int y = 0; y <= maxIndex.y; y++)
                                    for (int x = 0; x <= maxIndex.x; x++)
                                    {
                                        blocksArray[startResultIndex.z + z, startResultIndex.y + y, startResultIndex.x + x] = chunk.Blocks[startChunkIndex.z + z, startChunkIndex.y + y, startChunkIndex.x + x];
                                    }
                        }
                        else
                        {
                            for (int z = 0; z <= maxIndex.z; z++)
                                for (int y = 0; y <= maxIndex.y; y++)
                                    for (int x = 0; x <= maxIndex.x; x++)
                                    {
                                        blocksArray[startResultIndex.z + z, startResultIndex.y + y, startResultIndex.x + x] = 0;
                                    }
                        }
                    }
        }

        public void GetBlockValueArray(out byte[] blocksArray, Vector3Int fromPosition, Vector3Int toPosition)
        {
            if (fromPosition.x > toPosition.x || fromPosition.y > toPosition.y || fromPosition.z > toPosition.z)
            {
                blocksArray = new byte[0];
                return;
            }
            Vector3Int resultSize = toPosition - fromPosition + Vector3Int.one;
            blocksArray = new byte[resultSize.x * resultSize.y * resultSize.z];
            Vector3Int fromCoord = fromPosition.ToChunkCoord();
            Vector3Int toCoord = toPosition.ToChunkCoord();

            // Go through required chunks and copy data
            for (int zCoord = fromCoord.z; zCoord <= toCoord.z; zCoord++)
                for (int yCoord = fromCoord.y; yCoord <= toCoord.y; yCoord++)
                    for (int xCoord = fromCoord.x; xCoord <= toCoord.x; xCoord++)
                    {
                        Vector3Int chunkOrigin = (new Vector3Int(xCoord, yCoord, zCoord)).ToChunkOrigin();
                        Vector3Int chunkEnd = chunkOrigin + new Vector3Int(ChunkSize.Width - 1, ChunkSize.Height - 1, ChunkSize.Width - 1);

                        Vector3Int from = Vector3Int.Max(fromPosition, chunkOrigin);
                        Vector3Int to = Vector3Int.Min(toPosition, chunkEnd);

                        Vector3Int startResultIndex = from - fromPosition;
                        Vector3Int startChunkIndex = from.ToChunkPosition();
                        Vector3Int maxIndex = to - from;

                        if (ExistingChunks.TryGetValue(new Vector3Int(xCoord, yCoord, zCoord), out Chunk chunk))
                        {
                            for (int z = 0; z <= maxIndex.z; z++)
                                for (int y = 0; y <= maxIndex.y; y++)
                                    for (int x = 0; x <= maxIndex.x; x++)
                                    {
                                        blocksArray[(startResultIndex.z + z) * resultSize.x * resultSize.y + (startResultIndex.y + y) * resultSize.x + startResultIndex.x + x] = chunk.Blocks[startChunkIndex.z + z, startChunkIndex.y + y, startChunkIndex.x + x];
                                    }
                        }
                        else
                        {
                            for (int z = 0; z <= maxIndex.z; z++)
                                for (int y = 0; y <= maxIndex.y; y++)
                                    for (int x = 0; x <= maxIndex.x; x++)
                                    {
                                        blocksArray[(startResultIndex.z + z) * resultSize.x * resultSize.y + (startResultIndex.y + y) * resultSize.x + startResultIndex.x + x] = 0;
                                    }
                        }
                    }
        }

        public void GetBlockValueArray(out byte[,,] blocksArray, Vector3Int chunkCoord)
        {
            blocksArray = new byte[ChunkSize.Width, ChunkSize.Height, ChunkSize.Width];
            if (ExistingChunks.TryGetValue(chunkCoord, out Chunk chunk))
            {
                byte[] tmp = new byte[blocksArray.Length];
                chunk.Blocks.Native.CopyTo(tmp);
                Buffer.BlockCopy(tmp, 0, blocksArray, 0, blocksArray.Length);
            }
        }

        public void GetBlockValueArray(out byte[] blocksArray, Vector3Int chunkCoord)
        {
            blocksArray = new byte[ChunkSize.Length];
            if (ExistingChunks.TryGetValue(chunkCoord, out Chunk chunk))
            {
                chunk.Blocks.Native.CopyTo(blocksArray);
            }
        }

        public void GetBlockValueArray(out NativeArray<byte> blocksArray, Vector3Int chunkCoord)
        {
            if (ExistingChunks.TryGetValue(chunkCoord, out Chunk chunk))
            {
                blocksArray = new NativeArray<byte>(chunk.Blocks.Native, Allocator.Persistent);
                return;
            }
            blocksArray = new NativeArray<byte>(ChunkSize.Length, Allocator.None);
        }

        public NativeArray<byte> GetBlockArrayReference(Vector3Int chunkCoord)
        {
            if (ExistingChunks.TryGetValue(chunkCoord, out Chunk chunk))
            {
                return chunk.Blocks.Native;
            }
            return new NativeArray<byte>(0, Allocator.Persistent);
        }

        // Tries to modyfy block. If successfull, requests mesh and returns true
        public bool ModifyBlock(Vector3Int chunkCoord, Vector3Int localBlockPosition, int value)
        {
            if (ExistingChunks.TryGetValue(chunkCoord, out Chunk chunk))
            {
                if (chunk.ModifyBlock(localBlockPosition, value))
                {
                    MarkForMeshGeneration(chunk);

                    for (int i = 1; i < 8; i++)
                    {
                        int x = i & 1;
                        int y = (i & 2) >> 1;
                        int z = (i & 4) >> 2;

                        if ((localBlockPosition.x * x == 0) && (localBlockPosition.y * y == 0) && (localBlockPosition.z * z == 0) && ExistingChunks.TryGetValue(chunkCoord - new Vector3Int(x, y, z), out chunk))
                            MarkForMeshGeneration(chunk);
                    }
                    return true;
                }
            }

            return false;
        }

        public bool ModifyBlock(Vector3Int blockPosition, int value)
        {
            Vector3Int chunkCoord = blockPosition.ToChunkCoord();
            Vector3Int localBlockPosition = blockPosition.ToChunkPosition();

            if (ExistingChunks.TryGetValue(chunkCoord, out Chunk chunk))
            {
                if (chunk.ModifyBlock(localBlockPosition, value))
                {
                    MarkForMeshGeneration(chunk);

                    for (int i = 1; i < 8; i++)
                    {
                        int x = i & 1;
                        int y = (i & 2) >> 1;
                        int z = (i & 4) >> 2;

                        if ((localBlockPosition.x * x == 0) && (localBlockPosition.y * y == 0) && (localBlockPosition.z * z == 0) && ExistingChunks.TryGetValue(chunkCoord - new Vector3Int(x, y, z), out chunk))
                            MarkForMeshGeneration(chunk);
                    }
                    return true;
                }
            }

            return false;
        }

        void OnDrawGizmos()
        {
            if (Application.isPlaying)
            {
                if (showChunksGizmo)
                {
                    Vector3 chunkSize = new Vector3(ChunkSize.Width, ChunkSize.Height * WorldHeight, ChunkSize.Width);
                    var chunks = ExistingChunks.Values();
                    foreach (var chunk in chunks)
                    {
                        if (chunk.HasMesh)
                            Gizmos.color = generatedChunksGizmoCol;
                        else
                            Gizmos.color = chunksGizmoCol;
                        Vector2Int origin = chunk.Coord.ToChunkOrigin();
                        Gizmos.DrawWireCube(origin.X0Y() + chunkSize / 2f, chunkSize);
                    }
                }
                // world bounds 
                Gizmos.color = Color.green;
                Vector3 worldOrigin = (ViewerCoord - Vector2Int.one * GenerationDistance).ToChunkOrigin().X0Y();
                Vector3 worldSize = new Vector3(ChunkSize.Width * (2 * GenerationDistance + 1), 0, ChunkSize.Width * (2 * GenerationDistance + 1));
                Gizmos.DrawLine(worldOrigin, worldOrigin + worldSize - worldSize.VecX());
                Gizmos.DrawLine(worldOrigin + worldSize - worldSize.VecX(), worldOrigin + worldSize);
                Gizmos.DrawLine(worldOrigin + worldSize, worldOrigin + worldSize - worldSize.VecZ());
                Gizmos.DrawLine(worldOrigin + worldSize - worldSize.VecZ(), worldOrigin);
            }
        }
    }
}