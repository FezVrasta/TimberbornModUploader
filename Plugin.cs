using Bindito.Core;
using HarmonyLib;
using Timberborn.ModManagerScene;
using UnityEngine;

namespace ModUploader
{
    [Context("MainMenu")]
    public class ModUploaderConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition)
        {
            containerDefinition.Bind<UploadRequests>().AsSingleton();
        }
    }

    /**
     * Uploads mods without the game's upload panel, for release scripts:
     * a request file in Documents/Timberborn (see UploadRequests) makes the
     * main menu upload the named mod to the Steam Workshop, and to mod.io
     * when the request carries a zip and there's a token for it.
     */
    public class Plugin : IModStarter
    {
        public static void Log(string message) => Debug.Log($"[ModUploader] {message}");
        public static void LogWarning(string message) => Debug.LogWarning($"[ModUploader] {message}");

        public void StartMod(IModEnvironment modEnvironment)
        {
            Dependencies.Load(modEnvironment.ModPath);
            Dependencies.Patch(new Harmony("modUploader"));
            Log("Mod Uploader is loaded");
        }
    }
}
