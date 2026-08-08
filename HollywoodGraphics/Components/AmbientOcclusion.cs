namespace HollywoodGraphics.Components;

public class AmbientOcclusion
{
    private readonly HBAO _hbao;
    private readonly HBAO_Core.AOSettings _defaultAOSettings;
    private readonly HBAO_Core.ColorBleedingSettings _defaultColorBleedingSettings;

    public AmbientOcclusion()
    {
        var camera = EFT.CameraControl.CameraManager.Instance?.Camera;

        if (camera == null)
        {
            Plugin.Log.LogError("AmbientOcclusion: No camera found!");
            return;
        }
        
        _hbao = camera.GetComponent<HBAO>();
        _defaultAOSettings = _hbao.aoSettings;
        _defaultColorBleedingSettings = _hbao.colorBleedingSettings;

        UpdateSettings();
    }

    public void UpdateSettings()
    {
        if (Plugin.GraphicsConfig.AOEnabled.Value)
        {
            var settings = _hbao.aoSettings;
            settings.intensity = Plugin.GraphicsConfig.AOIntensity.Value;
            settings.radius = Plugin.GraphicsConfig.AORadius.Value;
            settings.bias = Plugin.GraphicsConfig.AOBias.Value;
            settings.useMultiBounce = Plugin.GraphicsConfig.AOMultiBounceEnabled.Value;
            settings.multiBounceInfluence = Plugin.GraphicsConfig.AOMultiBounceInfluence.Value;
            _hbao.aoSettings = settings;

            var colorbleedSettings = _hbao.colorBleedingSettings;
            colorbleedSettings.enabled = Plugin.GraphicsConfig.AOColorBleedEnabled.Value;
            colorbleedSettings.saturation = Plugin.GraphicsConfig.AOColorBleedSaturation.Value;
            colorbleedSettings.albedoMultiplier = Plugin.GraphicsConfig.AOColorBleedAlbedoMul.Value;
            _hbao.colorBleedingSettings = colorbleedSettings;
        }
        else
        {
            _hbao.aoSettings = _defaultAOSettings;
            _hbao.colorBleedingSettings = _defaultColorBleedingSettings;
        }
    }
}