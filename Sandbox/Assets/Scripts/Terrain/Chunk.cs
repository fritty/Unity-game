using UnityEngine;
using Unity.Collections;

public class Chunk : MonoBehaviour {
    public struct size
    {
        static public int width = 32;
        static public int height = 32;
    }

    [SerializeField]
    bool showBlocksGizmo = false;

    [HideInInspector]
    public Vector3Int coord { get; private set; }
    [HideInInspector]
    public byte[,,] blocks { get; private set; }
    [HideInInspector]
    public bool hasMesh { get; private set; }


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
    }
    

    public void SetCoord (Vector3Int coord) {        
        this.coord = coord;
        name = $"Chunk ({coord.x}, {coord.y}, {coord.z})";
        if (hasMesh)
        {
            mesh.Clear();
            hasMesh = false;
        }
        transform.position = OriginFromCoord(coord);
    }

    public void SetBlocks (byte[,,] blocks) {
        this.blocks = blocks;

        if (hasMesh)
        {
            mesh.Clear();
            hasMesh = false;
        }
    }

    public void SetMesh (MeshData meshData) {         

        mesh = meshFilter.sharedMesh = meshData.CreateMesh();
        
        mesh.Optimize();        

        hasMesh = true;

        if (generateCollider) {
            meshCollider.sharedMesh = mesh;
            // force update
            meshCollider.enabled = false;
            meshCollider.enabled = true;
        }        
    } 

    Vector3 OriginFromCoord(Vector3Int coord)
    {
        return new Vector3(coord.x * Chunk.size.width, coord.y * Chunk.size.height, coord.z * Chunk.size.width);
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
        if (showBlocksGizmo)
        {
            Vector3 center = OriginFromCoord(coord);
            Vector3 size = Vector3.one * 1;
            for (int x = 0; x < Chunk.size.width; x++)
                for (int z = 0; z < Chunk.size.width; z++)
                    for (int y = 0; y < Chunk.size.height; y++)
                    {
                        if (blocks[z, y, x] < 255 && blocks[z, y, x] > 0)
                        {
                            Vector3 offset = new Vector3(x, y, z);
                            size = Vector3.up * (blocks[z, y, x] / 255f);
                            Gizmos.color = new Color(0, 1, 0, 1);
                            Gizmos.DrawLine(center + offset, center + offset + size);

                            offset = new Vector3(x, y + blocks[z, y, x] / (255f), z);
                            size = Vector3.up;
                            Gizmos.color = new Color(1, 0, 0, 1);
                            Gizmos.DrawLine(center + offset, center + offset + size);

                            offset = new Vector3(x, y, z);
                            size = Vector3.one * .1f;
                            Gizmos.color = new Color(0, 0, 1, .5f);
                            Gizmos.DrawWireCube(center + offset, size);

                        }
                    }
        }
    }
}