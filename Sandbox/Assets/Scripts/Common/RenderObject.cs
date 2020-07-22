using UnityEngine;

public class RenderObject : MonoBehaviour
{
    public Mesh Mesh;
    public MeshFilter MeshFilter;
    public MeshRenderer MeshRenderer;
    public bool Visibility { get { return MeshRenderer.enabled; } set { MeshRenderer.enabled = value; } }

    protected virtual void Awake ()
    {           
        MeshFilter = GetComponent<MeshFilter>();
        MeshRenderer = GetComponent<MeshRenderer>();

        if (MeshFilter == null) MeshFilter = gameObject.AddComponent<MeshFilter>();
        if (MeshRenderer == null) MeshRenderer = gameObject.AddComponent<MeshRenderer>();

        Mesh = MeshFilter.sharedMesh = new Mesh();
    } 
}
