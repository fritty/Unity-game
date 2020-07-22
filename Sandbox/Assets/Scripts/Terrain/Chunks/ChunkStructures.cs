using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.ProceduralTerrain.Core
{
    // Main structure for holding chunks
    public struct ChunksDictionary
    {    
        readonly int _worldHeight;

        Dictionary<Vector2Int, ChunkColumn> _columns;
        Queue<ChunkColumn> _columnsForRecycling;
        
        public ChunksDictionary(ChunksManager manager)
        {
            _columns = new Dictionary<Vector2Int, ChunkColumn>();
            _columnsForRecycling = new Queue<ChunkColumn>();
            _worldHeight = manager.Settings.WorldHeight;
            CreateChunksPool(manager);
        }

        public bool TryGetValue(Vector3Int coord, out Chunk chunk)
        {               
            if (coord.y >= 0 && coord.y < _worldHeight)
                if (_columns.TryGetValue(new Vector2Int(coord.x, coord.z), out ChunkColumn column))
                {
                    chunk = column.Chunks[coord.y];
                    return true;
                }

            chunk = null;
            return false;
        }
        public bool TryGetValue(Vector2Int coord, out ChunkColumn column) => _columns.TryGetValue(coord, out column);

        public bool ContainsKey(Vector3Int coord) => (_columns.ContainsKey(new Vector2Int(coord.x, coord.z)) && coord.y >= 0 && coord.y < _worldHeight);
        public bool ContainsKey(Vector2Int coord) => _columns.ContainsKey(coord);

        public Dictionary<Vector2Int, ChunkColumn>.KeyCollection Coords() => _columns.Keys;
        public Dictionary<Vector2Int, ChunkColumn>.ValueCollection Values() => _columns.Values;

        public void AddColumn(GeneratedDataInfo<MapData[]> mapData)
        {
            ChunkColumn column = _columnsForRecycling.Dequeue();
            column.SetMapData(mapData);
            _columns.Add(mapData.coord.XZ(), column);
        }

        public void AddColumnMesh(GeneratedDataInfo<MeshData[]> meshData)
        {               
            if (_columns.TryGetValue(meshData.coord.XZ(), out ChunkColumn column))
                column.SetMeshData(meshData);
        }

        public void RecycleChunkColumn(Vector2Int coord)
        {               
            if (!_columns.TryGetValue(coord, out ChunkColumn column)) return;

            //column.Deactivate();
            _columns.Remove(coord);
            _columnsForRecycling.Enqueue(column);
        }
        public void RecycleChunkColumn(int coodX, int coordZ) => RecycleChunkColumn(new Vector2Int(coodX, coordZ));

        public void SetVisibility(Vector2Int coord, bool visibility)
        {
            if (_columns.TryGetValue(coord, out ChunkColumn column))
                column.SetVisibility(visibility);
        }

        private void CreateChunksPool(ChunksManager manager)
        {
            for (int x = -manager.Settings.GenerationDistance; x <= manager.Settings.GenerationDistance; x++)
                for (int z = -manager.Settings.GenerationDistance; z <= manager.Settings.GenerationDistance; z++)
                {
                    _columnsForRecycling.Enqueue(new ChunkColumn(manager));
                }
        }
    }

    // Structure for managing column of chunks as a whole
    public struct ChunkColumn
    {
        public Chunk[] Chunks;
        public Vector2Int Coord;
        public bool IsActive { get; private set; }
        public bool IsVisible { get; private set; }//{ get; private set; }
        public bool IsWaitingMesh { get; set; }
        public bool HasMesh { get; private set; }

        readonly int _worldHeight;

        public ChunkColumn(ChunksManager manager)
        {
            _worldHeight = manager.Settings.WorldHeight;
            Chunks = new Chunk[_worldHeight];
            Coord = new Vector2Int();
            IsActive = IsVisible = IsWaitingMesh = HasMesh = false;

            for (int i = 0; i < _worldHeight; i++)
            {
                Chunks[i] = CreateChunk(manager);
                Chunks[i].Visibility = false;
            }
        }

        public void SetMapData(GeneratedDataInfo<MapData[]> mapData)
        {
            Coord = new Vector2Int(mapData.coord.x, mapData.coord.z);
            for (int i = 0; i < _worldHeight; i++)
            {
                Chunks[i].SetBlocks(mapData.data[i].blocks);
                Chunks[i].SetCoord(new Vector3Int(mapData.coord.x, i, mapData.coord.z));
            }
            HasMesh = false;
        }

        public void SetMeshData(GeneratedDataInfo<MeshData[]> meshData)
        {
            for (int i = 0; i < _worldHeight; i++)
            {
                Chunks[i].SetMesh(meshData.data[i]);
            }
            HasMesh = true;
        }

        public void SetVisibility(bool visibility)
        {
            for (int i = 0; i < _worldHeight; i++)
            {
                Chunks[i].Visibility = visibility;
            }
            IsVisible = visibility;
        }

        //public void Deactivate()
        //{
        //    SetVisibility(false);
        //    isActive = false;
        //}

        private Chunk CreateChunk(ChunksManager manager)
        {
            GameObject chunk = new GameObject();
            chunk.transform.parent = manager.transform;
            chunk.tag = "Chunk";
            chunk.isStatic = true;
            Chunk newChunk = chunk.AddComponent<Chunk>();
            newChunk.Create(manager.Settings.MeshGeneratorSettings.GenerateColliders, manager.Settings.MeshGeneratorSettings.Material);
            return newChunk;
        }
    }

    // Contains order in which chunks are generated relative to the viewer
    public struct GenerationOrder
    {
        readonly int[,] _order;
        readonly ChunksManager _manager;

        public GenerationOrder(ChunksManager manager)
        {
            this._manager = manager;
            int width = (manager.Settings.GenerationDistance) * 2 + 1;
            _order = new int[width, width];

            int index = 0;
            this[0, 0] = index++;
            for (int i = 1; i <= manager.Settings.GenerationDistance; i++)
            {
                this[i, 0] = index++;
                this[0, -i] = index++;
                this[-i, 0] = index++;
                this[0, i] = index++;
                for (int j = 1; j < i; j++)
                {
                    this[i, j] = index++;
                    this[i, -j] = index++;
                    this[j, -i] = index++;
                    this[-j, -i] = index++;
                    this[-i, -j] = index++;
                    this[-i, j] = index++;
                    this[-j, i] = index++;
                    this[j, i] = index++;
                }
                this[i, i] = index++;
                this[i, -i] = index++;
                this[-i, -i] = index++;
                this[-i, i] = index++;
            }
        }

        // Comparator for sorting
        public int Comparison(Vector2Int x, Vector2Int y)
        {
            return (this[x - _manager.ViewerCoord] - this[y - _manager.ViewerCoord]);
        }

        public int this[Vector2Int relativeCoord]
        {
            get
            {
                return _order[relativeCoord.y + _manager.Settings.GenerationDistance, relativeCoord.x + _manager.Settings.GenerationDistance];
            }
            private set
            {
                _order[relativeCoord.y + _manager.Settings.GenerationDistance, relativeCoord.x + _manager.Settings.GenerationDistance] = value;
            }
        }

        public int this[int relativeCoordX, int relativeCoordZ]
        {
            get
            {
                return _order[relativeCoordX + _manager.Settings.GenerationDistance, relativeCoordZ + _manager.Settings.GenerationDistance];
            }
            private set
            {
                _order[relativeCoordX + _manager.Settings.GenerationDistance, relativeCoordZ + _manager.Settings.GenerationDistance] = value;
            }
        }
    }

    // for passing generated data
    public struct GeneratedDataInfo<T>
    {
        public readonly T data;
        public readonly Vector3Int coord;

        public GeneratedDataInfo(T data, Vector3Int coord)
        {
            this.coord = coord;
            this.data = data;
        }
    }

    public struct MeshData
    {
        public Vector3[] Vertices;
        public int[] Triangles;

        int triangleIndex;

        public MeshData(int numTris)
        {
            Vertices = new Vector3[(numTris) * 3];
            Triangles = new int[(numTris) * 3];
            triangleIndex = 0;
        }

        public void AddTriangle(Triangle tri)
        {
            Vertices[triangleIndex] = tri[0];
            Vertices[triangleIndex + 1] = tri[1];
            Vertices[triangleIndex + 2] = tri[2];
            Triangles[triangleIndex] = triangleIndex;
            Triangles[triangleIndex + 1] = triangleIndex + 1;
            Triangles[triangleIndex + 2] = triangleIndex + 2;
            triangleIndex += 3;
        }

        public Mesh CreateMesh()
        {
            Mesh mesh = new Mesh();
            mesh.vertices = Vertices;
            mesh.triangles = Triangles;
            mesh.RecalculateNormals();
            return mesh;
        }
    }

    // for chunk terrain data
    public struct MapData
    {
        public readonly byte[] blocks;

        public MapData(byte[] blocks)
        {
            this.blocks = blocks;
        }
    }
}