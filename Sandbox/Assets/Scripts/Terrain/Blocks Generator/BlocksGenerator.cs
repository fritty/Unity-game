using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

/* Handles blocks data generation */
public class BlocksGenerator
{  
    BlocksGeneratorSettings Settings;

    static int maxThreadsPerUpdate = 8;

    Queue<GeneratedDataInfo<MapData>> mapDataQueue = new Queue<GeneratedDataInfo<MapData>>();
    Queue<Vector3Int> requestedCoords = new Queue<Vector3Int>();

    Action<GeneratedDataInfo<MapData>> dataCallback;
    ProceduralTerrain terrain;

    HeightFunction heightFunction;
    
    public BlocksGenerator (BlocksGeneratorSettings blocksGeneratorSettings, Action<GeneratedDataInfo<MapData>> dataCallback, ProceduralTerrain terrain)
    {
        Settings = blocksGeneratorSettings;
        this.dataCallback = dataCallback;
        this.terrain = terrain;

        switch(Settings.UsedHeightMap)
        {
            case HeightMapOptions.Flat: heightFunction = FlatHeight; break;
            case HeightMapOptions.Sin: heightFunction = SinHeight; break;
            case HeightMapOptions.Perlin: heightFunction = PerlinHeight; break;
        } 
    }

    /* Interface */
    public void ManageRequests()
    {
        // Return requested data
        if (mapDataQueue.Count > 0)
        {
            for (int i = 0; i < mapDataQueue.Count; i++)
            {
                GeneratedDataInfo<MapData> mapData = mapDataQueue.Dequeue();
                dataCallback(mapData);
            }
        }

        // Go through requested coordinates and start generation threads if still relevant
        if (requestedCoords.Count > 0)
        {
            Vector3Int viewerCoord = new Vector3Int(Mathf.FloorToInt(terrain.viewer.position.x / Chunk.size.width), 0, Mathf.FloorToInt(terrain.viewer.position.z / Chunk.size.width));
            int maxThreads = Mathf.Min(maxThreadsPerUpdate, requestedCoords.Count);
            for (int i = 0; i < maxThreads && requestedCoords.Count > 0; i++)
            {
                Vector3Int coord = requestedCoords.Dequeue();

                // skip outdated coordinates
                while ((Mathf.Abs(coord.x - viewerCoord.x) > terrain.viewDistance || Mathf.Abs(coord.z - viewerCoord.z) > terrain.viewDistance) && requestedCoords.Count > 0)
                {
                    coord = requestedCoords.Dequeue();
                }
                  
                // start generation
                if (Mathf.Abs(coord.x - viewerCoord.x) <= terrain.viewDistance && Mathf.Abs(coord.z - viewerCoord.z) <= terrain.viewDistance)
                {
                    ThreadStart threadStart = delegate {
                        MapDataThread(coord);
                    };
                    new Thread(threadStart).Start();
                }
            }
        }
    }

    public void RequestData(Vector3Int coord)
    {
        requestedCoords.Enqueue(coord);
    }

    public void SetCallback(Action<GeneratedDataInfo<MapData>> callback)
    {
        dataCallback = callback;
    }

    public void SetMapReference(ProceduralTerrain terrain)
    {
        this.terrain = terrain;
    }


    // Generation thread
    void MapDataThread(Vector3Int coord)
    {
        MapData mapData = Generate(coord);
        lock (mapDataQueue)
        {
            mapDataQueue.Enqueue(new GeneratedDataInfo<MapData>(mapData, coord));
        }
    }


    ////////////////
    /* Generators */
    ////////////////

    MapData Generate(Vector3Int coord)
    {
        Vector3 origin = OriginFromCoord(coord);
        byte[,,] blocks = ApplyHeightMap(CreateHeightMap(heightFunction, origin));

        return new MapData(blocks);
    }

    byte[,,] ApplyHeightMap (HeightMap heightMap)
    {  
        byte[,,] blocks = new byte[Chunk.size.width, Chunk.size.height, Chunk.size.width];

        for (byte x = 0; x < Chunk.size.width; x++)
        {
            for (byte z = 0; z < Chunk.size.width; z++)
            {
                float height = heightMap.array[z, x];
                for (byte y = 0; y < Chunk.size.height; y++)
                {
                    blocks[z, y, x] = HeightToByte(y, height);
                }
            }
        }
        return blocks;
    }   
    
    HeightMap CreateHeightMap(HeightFunction heightFunction, Vector3 origin)
    {
        HeightMap heightMap = HeightMap.Create();

        for (byte x = 0; x < Chunk.size.width; x++)
        {
            for (byte z = 0; z < Chunk.size.width; z++)
            {
                heightMap.array[z, x] = heightFunction(x, z, origin);
            }
        }

        return heightMap;
    }  

    float FlatHeight(int x, int z, Vector3 origin)
    {
        Vector3 tilt = new Vector3((origin.x / 32 + 120) / 256, 0, 0);

        return Settings.flatTiltX * x + Settings.flatTiltZ * z + Settings.baseHeight;
    }

    float SinHeight(int x, int z, Vector3 origin)
    {
        return (Settings.sinScale - 2) * Mathf.Pow(Mathf.Sin((origin.x + x) * Settings.sinFrequency), 2) + Settings.baseHeight;
    }

    float PerlinHeight(int x, int z, Vector3 origin)
    {
        float xCoord = (x + origin.x) * Settings.perlinFrequency + Settings.perlinOffset.x;
        float zCoord = (z + origin.z) * Settings.perlinFrequency + Settings.perlinOffset.y;
        return Settings.perlinScale * Mathf.PerlinNoise(xCoord, zCoord) + Settings.baseHeight;
    }

    //byte[,,] PlaneGen()
    //{
    //    byte[,,] blocks = new byte[Chunk.size.width, Chunk.size.height, Chunk.size.width];
    //    //Vector3 tilt = new Vector3((origin.x/32 + 120)/256,0,0);
    //    Vector3 rangeMin = new Vector3(0, 0, 10);
    //    Vector3 rangeMax = new Vector3(0, 0, 15);


    //    for (byte x = 0; x < Chunk.size.width; x++)
    //    {
    //        for (byte z = 0; z < Chunk.size.width; z++)
    //        {
    //            for (byte y = 0; y < Chunk.size.height; y++)
    //            {
    //                if ((x < rangeMax.x && x > rangeMin.x) || (z < rangeMax.z && z > rangeMin.z) || (y < rangeMax.y && y > rangeMin.y))
    //                    blocks[z, y, x] = 255;
    //            }
    //        }
    //    }
    //    return blocks;
    //} 

    //byte[,,] ShaderGen(Vector3 origin)
    //{
    //    byte[,,] blocks = new byte[Chunk.size.width, Chunk.size.height, Chunk.size.width];
    //    ComputeBuffer pointsBuffer = new ComputeBuffer(Chunk.size.width * Chunk.size.height * Chunk.size.width / 4, 4);
    //    int kernelHandle = mapShader.FindKernel("MapGen");
    //    int threadsPerAxis = 8;

    //    mapShader.SetBuffer(kernelHandle, "points", pointsBuffer);
    //    mapShader.SetInt("Width", Chunk.size.width);
    //    mapShader.SetFloat("Height", Chunk.size.height);

    //    mapShader.Dispatch(kernelHandle, Chunk.size.width / (threadsPerAxis * 4), Chunk.size.height / (threadsPerAxis), Chunk.size.width);

    //    pointsBuffer.GetData(blocks);
    //    pointsBuffer.Release();
    //    return blocks;
    //}        

    byte HeightToByte(byte y, float val, float slope)
    {
        if (y == Mathf.FloorToInt(val))
            return (byte)(Mathf.RoundToInt(255 * (val - Mathf.Floor(val))));
        if (y > val)
            return 0;

        if (slope <= val - y)
            return 255;
        else
            return (byte)Mathf.RoundToInt(255 * (val - y) / (slope));

    }

    byte HeightToByte(byte y, float val)
    {
        if (y == Mathf.FloorToInt(val))
            return (byte)(Mathf.RoundToInt(255 * (val - Mathf.Floor(val))));
        if (y > val)
            return 0;
        else
            return 255;
    }    

    Vector3 OriginFromCoord(Vector3Int coord)
    {
        return new Vector3(coord.x * Chunk.size.width, coord.y * Chunk.size.height, coord.z * Chunk.size.width);
    }
}

// container for chunk terrain data
public struct MapData
{
    public readonly byte[,,] blocks;

    public MapData(byte[,,] blocks)
    {
        this.blocks = blocks;
    }
}


// auxiliary types
struct HeightMap
{
    public float[,] array;

    public static HeightMap Create()
    {
        HeightMap heightMap = new HeightMap();
        heightMap.array = new float[Chunk.size.width, Chunk.size.width];
        return heightMap;
    }
}

delegate float HeightFunction(int x, int z, Vector3 origin);