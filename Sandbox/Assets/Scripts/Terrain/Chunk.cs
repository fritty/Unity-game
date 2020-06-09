using UnityEngine;
using Unity.Collections;

public class Chunk : MonoBehaviour {
    public struct size
    {
        public const int width = 32;
        public const int height = 32;
    }

    [SerializeField]
    bool showSurfaceBlocks = false;

    [HideInInspector]
    public Vector3Int coord { get; private set; }
    [HideInInspector]
    public byte[,,] blocks { get; private set; }
    [HideInInspector]
    public bool hasMesh { get; private set; }
    [HideInInspector]
    public bool isDirty { get; private set; }
    [HideInInspector]
    public bool isWaitingMesh { get; private set; }


    Mesh mesh;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    MeshCollider meshCollider;
    bool generateCollider;


    public void Create (bool generateCollider, Material material)
    {
        this.generateCollider = generateCollider;

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();

        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.material = material;
        }

        if (meshCollider == null && generateCollider)
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }
        if (meshCollider != null && !generateCollider)
        {
            DestroyImmediate(meshCollider);
        }

        isDirty = false;
    }  

    public void SetCoord (Vector3Int coord) {        
        this.coord = coord;
        name = $"Chunk ({coord.x}, {coord.y}, {coord.z})";
        if (hasMesh)
        {
            mesh.Clear();
            hasMesh = false;
        }
        transform.position = ProTerra.ChunkOriginFromCoord(coord);
    }

    public void SetBlocks (byte[,,] blocks) {
        if (blocks != null)
            this.blocks = blocks;
    }

    public bool ModifyBlock (Vector3Int localPosition, int value)
    {
        if (blocks != null)
        {
            byte newValue = (byte)Mathf.Clamp(blocks[localPosition.z, localPosition.y, localPosition.x] + value, 0, 255);
            if (blocks[localPosition.z, localPosition.y, localPosition.x] != newValue)
            {
                blocks[localPosition.z, localPosition.y, localPosition.x] = newValue;
                return true;
            }
        }

        return false;
    }

    public void SetMesh (MeshData meshData) {

        if (mesh == null)
        {
            mesh = meshFilter.sharedMesh = meshData.CreateMesh();            
        }
        else
        {
            mesh.Clear();
            mesh.vertices = meshData.vertices;
            mesh.triangles = meshData.triangles;
            mesh.RecalculateNormals();
        }

        mesh.Optimize();

        hasMesh = true;

        if (generateCollider) {
            meshCollider.sharedMesh = mesh;
            // force update
            meshCollider.enabled = false;
            meshCollider.enabled = true;
        }        
    }
    
    public void SetDirty (bool dirtyState)
    {
        isDirty = dirtyState;
    }

    public void WaitForMesh(bool wait)
    {
        isWaitingMesh = wait;
    }

    public void DestroyOrDisable () {
        if (Application.isPlaying) {
            mesh.Clear ();
            gameObject.SetActive (false);
        } else {
            Destroy(gameObject);
        }
    }

    private void OnDrawGizmos()
    {
        // surface blocks for player chunk
        if (showSurfaceBlocks)
        {
            Vector3 center = ProTerra.ChunkOriginFromCoord(coord);
            float size;
            for (int x = 0; x < Chunk.size.width; x++)
                for (int z = 0; z < Chunk.size.width; z++)
                {
                    bool hadBlock = blocks[z, 0, x] > 0;
                    for (int y = 1; y < Chunk.size.height; y++)
                    {
                        bool hasBlock = blocks[z, y, x] > 0;

                        if (hadBlock != hasBlock)
                        {                               
                            int renderY = y + (hadBlock ? -1 : 0);

                            Vector3 offset = (new Vector3(x, renderY, z) + center);
                            size = (blocks[z, renderY, x] / 255f);// - 0.01f;
                            Gizmos.color = Color.green;

                            //Gizmos.DrawLine(offset + Vector3.up, offset + Vector3.right);

                            Gizmos.DrawLine(offset + Vector3.up * size, offset + Vector3.right * size);
                            Gizmos.DrawLine(offset + Vector3.right * size, offset - Vector3.up * size);
                            Gizmos.DrawLine(offset - Vector3.up * size, offset - Vector3.right * size);
                            Gizmos.DrawLine(offset - Vector3.right * size, offset + Vector3.up * size);

                            Gizmos.DrawLine(offset + Vector3.up * size, offset + Vector3.forward * size);
                            Gizmos.DrawLine(offset + Vector3.forward * size, offset - Vector3.up * size);
                            Gizmos.DrawLine(offset - Vector3.up * size, offset - Vector3.forward * size);
                            Gizmos.DrawLine(offset - Vector3.forward * size, offset + Vector3.up * size);

                            size = .1f;
                            Gizmos.color = new Color(0, 0, 1, .5f);
                            Gizmos.DrawWireCube(offset, Vector3.one * size);

                            //offset = new Vector3(x, renderY + blocks[z, renderY, x] / (255f), z);
                            ////size = Vector3.up;
                            //Gizmos.color = Color.red;
                            //Gizmos.DrawLine(offset, offset + Vector3.up);

                            hadBlock = hasBlock;
                        }
                    }
                }
        }

        if (isWaitingMesh)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(ProTerra.ChunkOriginFromCoord(coord) + Vector3.one * (Chunk.size.width) / 2f, Vector3.one * Chunk.size.width * 1.1f);
        }
    }
}