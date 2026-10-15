using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bindito.Core;
using Newtonsoft.Json.Linq;
using Timberborn.Modding;
using Timberborn.SingletonSystem;
using Timberborn.SteamStoreSystem;
using Timberborn.SteamWorkshop;
using Timberborn.SteamWorkshopModUploadingUI;
using UnityEngine;

namespace ModUploader
{
    /**
     * A file named ModUploader-request.json in Documents/Timberborn starts an
     * upload. Every key but "mod" is optional:
     *
     *   {
     *     "mod": "a mod's id or folder name",
     *     "title": "the Workshop item's title",
     *     "description": "...", "descriptionFile": "a file with it",
     *     "changelog": "...", "changelogFile": "a file with it",
     *     "preview": "path to the preview image",
     *     "visibility": "Private | Friends | Public | Unlisted",
     *     "tags": ["the chosen tags"],
     *     "content": "the folder to upload, the mod's folder in Mods by default",
     *     "create": false,
     *     "steam": true,
     *     "modio": { "game": 3659, "mod": 123, "zip": "path", "version": "1.0", "active": true, "metadata": "...", "tokenFile": "..." }
     *   }
     *
     * What the request leaves out stays as the last upload from the game's
     * panel left it (name, description, visibility, tags and thumbnail, by its
     * toggles). Only items created once (workshop_data.json) can be
     * updated, unless "create" is true: then a mod with no item gets one made
     * (private unless "visibility" says otherwise) before its first upload.
     * "steam": false skips the Workshop. mod.io needs "modio" and an
     * OAuth2 access token with write access in the file "tokenFile" names,
     * ~/.config/timberborn-mod-uploader/modio-token by default; without the token it's skipped. ModUploader-request.json.result gets a
     * line for each target: "steam ok <item id>", "modio ok <file id>" or
     * "<target> failed <reason>".
     */
    public class UploadRequests : IUpdatableSingleton
    {
        private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        // Windows has Documents known to the system (it may not sit under the profile), elsewhere it's under the home folder
        private static string Folder => Path.Combine(Path.DirectorySeparatorChar == '\\' ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : Path.Combine(Home, "Documents"), "Timberborn");
        private static string Flag => Path.Combine(Folder, "ModUploader-request.json");
        private static string Result => Flag + ".result";
        private static string DefaultTokenFile => Path.Combine(Home, ".config", "timberborn-mod-uploader", "modio-token");

        private readonly ModRepository _modRepository;
        // The Steam parts are fetched only for an upload, so copies without Steam never need them
        private readonly IContainer _container;
        private float _nextPoll;
        private bool _busy;
        private readonly List<string> _outcomes = new List<string>();
        private System.Threading.Tasks.Task<string> _modio;

        public UploadRequests(ModRepository modRepository, IContainer container)
        {
            _modRepository = modRepository;
            _container = container;
        }

        public void UpdateSingleton()
        {
            if (_modio != null && _modio.IsCompleted)
            {
                _outcomes.Add(_modio.IsFaulted ? $"modio failed {_modio.Exception.GetBaseException().Message}" : _modio.Result);
                _modio = null;
                Done();
                return;
            }
            if (_busy || Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.5f;
            if (!File.Exists(Flag)) return;
            string text = File.ReadAllText(Flag);
            File.Delete(Flag);
            File.Delete(Result);
            _busy = true;
            _outcomes.Clear();
            JObject request;
            try
            {
                request = JObject.Parse(text);
                if (request["steam"]?.Value<bool>() == false)
                {
                    StartModIo(request);
                    return;
                }
                UploadToSteam(request);
            }
            catch (Exception e)
            {
                _outcomes.Add($"steam failed {e.Message}");
                Done();
            }
        }

        // A text from the request: the key itself, or the file named by key + "File"
        private static string Text(JObject request, string key)
        {
            string file = (string)request[key + "File"];
            if (file != null) return File.ReadAllText(file).Trim();
            return (string)request[key];
        }

        private void UploadToSteam(JObject request)
        {
            string id = (string)request["mod"];
            if (!_container.GetInstance<SteamManager>().Initialized)
            {
                Fail("Steam isn't running", request);
                return;
            }
            Mod mod = _modRepository.UserMods.FirstOrDefault(m => m.Manifest.Id == id || m.ModDirectory.OriginName == id);
            if (mod == null)
            {
                Fail($"no mod {id} in Mods", request);
                return;
            }
            var uploadable = _container.GetInstance<SteamWorkshopUploadableModFactory>().Create(mod);
            if (uploadable.ItemId.HasValue)
            {
                Update(request, mod, uploadable);
                return;
            }
            if (request["create"]?.Value<bool>() != true)
            {
                uploadable.Clear();
                Fail($"{id} has no Workshop item: set \"create\" to make it", request);
                return;
            }
            string name = (string)request["title"] ?? uploadable.Name;
            var visibility = Visibility(request) ?? SteamWorkshopVisibility.Private;
            var tags = request["tags"] is JArray chosen ? chosen.Select(t => (string)t).ToList() : new List<string>();
            _container.GetInstance<SteamWorkshopItemCreator>().CreateItem(response =>
            {
                if (!response.Successful)
                {
                    uploadable.Clear();
                    Fail($"couldn't create the item: {response.ResultMessage}", request);
                    return;
                }
                // Saved the way the panel saves a new item, so the next upload finds it
                uploadable.OnItemCreated(response.ItemId, name, visibility, tags);
                uploadable.Clear();
                Update(request, mod, _container.GetInstance<SteamWorkshopUploadableModFactory>().Create(mod));
            });
        }

        private static SteamWorkshopVisibility? Visibility(JObject request)
        {
            string text = (string)request["visibility"];
            return text == null ? (SteamWorkshopVisibility?)null : (SteamWorkshopVisibility)Enum.Parse(typeof(SteamWorkshopVisibility), text, true);
        }

        private void Update(JObject request, Mod mod, SteamWorkshopUploadableMod uploadable)
        {
            // As UploadPanelElements builds it, with the panel's toggles as they were left, unless the request says otherwise
            var builder = new SteamWorkshopUpdateRequest.Builder(uploadable.ItemId.Value, (string)request["title"] ?? uploadable.Name)
                .SetContentPath((string)request["content"] ?? uploadable.ContentPath);
            builder.SetChangelog(Text(request, "changelog") ?? "");
            string description = Text(request, "description");
            if (description != null) builder.SetDescription(description);
            else if (uploadable.UpdateDescription) builder.SetDescription(uploadable.Description);
            var visibility = Visibility(request);
            if (visibility != null) builder.SetVisibility(visibility);
            else if (uploadable.UpdateVisibility) builder.SetVisibility(uploadable.Visibility);
            string preview = (string)request["preview"];
            if (preview != null) builder.SetPreviewPath(preview);
            else if (uploadable.UpdatePreview) builder.SetPreviewPath(uploadable.PreviewPath);
            if (request["tags"] is JArray tags) builder.AddMandatoryTags(uploadable.MandatoryTags).AddChosenTags(tags.Select(t => (string)t));
            else if (uploadable.UpdateTags) builder.AddMandatoryTags(uploadable.MandatoryTags).AddChosenTags(uploadable.ChosenTags);
            var update = builder.Build();
            uploadable.OnUpdateRequestCreated(update);

            // The callback captures no Steam types: a field of one in a closure
            // fails to load on copies of the game without Steam
            Action clear = uploadable.Clear;
            ulong item = update.ItemId;
            Plugin.Log($"Uploading {mod.Manifest.Name} {mod.Manifest.Version} to item {item}");
            _container.GetInstance<SteamWorkshopItemUpdater>().Update(update, response =>
            {
                clear();
                _outcomes.Add(response.Successful ? $"steam ok {item}" : $"steam failed {response.ResultMessage}");
                StartModIo(request);
            });
        }

        private void Fail(string reason, JObject request)
        {
            _outcomes.Add($"steam failed {reason}");
            StartModIo(request);
        }

        private void StartModIo(JObject request)
        {
            if (!(request["modio"] is JObject modio))
            {
                Done();
                return;
            }
            string tokenFile = (string)modio["tokenFile"] ?? DefaultTokenFile;
            if (!File.Exists(tokenFile))
            {
                Plugin.Log($"No mod.io token in {tokenFile}: skipping mod.io");
                Done();
                return;
            }
            string token = File.ReadAllText(tokenFile).Trim();
            string changelog = Text(request, "changelog") ?? "";
            _modio = System.Threading.Tasks.Task.Run(() => ModIo.Upload((string)modio["game"], (string)modio["mod"], (string)modio["zip"],
                (string)modio["version"] ?? "", changelog, modio["active"]?.Value<bool>() ?? true, (string)modio["metadata"], token));
        }

        private void Done()
        {
            _busy = false;
            foreach (string outcome in _outcomes) Plugin.Log(outcome);
            File.WriteAllText(Result, string.Join("\n", _outcomes) + "\n");
        }
    }
}
