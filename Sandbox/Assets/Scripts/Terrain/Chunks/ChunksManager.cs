using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.ProceduralTerrain.Core
{
    /* Manages chunks structures and data */
    public abstract class ChunksManager : MonoBehaviour
    {
        [Header("References")]
        public TerrainSettings Settings;
        [SerializeField]
        private Transform _viewer;

        public Vector2Int ViewerCoord { get; private set; }

        protected ChunksDictionary ExistingChunks { get; private set; }
        protected int WorldHeight;
        protected int GenerationDistance;
        protected int ViewDistance;

        MapGenerator _mapGenerator;
        CpuMeshGenerator _meshGenerator;
        GenerationOrder _generationOrder;

        Camera cameram; ///

        protected virtual void Awake()
        {
            SetVariables();
            cameram = Camera.main;
        }

        protected virtual void Start()
        {
            GetVariables();
            InitialGeneration();
        }

        protected void OnDestroy()
        {
            _mapGenerator.Destroy();
            _meshGenerator.Destroy();
        }

        private void LateUpdate()
        {
            _meshGenerator.ManageColumnRequests();
            Settings.UpdateColors();
        }

        private void FixedUpdate()
        {
            RequestChunks();
            _mapGenerator.ManageRequests();
            _meshGenerator.ManageChunkRequests();
        }

        // Requests single chunk mesh update 
        protected void MarkForMeshGeneration(Chunk chunk)
        {
            if (chunk.IsWaitingMesh)
                chunk.IsDirty = (true);
            else
            {
                chunk.IsWaitingMesh = (true);
                RequestMesh(chunk);
            }
        }

        private void SetVariables()
        {
            if (Settings == null)
                Settings = FindObjectOfType<TerrainSettings>();

            WorldHeight = Settings.WorldHeight;
            GenerationDistance = Settings.GenerationDistance;
            ViewDistance = Settings.ViewDistance;

            ExistingChunks = new ChunksDictionary(this);
            _generationOrder = new GenerationOrder(this);

            _mapGenerator = new MapGenerator(OnMapDataReceived, Settings);
            _meshGenerator = new CpuMeshGenerator(OnColumnMeshDataRecieved, OnChunkMeshDataReceived, Settings);

            if (_viewer == null)
                _viewer = Camera.main.transform;
        }

        private void GetVariables()
        {
            ViewerCoord = _viewer.position.ToChunkCoord().XZ();
        }

        private void InitialGeneration()
        {
            List<Vector2Int> forGeneration = new List<Vector2Int>();

            for (int x = -GenerationDistance; x <= GenerationDistance; x++)
                for (int z = -GenerationDistance; z <= GenerationDistance; z++)
                    forGeneration.Add(ViewerCoord.Plus(x, z));

            FormMapRequests(forGeneration);
        }

        /* Create/destroy chunks based on generation distance */
        private void RequestChunks()
        {
            Vector2Int currentCoord = _viewer.position.XZ().ToChunkCoord();

            // Go through world bound difference and delete/mark for generation
            if (ViewerCoord != currentCoord) // only if viewer coord is changed
            {
                Vector2Int previousCoord = ViewerCoord;

                List<Vector2Int> forGeneration = new List<Vector2Int>();

                Vector2Int[] regionX = new Vector2Int[2];
                Vector2Int[] regionZ = new Vector2Int[2];

                // regions for recalculation
                regionX[0] = new Vector2Int(currentCoord.x > previousCoord.x ? previousCoord.x - GenerationDistance
                                                                             : Mathf.Max(currentCoord.x + GenerationDistance + 1, previousCoord.x - GenerationDistance),
                                            previousCoord.y - GenerationDistance);
                regionX[1] = new Vector2Int(currentCoord.x > previousCoord.x ? Mathf.Min(currentCoord.x - GenerationDistance, previousCoord.x + GenerationDistance + 1)
                                                                             : previousCoord.x + GenerationDistance + 1,
                                            previousCoord.y + GenerationDistance + 1);

                regionZ[0] = new Vector2Int(Mathf.Max(currentCoord.x - GenerationDistance, previousCoord.x - GenerationDistance),
                                            currentCoord.y > previousCoord.y ? previousCoord.y - GenerationDistance
                                                                             : Mathf.Max(currentCoord.y + GenerationDistance + 1, previousCoord.y - GenerationDistance));
                regionZ[1] = new Vector2Int(Mathf.Min(currentCoord.x + GenerationDistance + 1, previousCoord.x + GenerationDistance + 1),
                                            currentCoord.y > previousCoord.y ? Mathf.Min(currentCoord.y - GenerationDistance, previousCoord.y + GenerationDistance + 1)
                                                                             : previousCoord.y + GenerationDistance + 1);

                // loop through old regions and mark them for regeneration as new
                for (int x = regionX[0].x; x < regionX[1].x; x++)
                {
                    for (int z = regionX[0].y; z < regionX[1].y; z++)
                    {
                        ExistingChunks.RecycleChunkColumn(x, z);

                        if (Mathf.Abs(currentCoord.x - previousCoord.x) < 2 * GenerationDistance + 1)
                        {
                            if (currentCoord.x > previousCoord.x)
                                forGeneration.Add(new Vector2Int(x + 2 * GenerationDistance + 1, z + currentCoord.y - previousCoord.y));
                            else
                                forGeneration.Add(new Vector2Int(x - 2 * GenerationDistance - 1, z + currentCoord.y - previousCoord.y));
                        }
                        else
                        {
                            forGeneration.Add(new Vector2Int(x + currentCoord.x - previousCoord.x, z + currentCoord.y - previousCoord.y));
                        }
                    }
                }
                for (int z = regionZ[0].y; z < regionZ[1].y; z++)
                {
                    for (int x = regionZ[0].x; x < regionZ[1].x; x++)
                    {
                        ExistingChunks.RecycleChunkColumn(x, z);

                        if (Mathf.Abs(currentCoord.y - previousCoord.y) < 2 * GenerationDistance + 1)
                        {
                            if (currentCoord.y > previousCoord.y)
                                forGeneration.Add(new Vector2Int(x, z + 2 * GenerationDistance + 1));
                            else
                                forGeneration.Add(new Vector2Int(x, z - 2 * GenerationDistance - 1));
                        }
                        else
                        {
                            forGeneration.Add(new Vector2Int(x, z + currentCoord.y - previousCoord.y));
                        }
                    }
                }

                ViewerCoord = currentCoord;
                FormMapRequests(forGeneration);
                SortMeshRequests();
                ToggleVisibility();
            }
        }

        private void ToggleVisibility ()
        {
            //Vector3 size = new Vector3(ChunkSize.Width, ChunkSize.Height * Settings.WorldHeight, ChunkSize.Width);
            for (int z = -GenerationDistance; z < GenerationDistance; z++)
                for (int x = -GenerationDistance; x < GenerationDistance; x++)
                {
                    Vector2Int coord = ViewerCoord.Plus(x, z);
                    //Vector3 center = coord.ToChunkOrigin().X0Y() + size / 2f;
                    //if (IsVisibleFrom(new Bounds(center, size), cameram))
                    ExistingChunks.SetVisibility(coord, IsVisible(coord));
                    //else
                    //    ExistingChunks.SetVisibility(coord, false);
                }
        }

        private bool IsVisibleFrom(Bounds bounds, Camera camera)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            return GeometryUtility.TestPlanesAABB(planes, bounds);
        }

        private void FormMapRequests(List<Vector2Int> forGeneration)
        {
            Queue<Vector2Int> remainingRequests = _mapGenerator.GetRequests();

            forGeneration.AddRange(FilterRequests(remainingRequests));
            forGeneration.Sort(_generationOrder.Comparison);

            _mapGenerator.ReplaceRequests(forGeneration);
        }

        private void SortMeshRequests()
        {
            Queue<Vector2Int> remainingRequests = FilterRequests(_meshGenerator.GetColumnRequests());

            if (remainingRequests.Count > 1)
            {
                List<Vector2Int> newRequests = new List<Vector2Int>(remainingRequests);
                newRequests.Sort(_generationOrder.Comparison);
                _meshGenerator.ReplaceColumnRequests(newRequests);
            }
        }

        private Queue<Vector2Int> FilterRequests(Queue<Vector2Int> remainingRequests)
        {
            Queue<Vector2Int> result = new Queue<Vector2Int>();
            while (remainingRequests.Count > 0)
            {
                Vector2Int coord = remainingRequests.Dequeue();
                if (IsInsideBorders(coord))
                    result.Enqueue(coord);
            }
            return result;
        }

        // Recieves map data from generator, assignes it for recycled chunk and triggers mesh generation
        private void OnMapDataReceived(GeneratedDataInfo<MapData[]> mapData)
        {
            Vector2Int coord = new Vector2Int(mapData.coord.x, mapData.coord.z);
            if (IsInsideBorders(coord) && !ExistingChunks.ContainsKey(coord))
            {
                ExistingChunks.AddColumn(mapData);

                TriggerMeshGeneration(coord);
            }
        }

        // Recieves mesh from generator and requests another one if chunk is dirty
        private void OnChunkMeshDataReceived(GeneratedDataInfo<MeshData> meshData)
        {
            if (ExistingChunks.TryGetValue(meshData.coord, out Chunk chunk))
            {
                chunk.SetMesh(meshData.data);

                if (chunk.IsDirty)
                {
                    RequestMesh(chunk);
                    chunk.IsDirty = (false);
                }
                else
                    chunk.IsWaitingMesh = (false);
            }
        }

        private void OnColumnMeshDataRecieved(GeneratedDataInfo<MeshData[]> meshData)
        {
            Vector2Int coord = meshData.coord.XZ();
            if (ExistingChunks.TryGetValue(coord, out ChunkColumn column))
            {
                column.SetMeshData(meshData);
                column.SetVisibility(IsVisible(coord));
            }
        }

        // Requests meshes for all adjacent columns
        private void TriggerMeshGeneration(Vector2Int coord)
        {
            Vector2Int requestCoord = new Vector2Int();
            for (requestCoord.x = coord.x - 1; requestCoord.x <= coord.x + 1; requestCoord.x++)
                for (requestCoord.y = coord.y - 1; requestCoord.y <= coord.y + 1; requestCoord.y++)
                    RequestMeshColumn(requestCoord);
        }

        private void RequestMeshColumn(Vector2Int requestedCoord)
        {
            if (!ExistingChunks.TryGetValue(requestedCoord, out ChunkColumn column)) return;
            if (column.HasMesh || column.IsWaitingMesh) return;

            bool canGenerate = true;
            Vector2Int coord = new Vector2Int();
            for (coord.x = requestedCoord.x - 1; coord.x <= requestedCoord.x + 1; coord.x++)
                for (coord.y = requestedCoord.y - 1; coord.y <= requestedCoord.y + 1; coord.y++)
                    if (!ExistingChunks.ContainsKey(coord)) canGenerate = false;

            if (canGenerate)
            {
                column.IsWaitingMesh = true;
                _meshGenerator.RequestColumnData(requestedCoord);
            }
        }

        // Checks if mesh is needed and can be generated before requesting it
        private void RequestMesh(Chunk chunk)
        {
            if (chunk.HasMesh && !chunk.IsWaitingMesh)
                return;

            bool generate = true;

            Vector3Int requestCoord = new Vector3Int();
            for (requestCoord.x = chunk.Coord.x - 1; requestCoord.x <= chunk.Coord.x + 1; requestCoord.x++)
                for (requestCoord.y = Mathf.Max(chunk.Coord.y - 1, 0); requestCoord.y <= Mathf.Min(chunk.Coord.y + 1, WorldHeight - 1); requestCoord.y++)
                    for (requestCoord.z = chunk.Coord.z - 1; requestCoord.z <= chunk.Coord.z + 1; requestCoord.z++)
                        if (!ExistingChunks.ContainsKey(requestCoord))
                            generate = false;

            if (generate)
                _meshGenerator.RequestChunkData(chunk.Coord);
        }

        private bool IsInsideBorders(Vector2Int coord)
        {
            return (Mathf.Abs(coord.x - ViewerCoord.x) <= GenerationDistance &&
                    Mathf.Abs(coord.y - ViewerCoord.y) <= GenerationDistance);
        }

        private bool IsVisible(Vector2Int coord)
        {
            return (Mathf.Abs(coord.x - ViewerCoord.x) <= ViewDistance &&
                    Mathf.Abs(coord.y - ViewerCoord.y) <= ViewDistance);
        }
    }
}