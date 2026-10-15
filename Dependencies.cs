using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Steamworks;
using Timberborn.SteamWorkshop;

namespace ModUploader
{
    /**
     * The game's uploader can't set an item's required items. Dependencies.json
     * maps a mod id to the Workshop items it requires; once an upload of that
     * mod goes through, from the panel or from a request, they're added through
     * Steam, so subscribing to the mod subscribes to them too.
     */
    internal static class Dependencies
    {
        private static Dictionary<string, ulong[]> _required = new Dictionary<string, ulong[]>();
        // Kept, or it's collected before Steam answers
        private static readonly List<object> _pending = new List<object>();

        public static void Load(string modPath)
        {
            string file = Path.Combine(modPath, "Dependencies.json");
            if (!File.Exists(file)) return;
            try
            {
                _required = JsonConvert.DeserializeObject<Dictionary<string, ulong[]>>(File.ReadAllText(file)) ?? _required;
            }
            catch (System.Exception e)
            {
                Plugin.LogWarning($"Can't read Dependencies.json: {e.Message}");
            }
        }

        // Patched by name, not with an attribute: copies of the game without
        // Steam have no Steam assemblies, and PatchAll fails on the attribute
        public static void Patch(Harmony harmony)
        {
            var updater = AccessTools.TypeByName("Timberborn.SteamWorkshop.SteamWorkshopItemUpdater");
            var target = updater == null ? null : AccessTools.Method(updater, "OnItemUpdated");
            if (target == null) return;
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(Dependencies), nameof(Postfix)));
        }

        private static void Postfix(SubmitItemUpdateResult_t result, bool ioFailure, SteamWorkshopUpdateRequest request)
        {
            if (ioFailure || result.m_eResult != EResult.k_EResultOK) return;
            string id = ModIdIn(request.ContentPath);
            if (id == null || !_required.TryGetValue(id, out ulong[] items)) return;
            ulong item = request.ItemId;
            foreach (ulong required in items)
            {
                ulong dependency = required;
                var added = CallResult<AddUGCDependencyResult_t>.Create((answer, failure) =>
                {
                    if (failure || answer.m_eResult != EResult.k_EResultOK)
                        Plugin.LogWarning($"Couldn't make {dependency} a required item of {item}: {(failure ? "Steam didn't answer" : answer.m_eResult.ToString())}");
                    else
                        Plugin.Log($"{dependency} is a required item of {item}");
                });
                added.Set(SteamUGC.AddDependency(new PublishedFileId_t(item), new PublishedFileId_t(dependency)));
                _pending.Add(added);
            }
        }

        // The id of the mod in the folder uploaded: from a manifest in it, or in one of its version folders
        private static string ModIdIn(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return null;
            foreach (string file in Directory.GetFiles(folder, "manifest.json", SearchOption.AllDirectories))
            {
                try
                {
                    string id = (string)JObject.Parse(File.ReadAllText(file))["Id"];
                    if (id != null && _required.Keys.Any(k => string.Equals(k, id, System.StringComparison.OrdinalIgnoreCase)))
                        return _required.Keys.First(k => string.Equals(k, id, System.StringComparison.OrdinalIgnoreCase));
                }
                catch
                {
                }
            }
            return null;
        }
    }
}
