using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public interface IGenerator {
    void ManageRequests ();
    void RequestData (Vector3Int coord);
    // void SetCallback (Action<GeneratedDataInfo<T>> callback);
    // void SetMapReference (Map map);
}
