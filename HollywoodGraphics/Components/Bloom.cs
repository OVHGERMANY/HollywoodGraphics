using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using EFT.Weather;
using UnityEngine;

namespace HollywoodGraphics.Components;

public class Bloom
{
    private float _sunLightFactor = 10000f;
    
    private readonly UltimateBloom _ultimateBloom;
    private readonly WeatherController _weatherController;

    public Bloom()
    {
        // Find the main camera
        var camera = EFT.CameraControl.CameraManager.Instance?.Camera;

        if (camera == null)
        {
            Plugin.Log.LogError("Bloom: No camera found!");
            return;
        }

        // Check if Ultimate Bloom is already on the camera
        _ultimateBloom = camera.GetComponent<UltimateBloom>();

        if (_ultimateBloom == null)
        {
            // Add Ultimate Bloom component to camera
            _ultimateBloom = camera.gameObject.AddComponent<UltimateBloom>();
            Plugin.Log.LogInfo("Bloom: Added Ultimate Bloom component to camera");
        }

        _ultimateBloom.m_IntensityManagement = UltimateBloom.BloomIntensityManagement.FilmicCurve;
        _ultimateBloom.m_SamplingMode = UltimateBloom.SamplingMode.HeightRelative;
        _ultimateBloom.m_SamplingMinHeight = 384;
        // Reduces flicker
        _ultimateBloom.m_AnamorphicSmallVerticalBlur = true;

        Plugin.Log.LogInfo("Resetting Main Bloom intensities");
        ResetIntensities(_ultimateBloom.m_BloomIntensities);
        Plugin.Log.LogInfo("Resetting Anamorphic Bloom intensities");
        ResetIntensities(_ultimateBloom.m_AnamorphicBloomIntensities);
        Plugin.Log.LogInfo("Resetting Star Bloom intensities");
        ResetIntensities(_ultimateBloom.m_StarBloomIntensities);
        
        // Turn these off as they form the "blob" part of the bloom and can oversaturate things.
        _ultimateBloom.m_BloomUsages[0] = _ultimateBloom.m_BloomUsages[1] = false;
        _ultimateBloom.m_AnamorphicBloomUsages[0] = false;
        _ultimateBloom.m_AnamorphicBloomUsages[1] = true;
        _ultimateBloom.m_StarBloomUsages[0] = false;

        // Disable high order star blooms because they end up applying everywhere on the screen
        for (var i = 3; i < _ultimateBloom.m_StarBloomUsages.Length; i++)
        {
            _ultimateBloom.m_StarBloomUsages[i] = false;
        }
        
        UpdateSettings();
        UpdateLensDust();
        Plugin.Log.LogInfo($"Bloom: Ultimate Bloom effect applied to camera {camera.name}");

        var weather = GameObject.Find("Weather");
        
        if (weather == null)
            return;
        
        _weatherController = weather.GetComponent<WeatherController>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update()
    {
        if (_weatherController == null)
            return;

        var nightFactor = Mathf.InverseLerp(0f, -0.1f, _weatherController.SunHeight);
        
        if (Mathf.Abs(nightFactor - _sunLightFactor) < 0.05f)
            return;

        var bloomConfig = Plugin.GraphicsConfig.Bloom;

        var bias = 0.5f * nightFactor;
        _ultimateBloom.m_DirtLightIntensity = bloomConfig.DirtLightIntensity.Value * Plugin.GraphicsConfig.Current.BloomMultiplier.Value + bias;
        _ultimateBloom.m_StarFlareIntensity = bloomConfig.StarFlareIntensity.Value + bias;
        _ultimateBloom.m_StarScale = Mathf.Max(bloomConfig.StarScale.Value - 2f * nightFactor, 0.5f);

        var highlightScaling = 1f + 0.1f * nightFactor;
        _ultimateBloom.SetFilmicCurveParameters(
            bloomConfig.BloomMid.Value,
            bloomConfig.BloomDark.Value,
            bloomConfig.BloomBright.Value,
            bloomConfig.BloomHighlight.Value * highlightScaling
        );

        _sunLightFactor = nightFactor;
    }
    
    public static void Destroy()
    {
    }
    
    public void UpdateSettings()
    {
        var config = Plugin.GraphicsConfig.Bloom;
        
        _ultimateBloom.m_BloomIntensity = config.BloomIntensity.Value;
        _ultimateBloom.SetFilmicCurveParameters(config.BloomMid.Value, config.BloomDark.Value, config.BloomBright.Value, config.BloomHighlight.Value);

        _ultimateBloom.m_UseLensDust = config.UseLensDust.Value;
        _ultimateBloom.m_DustIntensity = config.DustIntensity.Value;
        _ultimateBloom.m_DirtLightIntensity = config.DirtLightIntensity.Value * Plugin.GraphicsConfig.Current.BloomMultiplier.Value;

        _ultimateBloom.m_UseAnamorphicFlare = config.UseAnamorphicFlare.Value;
        _ultimateBloom.m_AnamorphicFlareIntensity = config.AnamorphicFlareIntensity.Value;
        _ultimateBloom.m_AnamorphicScale = config.AnamorphicScale.Value;
        _ultimateBloom.m_AnamorphicBlurPass = config.AnamorphicBlurPass.Value;

        _ultimateBloom.m_UseStarFlare = config.UseStarFlare.Value;
        _ultimateBloom.m_StarFlareIntensity = config.StarFlareIntensity.Value;
        _ultimateBloom.m_StarScale = config.StarScale.Value;
        _ultimateBloom.m_StarBlurPass = config.StarBlurPass.Value;
        
        // Force the recalculation of the sunlight factor
        _sunLightFactor = 10000f;
    }

    public void UpdateLensDust()
    {
        var config = Plugin.GraphicsConfig.Bloom;
        
        if (config.LensDust.Value == null)
            return;

        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        if (assemblyDirectory == null)
            return;

        var path = Path.Combine(assemblyDirectory, "bloom", config.LensDust.Value);

        if (!File.Exists(path))
            return;

        var data = File.ReadAllBytes(path);
        var tex2D = new Texture2D(1920, 1080, TextureFormat.RGBA32, true);

        tex2D.LoadImage(data);
        _ultimateBloom.m_DustTexture = tex2D;
    }
    
    private static void ResetIntensities(float[] intensities)
    {
        for (var i = 0; i < intensities.Length; i++)
        {
            Plugin.Log.LogInfo($"Intensity: {intensities[i]}");
            intensities[i] = 1f;
        }
    }
}