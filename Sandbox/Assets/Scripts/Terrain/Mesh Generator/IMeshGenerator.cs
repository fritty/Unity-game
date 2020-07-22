using UnityEngine;

namespace Sandbox.ProceduralTerrain.Core
{
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
}