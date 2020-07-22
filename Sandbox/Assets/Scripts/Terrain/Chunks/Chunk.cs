using UnityEngine;
using Unity.Collections;

namespace Sandbox.ProceduralTerrain.Core
{
    public class Chunk : MonoBehaviour
    {   
        [SerializeField]
        bool _showSurfaceBlocks_ = false;

        public Vector3Int Coord { get; private set; }
        public NativeBlocksContainer Blocks => _blocks;
        public bool Visibility { get { return _meshRenderer.enabled; } set { _meshRenderer.enabled = value; } }
        public bool HasMesh { get; private set; }
        public bool IsDirty { get; set; }
        public bool IsWaitingMesh { get; set; }

        private NativeBlocksContainer _blocks;
        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _meshCollider;
        private bool _generateCollider;

        public void Create(bool generateCollider, Material material)
        {
            _blocks.Create();
            this._generateCollider = generateCollider;

            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshCollider = GetComponent<MeshCollider>();

            if (_meshFilter == null)
            {
                _meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            if (_meshRenderer == null)
            {
                _meshRenderer = gameObject.AddComponent<MeshRenderer>();                
            }
            _meshRenderer.sharedMaterial = material;

            _meshFilter.sharedMesh = _mesh = new Mesh();

            if (_meshCollider == null && generateCollider)
            {
                _meshCollider = gameObject.AddComponent<MeshCollider>();
                _meshCollider.sharedMesh = _mesh;
            }
            if (_meshCollider != null && !generateCollider)
            {
                DestroyImmediate(_meshCollider);
            }

            IsDirty = false;
        }

        private void OnDestroy()
        {
            _blocks.Dispose();
        }

        public void SetCoord(Vector3Int coord)
        {
            this.Coord = coord;
            name = $"Chunk ({coord.x}, {coord.y}, {coord.z})";
            transform.position = coord.ToChunkOrigin();
        }

        public void SetBlocks(byte[] blocks)
        {
            if (HasMesh)
            {
                _mesh.Clear();
                HasMesh = false;
            }
            if (blocks != null)
            {
                this._blocks.Native.CopyFrom(blocks);
            }
        }

        public bool ModifyBlock(Vector3Int localPosition, int value)
        {
            byte newValue = (byte)Mathf.Clamp(_blocks[localPosition.z, localPosition.y, localPosition.x] + value, 0, 255);
            if (_blocks[localPosition.z, localPosition.y, localPosition.x] != newValue)
            {
                _blocks[localPosition.z, localPosition.y, localPosition.x] = newValue;
                return true;
            }

            return false;
        }

        public void SetMesh(MeshData meshData)
        {
            int meshSize = meshData.Vertices.Length;

            if (_mesh == null)
            {
                _mesh = meshData.CreateMesh();
            }
            else
            {
                _mesh.Clear();
                if (meshSize > 0)
                {
                    _mesh.vertices = meshData.Vertices;
                    _mesh.triangles = meshData.Triangles;
                    _mesh.RecalculateNormals();
                }
            }

            if (_generateCollider)
            {
                _meshCollider.enabled = false;
                if (meshSize > 0)
                    _meshCollider.enabled = true;
            }

            HasMesh = true;
        }

        private void OnDrawGizmos()
        {
            if (_showSurfaceBlocks_)
            {
                Gizmos.color = Color.green;
                Vector3 center = Coord.ToChunkOrigin();
                for (int x = 0; x < ChunkSize.Width; x++)
                    for (int z = 0; z < ChunkSize.Width; z++)
                    {
                        bool hadBlock = _blocks[z, 0, x] > 0;
                        for (int y = 0; y < ChunkSize.Height; y++)
                        {
                            bool render = false;
                            int renderY = 0;

                            bool hasBlock = _blocks[z, y, x] > 0;

                            if (hadBlock != hasBlock)
                            {
                                renderY = y - (hadBlock ? 1 : 0);
                                render = true;
                            }
                            else if (hasBlock && _blocks[z, y, x] < 255)
                            {
                                renderY = y;
                                render = true;
                            }

                            if (render)
                            {
                                Vector3 offset = (new Vector3(x, renderY, z) + center);
                                float size = (_blocks[z, renderY, x] / 255f);

                                Vector3 up = offset.Plus(0, size, 0);
                                Vector3 forward = offset.Plus(0, 0, size);
                                Vector3 right = offset.Plus(size, 0, 0);
                                Vector3 down = offset.Plus(0, -size, 0);
                                Vector3 backward = offset.Plus(0, 0, -size);
                                Vector3 left = offset.Plus(-size, 0, 0);

                                Gizmos.DrawLine(up, right);
                                Gizmos.DrawLine(right, down);
                                Gizmos.DrawLine(down, left);
                                Gizmos.DrawLine(left, up);

                                Gizmos.DrawLine(up, forward);
                                Gizmos.DrawLine(forward, down);
                                Gizmos.DrawLine(down, backward);
                                Gizmos.DrawLine(backward, up);

                                size = .1f;
                                Gizmos.DrawWireCube(offset, Vector3.one * size);

                                hadBlock = hasBlock;
                            }
                        }
                    }
            }

            if (IsWaitingMesh)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(Coord.ToChunkOrigin() + Vector3.one * (ChunkSize.Width) / 2f, Vector3.one * ChunkSize.Width * 1.1f);
            }
        }
    }
}

public struct NativeBlocksContainer
{
    public NativeArray<byte> Native => _nativeBlocks;

    // blocks are stored in (X * Z) * Y orientation for faster partial copying
    private NativeArray<byte> _nativeBlocks;

    public byte this[int z, int y, int x]
    {
        get
        {
            return _nativeBlocks[x + z * ChunkSize.Width + y * ChunkSize.Width * ChunkSize.Width];//y * ChunkSize.width + z * ChunkSize.width * ChunkSize.height];
        }
        set
        {
            _nativeBlocks[x + z * ChunkSize.Width + y * ChunkSize.Width * ChunkSize.Width] = value;
        }
    }

    public void Create() => _nativeBlocks = new NativeArray<byte>(ChunkSize.Length, Allocator.Persistent);
    public void Dispose() => _nativeBlocks.Dispose();
}