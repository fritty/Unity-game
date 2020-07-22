using UnityEngine;
using Sandbox.ProceduralTerrain.Core;

namespace Sandbox.Editing
{
    public class TerrainPreview : MonoBehaviour
    {
        const int maxGenerationDistance = 32;
        [Range(1, 16)]
        public int WorldHeight_ = 8;
        [Range(0, maxGenerationDistance)]
        public int GenerationDistance_ = 8;
        public Vector2Int ChunkOffset_ = new Vector2Int();

        public TerrainSettings Settings_;

        public bool AutoUpdate_;

        HeightMapGenerator _heightMapGenerator;

        int _generationWidth;
        int _maxGenerationWidth;

        int _oldGenerationDistance;

        bool _sizeChanged;
        bool _verticesChanged;

        const int trianglesWidth = ChunkSize.Width;
        const int verticesWigth = (ChunkSize.Width + 1);

        RenderObject[,] _chunks;

        public void Redraw()
        {
            if (_sizeChanged)
            {
                FullChange();
                _sizeChanged = false;
            }
            else if (Settings_.MapGeneratorSettings.IsChanged || Settings_.MapGeneratorSettings.HeightMapSettings.IsChanged)
            {
                _heightMapGenerator.UpdateSettings();
                DrawVertices();
                Settings_.MapGeneratorSettings.IsChanged = false;
                Settings_.MapGeneratorSettings.HeightMapSettings.IsChanged = false;
                _verticesChanged = false;
            }
            else if (_verticesChanged)
            {
                DrawVertices();
                _verticesChanged = false;
            }
        }

        private void Awake()
        {
            _heightMapGenerator = new HeightMapGenerator(Settings_.MapGeneratorSettings.HeightMapSettings);
            _maxGenerationWidth = maxGenerationDistance * 2 + 1;
            _chunks = new RenderObject[_maxGenerationWidth, _maxGenerationWidth];

            for (int z = 0; z < _maxGenerationWidth; z++)
                for (int x = 0; x < _maxGenerationWidth; x++)
                {
                    GameObject chunk = new GameObject();
                    chunk.transform.parent = transform;
                    //chunk.isStatic = true;
                    _chunks[z, x] = chunk.AddComponent<RenderObject>();
                    _chunks[z, x].MeshRenderer.sharedMaterial = Settings_.MeshGeneratorSettings.Material;
                    _chunks[z, x].Mesh.MarkDynamic();                         
                }
        }

        private void Start()
        {
            if (this.enabled)
            {
                FullChange();

                _sizeChanged = false;
                _verticesChanged = false;
                Settings_.MapGeneratorSettings.IsChanged = false;
            }
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                if (_oldGenerationDistance != GenerationDistance_)
                    _sizeChanged = true;
                else
                    _verticesChanged = true;
            }
        }

        private void Update()
        {
            if (AutoUpdate_)
            {
                Redraw();
            }
            Settings_.UpdateColors();
        }

        private void FullChange()
        {
            ChangeSize();
            GenerateVertices();
            GenerateTriangles();
            RecalculateNormals();
        }

        private void DrawVertices()
        {
            GenerateVertices();
            RecalculateNormals();
        }

        private void ChangeSize()
        {
            _oldGenerationDistance = GenerationDistance_;
            _generationWidth = (GenerationDistance_ * 2 + 1);

            for (int z = 0; z < _maxGenerationWidth; z++)
                for (int x = 0; x < _maxGenerationWidth; x++)
                {
                    _chunks[z, x].Mesh.Clear();
                    _chunks[z, x].MeshRenderer.enabled = (z < _generationWidth && x < _generationWidth);
                    _chunks[z, x].transform.position = new Vector3((x - GenerationDistance_) * ChunkSize.Width, 0, (z - GenerationDistance_) * ChunkSize.Width);
                }
        }
        private void GenerateVertices()
        {               
            Vector2Int chunkCoord = new Vector2Int();
            for (chunkCoord.y = 0; chunkCoord.y < _generationWidth; chunkCoord.y++)
                for (chunkCoord.x = 0; chunkCoord.x < _generationWidth; chunkCoord.x++)
                {
                    HeightMap heightMap = _heightMapGenerator.CreateHeightMap(chunkCoord.Plus(ChunkOffset_.x - GenerationDistance_, ChunkOffset_.y - GenerationDistance_));

                    Vector3[] chunkVertices = new Vector3[verticesWigth * verticesWigth];
                    for (int z = 0; z < verticesWigth; z++)
                    {
                        for (int x = 0; x < verticesWigth; x++)
                        {
                            chunkVertices[x + z * (ChunkSize.Width + 1)] =  new Vector3(x, Mathf.Clamp(heightMap[z, x], 0, WorldHeight_ * ChunkSize.Height), z);
                        }
                    }
                    _chunks[chunkCoord.y, chunkCoord.x].Mesh.vertices = chunkVertices;
                }
        }

        private void GenerateTriangles()
        {               
            Vector2Int chunkCoord = new Vector2Int();
            for (chunkCoord.y = 0; chunkCoord.y < _generationWidth; chunkCoord.y++)
            {
                for (chunkCoord.x = 0; chunkCoord.x < _generationWidth; chunkCoord.x++)
                {
                    int[] chunkTriangles = new int[6 * (trianglesWidth) * (trianglesWidth)];

                    int i = 0;
                    for (int z = 0; z < trianglesWidth; z++)
                    {
                        int z0 = z * (verticesWigth), z1 = (z + 1) * (verticesWigth);
                        for (int x = 0; x < trianglesWidth; x++)
                        {
                            chunkTriangles[i] = x + z0;
                            chunkTriangles[i + 1] = x + z1;
                            chunkTriangles[i + 2] = x + 1 + z0;
                            chunkTriangles[i + 3] = chunkTriangles[i + 2];
                            chunkTriangles[i + 4] = chunkTriangles[i + 1];
                            chunkTriangles[i + 5] = x + 1 + z1;

                            i += 6;
                        }
                    }
                    _chunks[chunkCoord.y, chunkCoord.x].Mesh.triangles = chunkTriangles;
                }
            }
        }

        private void RecalculateNormals()
        {
            for (int z = 0; z < _generationWidth; z++)
                for (int x = 0; x < _generationWidth; x++)
                    _chunks[z, x].Mesh.RecalculateNormals();
        }
    }
}