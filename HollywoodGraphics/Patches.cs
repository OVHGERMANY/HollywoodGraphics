using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using GPUInstancer;
using HarmonyLib;
using MultiFlare;
using SPT.Reflection.Patching;

namespace HollywoodGraphics;


public class GraphicsControllerInitPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(GameWorld).GetMethod(nameof(GameWorld.OnGameStarted));
    }

    [PatchPostfix]
    // ReSharper disable once InconsistentNaming
    public static void Postfix(GameWorld __instance)
    {
        Plugin.Log.LogInfo("Running game world initialization and creating graphics controller");
        var graphicsController = __instance.gameObject.AddComponent<GraphicsController>();
        Singleton<GraphicsController>.Create(graphicsController);
    }
}

public class GraphicsRaidInitPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        // Every raid-start path (Run, InternalStartGame, OnReadyToStartMatchingAsync) converges here,
        // after _raidSettings is populated and before LocalGameCreate loads the location scene.
        return typeof(TarkovApplication).GetMethod(nameof(TarkovApplication.LocalGameMatching));
    }

    [PatchPrefix]
    // ReSharper disable once InconsistentNaming
    public static void Prefix(RaidSettings ____raidSettings)
    {
        Plugin.Log.LogInfo("Running raid initialization");
        var mapName = ____raidSettings.LocationId.ToLower();
        Plugin.GraphicsConfig.SetCurrentMap(mapName);
        
        var overrides = Plugin.GraphicsConfig.Current;
        var message = $"Graphics overrides map: {mapName} - {overrides.Name} enabled: {overrides.LodEnabled.Value}";
        Plugin.Log.LogInfo(message);
    }
}

public class LampControllerAwakePostfixPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(LampController).GetMethod(nameof(LampController.Awake));
    }

    [PatchPrefix]
    // ReSharper disable InconsistentNaming
    public static void Prefix(LampController __instance, FlareLight[] ___MultiFlareLights, MaterialEmission[] ____materialsWithEmission)
    {
        if (!Plugin.GraphicsConfig.LightFlareEnabled.Value)
            return;
        
        // Plugin.Log.LogInfo($"Found light: {__instance.name} lights: {___MultiFlareLights} alights: {__instance.CustomLights.Length}");
        
        foreach (var flareLight in ___MultiFlareLights)
        {
            // Plugin.Log.LogInfo($"Flare light: {__instance.name} alpha {flareLight.Alpha} scale {flareLight.Scale} flares {flareLight.Flares.Count}");
            
            var alphaField = Traverse.Create(flareLight).Field("_totalAlpha");
            alphaField.SetValue(alphaField.GetValue<float>() * Plugin.GraphicsConfig.LightFlareIntensity.Value);

            var scaleField = Traverse.Create(flareLight).Field("_totalScale");
            scaleField.SetValue(scaleField.GetValue<float>() * Plugin.GraphicsConfig.LightFlareSize.Value);
        }
    }
}

public class TerrainDetailOverridePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(GPUInstancerDetailManager).GetMethod(nameof(GPUInstancerDetailManager.Awake));
    }

    [PatchPostfix]
    // ReSharper disable once InconsistentNaming
    public static void Postfix(GPUInstancerDetailManager __instance)
    {
        var overrides = Plugin.GraphicsConfig.Current;
        
        Plugin.Log.LogInfo(
            $"Terrain detail overrides: {overrides.Name} Enabled: {overrides.LodEnabled.Value} ScaleDist: {overrides.DetailDistance.Value} ScaleDens: {overrides.DetailDensity.Value} Terrain: {__instance.terrain.name} Dist: {__instance.terrainSettings.maxDetailDistance} DistL: {__instance.terrainSettings.maxDetailDistanceLegacy}"
        );
        
        if (!overrides.LodEnabled.Value)
            return;

        var terrainDetailDistance = overrides.DetailDistance.Value;
        
        // __instance.terrain.detailObjectDistance *= Plugin.TerrainDetailDistance.Value;

        __instance.terrainSettings.maxDetailDistance *= terrainDetailDistance;
        __instance.terrainSettings.maxDetailDistanceLegacy *= terrainDetailDistance;

        foreach (var prototype in __instance.prototypeList)
        {
            var detailPrototype = prototype as GPUInstancerDetailPrototype;

            if (detailPrototype != null)
            {
                Plugin.Log.LogInfo($"DetailProto: {prototype.name} {prototype.maxDistance} {detailPrototype.densityFadeFactor}");
                detailPrototype.maxDistance *= terrainDetailDistance;
                detailPrototype.lodBiasAdjustment = terrainDetailDistance;
                detailPrototype.densityFadeFactor /= overrides.DetailDensity.Value;
            }
            else
            {
                Plugin.Log.LogInfo($"Proto: {prototype.name} {prototype.maxDistance}");
                prototype.maxDistance *= terrainDetailDistance;
                prototype.lodBiasAdjustment *= terrainDetailDistance;
            }
        }
    }
}