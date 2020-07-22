using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;
using System.Linq;

namespace Sandbox.ProceduralTerrain.Core
{
    /* Handles blocks data generation */
    public class MapGenerator
    {
        readonly TerrainSettings _settings;

        Action<GeneratedDataInfo<MapData[]>> _dataCallback;     
        Queue<GeneratedDataInfo<MapData[]>> _mapDataQueue;
        Queue<Vector2Int> _requestedCoords;
        HeightMapGenerator _heightMapGenerator;
        Thread _thread;

        byte[] _fullArray;

        public MapGenerator(Action<GeneratedDataInfo<MapData[]>> dataCallback, TerrainSettings Settings)
        {
            this._settings = Settings;
            this._dataCallback = dataCallback;
            _mapDataQueue = new Queue<GeneratedDataInfo<MapData[]>>();
            _requestedCoords = new Queue<Vector2Int>();
            _heightMapGenerator = new HeightMapGenerator(Settings.MapGeneratorSettings.HeightMapSettings);
            _fullArray = Enumerable.Repeat<byte>(255, ChunkSize.Length).ToArray();

            ThreadStart threadStart = delegate
            {
                MapDataThread();
            };
            _thread = new Thread(threadStart);
            _thread.Start();
        }

        public void Destroy()
        {
            //thread.Abort();
        }

        public void ManageRequests()
        {
            // Return generated data
            if (_mapDataQueue.Count > 0)
            {
                for (int i = 0; i < _mapDataQueue.Count; i++)
                {
                    GeneratedDataInfo<MapData[]> mapData = _mapDataQueue.Dequeue();
                    _dataCallback(mapData);
                }
            }
        }

        public void RequestData(List<Vector2Int> forGeneration)
        {
            lock (_requestedCoords)
            {
                for (int i = 0; i < forGeneration.Count; i++)
                    _requestedCoords.Enqueue(forGeneration[i]);
            }
        }

        public Queue<Vector2Int> GetRequests()
        {
            return _requestedCoords;
        }

        public void ClearStoredRequests()
        {
            lock (_requestedCoords)
            {
                _requestedCoords.Clear();
            }
        }

        public void ReplaceRequests(List<Vector2Int> forGeneration)
        {
            lock (_requestedCoords)
            {
                _requestedCoords.Clear();
                for (int i = 0; i < forGeneration.Count; i++)
                    _requestedCoords.Enqueue(forGeneration[i]);
            }
        }

        // Generation thread
        private void MapDataThread()
        {
            while (true)
            {
                if (_requestedCoords.Count > 0)
                {
                    Vector2Int coord;
                    lock (_requestedCoords)
                    {
                        coord = _requestedCoords.Dequeue();
                    }

                    MapData[] mapData = Generate(coord);

                    lock (_mapDataQueue)
                    {
                        _mapDataQueue.Enqueue(new GeneratedDataInfo<MapData[]>(mapData, coord.X0Y()));
                    }
                }
                else
                    Thread.Sleep(20);
            }
        }

        MapData[] Generate(Vector2Int coord)
        {
            HeightMap heightMap = _heightMapGenerator.CreateHeightMap(coord);
            MapData[] mapData = new MapData[_settings.WorldHeight];

            for (int y = 0; y < _settings.WorldHeight; y++)
            {
                mapData[y] = new MapData(ApplyHeightMap(heightMap, y * ChunkSize.Height));
            }

            return mapData;
        }

        byte[] ApplyHeightMap(HeightMap heightMap, int yPosition)
        {
            byte[] blocks = new byte[ChunkSize.Length];
            if (heightMap.max <= yPosition) return blocks;
            if (heightMap.min >= yPosition + ChunkSize.Height) return _fullArray;

            // fill everything below heightMap.min
            int chunkFillHeight = Mathf.Max(Mathf.FloorToInt(heightMap.min - yPosition), 0);
            if (chunkFillHeight > 0)
                Buffer.BlockCopy(_fullArray, 0, blocks, 0, ChunkSize.Width * ChunkSize.Width * chunkFillHeight);

            for (int x = 0; x < ChunkSize.Width; x++)
            {
                for (int z = 0; z < ChunkSize.Width; z++)
                {
                    float height = heightMap[z, x] - yPosition;
                    if (height <= 0) continue;

                    float minHeight = Mathf.Min(new float[] { height,
                                                              heightMap[z, x + 1] - yPosition,
                                                              heightMap[z, x - 1] - yPosition,
                                                              heightMap[z + 1, x] - yPosition,
                                                              heightMap[z - 1, x] - yPosition });
                    int floorHeight = Mathf.FloorToInt(height), floorMin = Mathf.FloorToInt(minHeight);

                    int y;
                    int fillMax = Mathf.Min(ChunkSize.Height - 1, (floorHeight == floorMin ? floorMin - 1 : floorMin));
                    for (y = chunkFillHeight; y <= fillMax; y++)
                        blocks[x + z * ChunkSize.Width + y * ChunkSize.Width * ChunkSize.Width] = 255;
                    if (y == ChunkSize.Height) continue;

                    if (floorHeight == floorMin || (height - minHeight <= 1))
                    {
                        blocks[x + z * ChunkSize.Width + floorHeight * ChunkSize.Width * ChunkSize.Width] = (byte)Mathf.FloorToInt((height - floorHeight) * 255);
                        continue;
                    }

                    int maxY = Mathf.Min(ChunkSize.Height - 1, floorHeight);

                    y = Mathf.Max(0, floorMin + 1);
                    byte increment = (byte)Mathf.FloorToInt(255 / (height - minHeight));
                    byte value = (byte)Mathf.FloorToInt(255 * (height - floorHeight) / (height - minHeight));
                    if (floorHeight > maxY) value = (byte)(value + increment * (floorHeight - maxY));
                    for (int i = maxY; i >= y; i--)
                    {
                        blocks[x + z * ChunkSize.Width + i * ChunkSize.Width * ChunkSize.Width] = value;
                        value += increment;
                    }
                }
            }
            return blocks;
        }
    }
}