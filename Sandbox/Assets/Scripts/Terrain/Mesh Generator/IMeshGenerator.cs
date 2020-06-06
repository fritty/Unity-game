using UnityEngine;

public interface IMeshGenerator
{
    void RequestData(Vector3Int coord);
    void ManageRequests();
    void Destroy();
}


// public structures for meshes
public struct Triangle
{
#pragma warning disable 649 // disable unassigned variable warning
    public Vector3 a;
    public Vector3 b;
    public Vector3 c;

    public Vector3 this[int i]
    {
        get
        {
            switch (i)
            {
                case 0:
                    return a;
                case 1:
                    return b;
                default:
                    return c;
            }
        }
    }
}

public class MeshData
{
    public Vector3[] vertices;
    public int[] triangles;
    public Material mat;
    public bool generateColliders;

    int triangleIndex;

    public MeshData() { }

    public MeshData(int numTris)
    {
        vertices = new Vector3[(numTris) * 3];
        triangles = new int[(numTris) * 3];
        triangleIndex = 0;
    }

    public void AddTriangle(Triangle tri)
    {
        vertices[triangleIndex] = tri[0];
        vertices[triangleIndex + 1] = tri[1];
        vertices[triangleIndex + 2] = tri[2];
        triangles[triangleIndex] = triangleIndex;
        triangles[triangleIndex + 1] = triangleIndex + 1;
        triangles[triangleIndex + 2] = triangleIndex + 2;
        triangleIndex += 3;
    }

    public Mesh CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }
}
