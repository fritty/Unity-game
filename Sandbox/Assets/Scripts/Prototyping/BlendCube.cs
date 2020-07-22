using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlendCube : RenderObject
{
    protected override void Awake()
    {
        base.Awake();

        Vector3[] vertices = new Vector3[8];
        int[] triangles = new int[] { 2, 1, 0,
                                      3, 1, 2,
                                      5, 6, 4,
                                      7, 6, 5,
                                      3, 5, 1,
                                      7, 5, 3,
                                      4, 2, 0,
                                      6, 2, 4,
                                      7, 2, 6,
                                      3, 2, 7,
                                      5, 0, 1,
                                      4, 0, 5, };
        for (int i = 0; i  < 8; i++)
            vertices[i] = new Vector3(i & 1, (i & 2) >> 1, (i & 4) >> 2);

        Mesh.vertices = vertices;
        Mesh.triangles = triangles;
        Mesh.RecalculateNormals();
    }
}
