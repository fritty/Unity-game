using Sandbox.ProceduralTerrain.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class HeightFunctionsFactory
{
    public static IHeightMapFunction CreateFunction (HeightMapLayerSettings settings)
    {
        switch (settings.UsedFunction)
        {
            case HeightMapLayerSettings.HeightMapOption.Flat: return new FlatFunction(settings.FlatFunctionSettings);
            case HeightMapLayerSettings.HeightMapOption.Tilt: return new RegularTiltFunction(settings.RegularTiltFunctionSettings);
            case HeightMapLayerSettings.HeightMapOption.Sin: return new SinFunction(settings.SinFunctionSettings);
            case HeightMapLayerSettings.HeightMapOption.Perlin: return new PerlinFunction(settings.PerlinFunctionSettings);
            case HeightMapLayerSettings.HeightMapOption.LayeredNoise: return new LayeredNoiseFunction(settings.LayeredNoiseFunctionSettings);
        }
        return null;
    }
}
