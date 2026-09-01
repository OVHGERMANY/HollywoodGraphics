using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace HollywoodGraphics;

[BepInPlugin("com.janky.hollywoodgraphics", "Janky-HollywoodGraphics", HollywoodGraphicsVersion)]
[SuppressMessage("ReSharper", "HeapView.ObjectAllocation.Evident")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class Plugin : BaseUnityPlugin
{
    public const string MajorMinorVersion = "2.0";
    public const string HollywoodGraphicsVersion = $"{MajorMinorVersion}.1";
    public static ManualLogSource Log;

    public static GraphicsConfig GraphicsConfig;
    private static ConfigEntry<bool> _loggingEnabled;
    private static bool _amandsDetected;
    
    // Required by HFX integration, but there's no locally detectable usage so Rider yells at us
    // ReSharper disable once UnusedMember.Global
    public static ConfigEntry<float> lensDustIntensity => GraphicsConfig.Bloom.DustIntensity;

    private void Awake()
    {
        Log = Logger;
        StartCoroutine(DelayedLoad());
    }

    private IEnumerator DelayedLoad()
    {
        // We wait for 5 seconds to allow all the 500 shonky mods (incl. this one) an average user installs to load 
        yield return new WaitForSeconds(5);
        
        if (Chainloader.PluginInfos.ContainsKey("com.Amanda.Graphics"))
        {
            Log.LogInfo("Amand's Graphics Mod detected, disabling HollywoodGraphics!");
            _amandsDetected = true;
        }
        
        SetupConfig();
        
        // Versioning
        var key = new ConfigDefinition("99. Internal", "ConfigVersion");
        var description = new ConfigDescription(
            "Do not modify.",
            null,
            new ConfigurationManagerAttributes { Order = 0, IsAdvanced = true }
        );
        
        // Stub entry that will get whatever value is there without accidentally hiding it by a default
        var versionConfig = Config.Bind(key, "", description);

        if (versionConfig is not { Value: MajorMinorVersion })
        {
            Config.Clear();
            File.WriteAllText(Config.ConfigFilePath, "");
            Config.Reload();

            SetupConfig();
        
            Log.LogInfo($"Configuration reset for version {MajorMinorVersion}.");
        }
        else
        {
            // We have to remove the stub entry so that we can replace it with the real one below
            Config.Remove(key);
        }
        
        versionConfig = Config.Bind(key, MajorMinorVersion, description);
        versionConfig.Value = MajorMinorVersion;

        // Patches
        if (!_amandsDetected)
        {
            new LampControllerAwakePostfixPatch().Enable();
            new GraphicsRaidInitPatch().Enable();
            new GraphicsControllerInitPatch().Enable();
            new TerrainDetailOverridePatch().Enable();
        }
        
        Log.LogInfo("Initialization finished");

        if (_loggingEnabled.Value)
        {
            Log.LogInfo("Logging enabled");
        }
        else
        {
            Log.LogInfo("Logging disabled");
            BepInEx.Logging.Logger.Sources.Remove(Log);
        }
    }

    private static void LoadTemplateDrawer(ConfigEntryBase entry)
    {
        if (_amandsDetected)
        {
            GUILayout.TextArea("Amand's Graphics mod detected, HollywoodGraphics disabled!");
        }
        else
        {
            if (GUILayout.Button("Janky's Special"))
            {
                ConfigurationTemplates.SetJanky(entry.ConfigFile);
            }
            
            if (GUILayout.Button("Potato"))
            {
                ConfigurationTemplates.SetPotato(entry.ConfigFile);
            }

            if (GUILayout.Button("Defaults"))
            {
                ConfigurationTemplates.SetDefaults(entry.ConfigFile);
            }            
        }
    }

    private void SetupConfig()
    {
        const string general = "00. General";
        const string debug = "98. Debug";

        /*
         * General
         */
        Config.Bind(general, "Load Template (RESTART)", "", new ConfigDescription(
            "Use a preset template for the settings. Requires restarting the game to ensure all the settings take effect.",
            null,
            new ConfigurationManagerAttributes { Order = 1, CustomDrawer = LoadTemplateDrawer }
        ));

        /*
         * Graphics
         */
        GraphicsConfig = new GraphicsConfig(Config);

        // Turn everything readonly if Amand's is detected to avoid conflicts
        if (_amandsDetected)
        {
            foreach (var pair in Config)
            {
                foreach (var configTag in pair.Value.Description.Tags)
                {
                    if (configTag is ConfigurationManagerAttributes configAttrs)
                    {
                        configAttrs.ReadOnly = true;
                    }
                }
            }
        }
        
        /*
         * Deboog
         */
        _loggingEnabled = Config.Bind(debug, "Enable Debug Logging (RESTART)", false, new ConfigDescription(
            "Duh. Requires restarting the game to take effect.",
            null,
            new ConfigurationManagerAttributes { Order = 1 }
        ));
    }
}
