using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Sandbox.ProceduralTerrain.Core
{
    public struct ChunkMarchData
    {
        public ChunkMarchBlocks Blocks;
        public ChunkMarchVerticies VerticesExpanded;
        public NativeQueue<float3> Verticies;
        public NativeQueue<int> Indicies;

        //public NativeArray<int> mapping;
        //static NativeArray<int> _clear;

        public void Create(bool isReference)
        {
            Blocks = new ChunkMarchBlocks(isReference);
            VerticesExpanded = ChunkMarchVerticies.Create();
            Verticies = new NativeQueue<float3>(Allocator.Persistent);
            Indicies = new NativeQueue<int>(Allocator.Persistent);

            //mapping = new NativeArray<int>(ChunkMarchVerticies.Length * 3, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            //_clear = new NativeArray<int>(ChunkMarchVerticies.Length * 3, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        public void Clear()
        {
            if (Verticies.IsCreated)
                Verticies.Clear();
            if (Indicies.IsCreated)
                Indicies.Clear();
            //if (mapping.IsCreated)
            //    mapping.CopyFrom(_clear);
        }

        public void Dispose()
        {
            Blocks.Dispose();
            VerticesExpanded.Dispose();
            if (Verticies.IsCreated)
                Verticies.Dispose();
            if (Indicies.IsCreated)
                Indicies.Dispose();
            //if (mapping.IsCreated)
            //    mapping.Dispose();
            //if (_clear.IsCreated)
            //    _clear.Dispose();
        }
    }

    public struct ColumnMarchData
    {
        public ChunkMarchData[] ChunksData;

        NativeArray<byte>[,,] _blocksReferences;
        int _worldHeight;

        public void Create(int WorldHeight)
        {
            _worldHeight = WorldHeight;
            _blocksReferences = new NativeArray<byte>[3, _worldHeight + 2, 3];

            ChunksData = new ChunkMarchData[_worldHeight];
            for (int i = 0; i < _worldHeight; i++)
            {
                ChunksData[i].Create(true);
            }

            // Outside of boundary chunks are never assigned
            for (int z = 0; z < 3; z++)
                for (int x = 0; x < 3; x++)
                {
                    _blocksReferences[z, 0, x] = new NativeArray<byte>(ChunkSize.Length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                    _blocksReferences[z, _worldHeight + 1, x] = new NativeArray<byte>(ChunkSize.Length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                }
        }

        public void Clear()
        {
            for (int i = 0; i < _worldHeight; i++)
                ChunksData[i].Clear();
        }

        public void AssignReference(NativeArray<byte> blocks, Vector3Int relativeCoord)
        {
            _blocksReferences[relativeCoord.z, relativeCoord.y + 1, relativeCoord.x] = blocks;
        }

        public void SetReferences()
        {
            for (int i = 0; i < _worldHeight; i++)
                for (int z = 0; z < 3; z++)
                    for (int y = 0; y < 3; y++)
                        for (int x = 0; x < 3; x++)
                        {
                            ChunksData[i].Blocks.SetBlocksReference(_blocksReferences[z, y + i, x], new Vector3Int(x - 1, y - 1, z - 1));
                        }
        }

        public void Dispose()
        {
            for (int i = 0; i < _worldHeight; i++)
            {
                ChunksData[i].Dispose();
            }
            for (int z = 0; z < 3; z++)
                for (int x = 0; x < 3; x++)
                {
                    _blocksReferences[z, 0, x].Dispose();
                    _blocksReferences[z, _worldHeight + 1, x].Dispose();
                }
        }
    }

    public struct ChunkMarchVerticies
    {
        public const int Length = _width * _width * _height;
        const int _width = ChunkSize.Width + 1;
        const int _height = ChunkSize.Height + 1;

        NativeArray<Byte3> _verticies;
        

        public static ChunkMarchVerticies Create()
        {
            return new ChunkMarchVerticies
            {
                _verticies = new NativeArray<Byte3>(Length, Allocator.Persistent)
            };
        }

        public int3 IdToCoord(int id)
        {
            return new int3(id % _width, (id / _width) % _height, id / (_width * _height));
        }

        public int CoordToId(int3 coordinate)
        {
            return coordinate.x + coordinate.y * _width + coordinate.z * _width * _height;
        }

        public Byte3 this[int i]
        {
            get
            {
                return _verticies[i];
            }
            set
            {
                _verticies[i] = value;
            }
        }

        public Byte3 this[int3 coordinate]
        {
            get
            {
                int id = coordinate.x + coordinate.y * _width + coordinate.z * _width * _height;
                if (id >= Length) return new Byte3(0, 0, 0);
                return _verticies[id];
            }
            set
            {
                int id = coordinate.x + coordinate.y * _width + coordinate.z * _width * _height;
                if (id < Length)
                    _verticies[id] = value;
            }
        }

        public void Dispose()
        {
            if (_verticies.IsCreated)
                _verticies.Dispose();
        }
    }

    public struct ChunkMarchBlocks
    {
        public const int Width = ChunkSize.Width;
        public const int Height = ChunkSize.Height;
        public const int Length = ChunkSize.Length;

        private NativeBlocks _blocks;
        
        private readonly bool _isReference;

        public ChunkMarchBlocks(bool isReference)
        {
            this._isReference = isReference;
            //ChunkSize.Length = ChunkSize.Width * ChunkSize.Height * ChunkSize.Width;
            _blocks = new NativeBlocks();

            if (!isReference)
            {
                for (int i = 0; i < 27; i++)
                    _blocks[i] = new NativeArray<byte>(Length, Allocator.Persistent);
            }
        }

        public void SetBlocks(byte[][] surroundingBlocks)
        {
            if (_isReference) return;

            for (int i = 0; i < 27; i++)
            {
                NativeArray<byte>.Copy(surroundingBlocks[i], 0, _blocks[i], 0, Length);
            }
        }

        public void SetBlocks(byte[,,][] surroundingBlocks)
        {
            if (_isReference) return;

            for (int z = 0, i = 0; z < 3; z++)
                for (int y = 0; y < 3; y++)
                    for (int x = 0; x < 3; x++)
                    {
                        NativeArray<byte>.Copy(surroundingBlocks[z, y, x], 0, _blocks[i++], 0, Length);
                    }
        }

        public void SetBlocksReference(NativeArray<byte>[,,] surroundingBlocks)
        {
            if (!_isReference) return;

            int i = 0;
            for (int z = 0; z < 3; z++)
                for (int y = 0; y < 3; y++)
                    for (int x = 0; x < 3; x++)
                    {
                        _blocks[i++] = surroundingBlocks[z, y, x];
                    }
        }

        public void SetBlocksReference(NativeArray<byte> blocks, Vector3Int relativeCoord)
        {
            if (!_isReference) return;
            this._blocks[(relativeCoord.x + 1) + (relativeCoord.y + 1) * 3 + (relativeCoord.z + 1) * 9] = blocks;
        }

        public int3 ChunkBlockIdToPosition(int id)
        {
            return new int3(id % Width,
                           (id / Width) % Height,
                            id / (Width * Height));
        }

        public byte this[int3 coordinate]
        {
            get
            {
                int chunkId = (coordinate.x < 0 ? 0 : coordinate.x < Width ? 1 : 2) +
                              (coordinate.y < 0 ? 0 : coordinate.y < Height ? 3 : 6) +
                              (coordinate.z < 0 ? 0 : coordinate.z < Width ? 9 : 18);
                int id = (coordinate.x + Width) % Width +
                         ((coordinate.z + Width) % Width) * Width +
                         ((coordinate.y + Height) % Height) * Width * Width;

                return _blocks[chunkId][id];
            }
        }

        public void Dispose()
        {
            if (!_isReference)
                for (int i = 0; i < 27; i++)
                    if (_blocks[i].IsCreated)
                        _blocks[i].Dispose();
        }

        // jobs+burst system doesn't support array references, so they have to be defined explicitly
        private struct NativeBlocks
        {
            NativeArray<byte> _native_0, _native_1, _native_2,
                                     _native_3, _native_4, _native_5,
                                     _native_6, _native_7, _native_8,
                                     _native_9, _native_10, _native_11,
                                     _native_12, _native_13, _native_14,
                                     _native_15, _native_16, _native_17,
                                     _native_18, _native_19, _native_20,
                                     _native_21, _native_22, _native_23,
                                     _native_24, _native_25, _native_26;

            public NativeArray<byte> this[int i]
            {
                get
                {
                    switch (i)
                    {
                        case 0: return _native_0;
                        case 1: return _native_1;
                        case 2: return _native_2;
                        case 3: return _native_3;
                        case 4: return _native_4;
                        case 5: return _native_5;
                        case 6: return _native_6;
                        case 7: return _native_7;
                        case 8: return _native_8;
                        case 9: return _native_9;
                        case 10: return _native_10;
                        case 11: return _native_11;
                        case 12: return _native_12;
                        case 13: return _native_13;
                        case 14: return _native_14;
                        case 15: return _native_15;
                        case 16: return _native_16;
                        case 17: return _native_17;
                        case 18: return _native_18;
                        case 19: return _native_19;
                        case 20: return _native_20;
                        case 21: return _native_21;
                        case 22: return _native_22;
                        case 23: return _native_23;
                        case 24: return _native_24;
                        case 25: return _native_25;
                        case 26: return _native_26;
                        default: return _native_0;
                    }
                }
                set
                {
                    switch (i)
                    {
                        case 0: _native_0 = value; break;
                        case 1: _native_1 = value; break;
                        case 2: _native_2 = value; break;
                        case 3: _native_3 = value; break;
                        case 4: _native_4 = value; break;
                        case 5: _native_5 = value; break;
                        case 6: _native_6 = value; break;
                        case 7: _native_7 = value; break;
                        case 8: _native_8 = value; break;
                        case 9: _native_9 = value; break;
                        case 10: _native_10 = value; break;
                        case 11: _native_11 = value; break;
                        case 12: _native_12 = value; break;
                        case 13: _native_13 = value; break;
                        case 14: _native_14 = value; break;
                        case 15: _native_15 = value; break;
                        case 16: _native_16 = value; break;
                        case 17: _native_17 = value; break;
                        case 18: _native_18 = value; break;
                        case 19: _native_19 = value; break;
                        case 20: _native_20 = value; break;
                        case 21: _native_21 = value; break;
                        case 22: _native_22 = value; break;
                        case 23: _native_23 = value; break;
                        case 24: _native_24 = value; break;
                        case 25: _native_25 = value; break;
                        case 26: _native_26 = value; break;
                        default: _native_0 = value; break;
                    }
                }
            }
        }
    }

    // copies MarchTables in a burst compatible format
    public struct MarchTablesBurst
    {
        public NativeArray<byte> Triangulation;
        public int MaxConfiguration { get; private set; }
        public int MaxTriIndex { get; private set; }
        public NativeArray<byte> CornerIndexAFromEdge;
        public NativeArray<byte> CornerIndexBFromEdge;

        public void Create()
        {
            MaxConfiguration = 256;
            MaxTriIndex = 16;
            Triangulation = new NativeArray<byte>(MaxConfiguration * MaxTriIndex, Allocator.Persistent);
            for (int configuration = 0; configuration < MaxConfiguration; configuration++)
                for (int triIndex = 0; triIndex < MaxTriIndex; triIndex++)
                {
                    Triangulation[configuration * MaxTriIndex + triIndex] = MarchTables.triangulation[configuration, triIndex];
                }
            CornerIndexAFromEdge = new NativeArray<byte>(12, Allocator.Persistent);
            CornerIndexBFromEdge = new NativeArray<byte>(12, Allocator.Persistent);
            for (int i = 0; i < 12; i++)
            {
                CornerIndexAFromEdge[i] = MarchTables.cornerIndexAFromEdge[i];
                CornerIndexBFromEdge[i] = MarchTables.cornerIndexBFromEdge[i];
            }
        }

        public void Dispose()
        {
            if (Triangulation.IsCreated)
                Triangulation.Dispose();
            if (CornerIndexAFromEdge.IsCreated)
                CornerIndexAFromEdge.Dispose();
            if (CornerIndexBFromEdge.IsCreated)
                CornerIndexBFromEdge.Dispose();
        }
    }



    struct MarchTables
    {
        public static byte[,] triangulation = {
        {255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 8, 3, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 1, 9, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 8, 3, 9, 8, 1, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 2, 10, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 8, 3, 1, 2, 10, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 2, 10, 0, 2, 9, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 2, 8, 3, 2, 10, 8, 10, 9, 8, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 11, 2, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 11, 2, 8, 11, 0, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 9, 0, 2, 3, 11, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 11, 2, 1, 9, 11, 9, 8, 11, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 10, 1, 11, 10, 3, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 10, 1, 0, 8, 10, 8, 11, 10, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 9, 0, 3, 11, 9, 11, 10, 9, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 8, 10, 10, 8, 11, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 7, 8, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 3, 0, 7, 3, 4, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 1, 9, 8, 4, 7, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 1, 9, 4, 7, 1, 7, 3, 1, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 2, 10, 8, 4, 7, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 4, 7, 3, 0, 4, 1, 2, 10, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 2, 10, 9, 0, 2, 8, 4, 7, 255, 255, 255, 255, 255, 255, 255 },
        { 2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, 255, 255, 255, 255 },
        { 8, 4, 7, 3, 11, 2, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 11, 4, 7, 11, 2, 4, 2, 0, 4, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 0, 1, 8, 4, 7, 2, 3, 11, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, 255, 255, 255, 255 },
        { 3, 10, 1, 3, 11, 10, 7, 8, 4, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, 255, 255, 255, 255 },
        { 4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, 255, 255, 255, 255 },
        { 4, 7, 11, 4, 11, 9, 9, 11, 10, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 5, 4, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 5, 4, 0, 8, 3, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 5, 4, 1, 5, 0, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 8, 5, 4, 8, 3, 5, 3, 1, 5, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 2, 10, 9, 5, 4, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 0, 8, 1, 2, 10, 4, 9, 5, 255, 255, 255, 255, 255, 255, 255 },
        { 5, 2, 10, 5, 4, 2, 4, 0, 2, 255, 255, 255, 255, 255, 255, 255 },
        { 2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, 255, 255, 255, 255 },
        { 9, 5, 4, 2, 3, 11, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 11, 2, 0, 8, 11, 4, 9, 5, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 5, 4, 0, 1, 5, 2, 3, 11, 255, 255, 255, 255, 255, 255, 255 },
        { 2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, 255, 255, 255, 255 },
        { 10, 3, 11, 10, 1, 3, 9, 5, 4, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, 255, 255, 255, 255 },
        { 5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, 255, 255, 255, 255 },
        { 5, 4, 8, 5, 8, 10, 10, 8, 11, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 7, 8, 5, 7, 9, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 3, 0, 9, 5, 3, 5, 7, 3, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 7, 8, 0, 1, 7, 1, 5, 7, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 5, 3, 3, 5, 7, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 7, 8, 9, 5, 7, 10, 1, 2, 255, 255, 255, 255, 255, 255, 255 },
        { 10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, 255, 255, 255, 255 },
        { 8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, 255, 255, 255, 255 },
        { 2, 10, 5, 2, 5, 3, 3, 5, 7, 255, 255, 255, 255, 255, 255, 255 },
        { 7, 9, 5, 7, 8, 9, 3, 11, 2, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, 255, 255, 255, 255 },
        { 2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, 255, 255, 255, 255 },
        { 11, 2, 1, 11, 1, 7, 7, 1, 5, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, 255, 255, 255, 255 },
        { 5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, 255 },
        { 11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, 255 },
        { 11, 10, 5, 7, 11, 5, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 10, 6, 5, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 8, 3, 5, 10, 6, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 0, 1, 5, 10, 6, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 8, 3, 1, 9, 8, 5, 10, 6, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 6, 5, 2, 6, 1, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 6, 5, 1, 2, 6, 3, 0, 8, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 6, 5, 9, 0, 6, 0, 2, 6, 255, 255, 255, 255, 255, 255, 255 },
        { 5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, 255, 255, 255, 255 },
        { 2, 3, 11, 10, 6, 5, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 11, 0, 8, 11, 2, 0, 10, 6, 5, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 1, 9, 2, 3, 11, 5, 10, 6, 255, 255, 255, 255, 255, 255, 255 },
        { 5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, 255, 255, 255, 255 },
        { 6, 3, 11, 6, 5, 3, 5, 1, 3, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, 255, 255, 255, 255 },
        { 3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, 255, 255, 255, 255 },
        { 6, 5, 9, 6, 9, 11, 11, 9, 8, 255, 255, 255, 255, 255, 255, 255 },
        { 5, 10, 6, 4, 7, 8, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 3, 0, 4, 7, 3, 6, 5, 10, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 9, 0, 5, 10, 6, 8, 4, 7, 255, 255, 255, 255, 255, 255, 255 },
        { 10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, 255, 255, 255, 255 },
        { 6, 1, 2, 6, 5, 1, 4, 7, 8, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, 255, 255, 255, 255 },
        { 8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, 255, 255, 255, 255 },
        { 7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, 255 },
        { 3, 11, 2, 7, 8, 4, 10, 6, 5, 255, 255, 255, 255, 255, 255, 255 },
        { 5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, 255, 255, 255, 255 },
        { 0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, 255, 255, 255, 255 },
        { 9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, 255 },
        { 8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, 255, 255, 255, 255 },
        { 5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, 255 },
        { 0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, 255 },
        { 6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, 255, 255, 255, 255 },
        { 10, 4, 9, 6, 4, 10, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 10, 6, 4, 9, 10, 0, 8, 3, 255, 255, 255, 255, 255, 255, 255 },
        { 10, 0, 1, 10, 6, 0, 6, 4, 0, 255, 255, 255, 255, 255, 255, 255 },
        { 8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, 255, 255, 255, 255 },
        { 1, 4, 9, 1, 2, 4, 2, 6, 4, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, 255, 255, 255, 255 },
        { 0, 2, 4, 4, 2, 6, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 8, 3, 2, 8, 2, 4, 4, 2, 6, 255, 255, 255, 255, 255, 255, 255 },
        { 10, 4, 9, 10, 6, 4, 11, 2, 3, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, 255, 255, 255, 255 },
        { 3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, 255, 255, 255, 255 },
        { 6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, 255 },
        { 9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, 255, 255, 255, 255 },
        { 8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, 255 },
        { 3, 11, 6, 3, 6, 0, 0, 6, 4, 255, 255, 255, 255, 255, 255, 255 },
        { 6, 4, 8, 11, 6, 8, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 7, 10, 6, 7, 8, 10, 8, 9, 10, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, 255, 255, 255, 255 },
        { 10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, 255, 255, 255, 255 },
        { 10, 6, 7, 10, 7, 1, 1, 7, 3, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, 255, 255, 255, 255 },
        { 2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, 255 },
        { 7, 8, 0, 7, 0, 6, 6, 0, 2, 255, 255, 255, 255, 255, 255, 255 },
        { 7, 3, 2, 6, 7, 2, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, 255, 255, 255, 255 },
        { 2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, 255 },
        { 1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, 255 },
        { 11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, 255, 255, 255, 255 },
        { 8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, 255 },
        { 0, 9, 1, 11, 6, 7, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, 255, 255, 255, 255 },
        { 7, 11, 6, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 7, 6, 11, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 0, 8, 11, 7, 6, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 1, 9, 11, 7, 6, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 8, 1, 9, 8, 3, 1, 11, 7, 6, 255, 255, 255, 255, 255, 255, 255 },
        { 10, 1, 2, 6, 11, 7, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 2, 10, 3, 0, 8, 6, 11, 7, 255, 255, 255, 255, 255, 255, 255 },
        { 2, 9, 0, 2, 10, 9, 6, 11, 7, 255, 255, 255, 255, 255, 255, 255 },
        { 6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, 255, 255, 255, 255 },
        { 7, 2, 3, 6, 2, 7, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 7, 0, 8, 7, 6, 0, 6, 2, 0, 255, 255, 255, 255, 255, 255, 255 },
        { 2, 7, 6, 2, 3, 7, 0, 1, 9, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, 255, 255, 255, 255 },
        { 10, 7, 6, 10, 1, 7, 1, 3, 7, 255, 255, 255, 255, 255, 255, 255 },
        { 10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, 255, 255, 255, 255 },
        { 0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, 255, 255, 255, 255 },
        { 7, 6, 10, 7, 10, 8, 8, 10, 9, 255, 255, 255, 255, 255, 255, 255 },
        { 6, 8, 4, 11, 8, 6, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 6, 11, 3, 0, 6, 0, 4, 6, 255, 255, 255, 255, 255, 255, 255 },
        { 8, 6, 11, 8, 4, 6, 9, 0, 1, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, 255, 255, 255, 255 },
        { 6, 8, 4, 6, 11, 8, 2, 10, 1, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, 255, 255, 255, 255 },
        { 4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, 255, 255, 255, 255 },
        { 10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, 255 },
        { 8, 2, 3, 8, 4, 2, 4, 6, 2, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 4, 2, 2, 4, 6, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, 255, 255, 255, 255 },
        { 1, 9, 4, 1, 4, 2, 2, 4, 6, 255, 255, 255, 255, 255, 255, 255 },
        { 8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, 255, 255, 255, 255 },
        { 10, 1, 0, 10, 0, 6, 6, 0, 4, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, 255 },
        { 10, 9, 4, 6, 10, 4, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 9, 5, 7, 6, 11, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 8, 3, 4, 9, 5, 11, 7, 6, 255, 255, 255, 255, 255, 255, 255 },
        { 5, 0, 1, 5, 4, 0, 7, 6, 11, 255, 255, 255, 255, 255, 255, 255 },
        { 11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, 255, 255, 255, 255 },
        { 9, 5, 4, 10, 1, 2, 7, 6, 11, 255, 255, 255, 255, 255, 255, 255 },
        { 6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, 255, 255, 255, 255 },
        { 7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, 255, 255, 255, 255 },
        { 3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, 255 },
        { 7, 2, 3, 7, 6, 2, 5, 4, 9, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, 255, 255, 255, 255 },
        { 3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, 255, 255, 255, 255 },
        { 6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, 255 },
        { 9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, 255, 255, 255, 255 },
        { 1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, 255 },
        { 4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, 255 },
        { 7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, 255, 255, 255, 255 },
        { 6, 9, 5, 6, 11, 9, 11, 8, 9, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, 255, 255, 255, 255 },
        { 0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, 255, 255, 255, 255 },
        { 6, 11, 3, 6, 3, 5, 5, 3, 1, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, 255, 255, 255, 255 },
        { 0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, 255 },
        { 11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, 255 },
        { 6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, 255, 255, 255, 255 },
        { 5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, 255, 255, 255, 255 },
        { 9, 5, 6, 9, 6, 0, 0, 6, 2, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, 255 },
        { 1, 5, 6, 2, 1, 6, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, 255 },
        { 10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, 255, 255, 255, 255 },
        { 0, 3, 8, 5, 6, 10, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 10, 5, 6, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 11, 5, 10, 7, 5, 11, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 11, 5, 10, 11, 7, 5, 8, 3, 0, 255, 255, 255, 255, 255, 255, 255 },
        { 5, 11, 7, 5, 10, 11, 1, 9, 0, 255, 255, 255, 255, 255, 255, 255 },
        { 10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, 255, 255, 255, 255 },
        { 11, 1, 2, 11, 7, 1, 7, 5, 1, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, 255, 255, 255, 255 },
        { 9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, 255, 255, 255, 255 },
        { 7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, 255 },
        { 2, 5, 10, 2, 3, 5, 3, 7, 5, 255, 255, 255, 255, 255, 255, 255 },
        { 8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, 255, 255, 255, 255 },
        { 9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, 255, 255, 255, 255 },
        { 9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, 255 },
        { 1, 3, 5, 5, 3, 7, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 8, 7, 0, 7, 1, 1, 7, 5, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 0, 3, 9, 3, 5, 5, 3, 7, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 8, 7, 5, 9, 7, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 5, 8, 4, 5, 10, 8, 10, 11, 8, 255, 255, 255, 255, 255, 255, 255 },
        { 5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, 255, 255, 255, 255 },
        { 0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, 255, 255, 255, 255 },
        { 10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, 255 },
        { 2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, 255, 255, 255, 255 },
        { 0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, 255 },
        { 0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, 255 },
        { 9, 4, 5, 2, 11, 3, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, 255, 255, 255, 255 },
        { 5, 10, 2, 5, 2, 4, 4, 2, 0, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, 255 },
        { 5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, 255, 255, 255, 255 },
        { 8, 4, 5, 8, 5, 3, 3, 5, 1, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 4, 5, 1, 0, 5, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, 255, 255, 255, 255 },
        { 9, 4, 5, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 11, 7, 4, 9, 11, 9, 10, 11, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, 255, 255, 255, 255 },
        { 1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, 255, 255, 255, 255 },
        { 3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, 255 },
        { 4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, 255, 255, 255, 255 },
        { 9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, 255 },
        { 11, 7, 4, 11, 4, 2, 2, 4, 0, 255, 255, 255, 255, 255, 255, 255 },
        { 11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, 255, 255, 255, 255 },
        { 2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, 255, 255, 255, 255 },
        { 9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, 255 },
        { 3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, 255 },
        { 1, 10, 2, 8, 7, 4, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 9, 1, 4, 1, 7, 7, 1, 3, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, 255, 255, 255, 255 },
        { 4, 0, 3, 7, 4, 3, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 4, 8, 7, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 10, 8, 8, 10, 11, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 0, 9, 3, 9, 11, 11, 9, 10, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 1, 10, 0, 10, 8, 8, 10, 11, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 1, 10, 11, 3, 10, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 2, 11, 1, 11, 9, 9, 11, 8, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, 255, 255, 255, 255 },
        { 0, 2, 11, 8, 0, 11, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 3, 2, 11, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 2, 3, 8, 2, 8, 10, 10, 8, 9, 255, 255, 255, 255, 255, 255, 255 },
        { 9, 10, 2, 0, 9, 2, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, 255, 255, 255, 255 },
        { 1, 10, 2, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 1, 3, 8, 9, 1, 8, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 9, 1, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        { 0, 3, 8, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 },
        {255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 }
        };

        public static byte[] cornerIndexAFromEdge = {
        0,
        1,
        5,
        4,
        2,
        3,
        7,
        6,
        0,
        1,
        5,
        4
    };

        public static byte[] cornerIndexBFromEdge = {
        1,
        5,
        4,
        0,
        3,
        7,
        6,
        2,
        2,
        3,
        7,
        6
    };
    }
}