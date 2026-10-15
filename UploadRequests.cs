using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bindito.Core;
using Timberborn.Modding;
using Timberborn.SingletonSystem;
using Timberborn.SteamStoreSystem;
using Timberborn.SteamWorkshop;
using Timberborn.SteamWorkshopModUploadingUI;
using UnityEngine;

namespace ModUploader
{
    /**
     * A file named ModUploader-request in Documents/Timberborn starts an upload:
     *
     *   mod: a mod's id or folder name
     *   modio-game: the mod.io game id          (these three make the mod.io upload,
     *   modio-mod: the mod's id on mod.io        which also needs the token below)
     *   modio-zip: the zip to upload
     *   (an empty line)
     *   the change note
     *
     * The Steam Workshop item is updated from the mod's folder in Mods, with
     * name, description, visibility, tags and thumbnail as the last upload from
     * the game's panel left its toggles. Only items the panel created once
     * (workshop_data.json) can be updated. mod.io needs an OAuth2 access token
     * with write access in ~/.config/timberempires/modio-token; without one it's
     * skipped. ModUploader-request.result gets a line for each: "steam ok
     * <item id>", "modio ok <file id>", or "<target> failed <reason>".
     */
    public class UploadRequests : IUpdatableSingleton
    {
        private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Documents", "Timberborn");
        private static string Flag => Path.Combine(Folder, "ModUploader-request");
        private static string Result => Flag + ".result";
        private static string TokenFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".config", "timberempires", "modio-token");

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
            string[] lines = File.ReadAllLines(Flag);
            File.Delete(Flag);
            File.Delete(Result);
            var header = new Dictionary<string, string>();
            int i = 0;
            for (; i < lines.Length && lines[i].Trim().Length > 0; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon > 0) header[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
            }
            string note = string.Join("\n", lines.Skip(i)).Trim();
            _busy = true;
            _outcomes.Clear();
            try
            {
                header.TryGetValue("mod", out string id);
                UploadToSteam(id, note, header);
            }
            catch (Exception e)
            {
                _outcomes.Add($"steam failed {e.Message}");
                StartModIo(header, note);
            }
        }

        private void UploadToSteam(string id, string note, Dictionary<string, string> header)
        {
            if (!_container.GetInstance<SteamManager>().Initialized)
            {
                _outcomes.Add("steam failed Steam isn't running");
                StartModIo(header, note);
                return;
            }
            Mod mod = _modRepository.UserMods.FirstOrDefault(m => m.Manifest.Id == id || m.ModDirectory.OriginName == id);
            if (mod == null)
            {
                _outcomes.Add($"steam failed no mod {id} in Mods");
                StartModIo(header, note);
                return;
            }
            var uploadable = _container.GetInstance<SteamWorkshopUploadableModFactory>().Create(mod);
            if (!uploadable.ItemId.HasValue)
            {
                uploadable.Clear();
                _outcomes.Add($"steam failed {id} was never uploaded: do the first upload from the game's panel");
                StartModIo(header, note);
                return;
            }

            // As UploadPanelElements builds it, with the panel's toggles as they were left
            var builder = new SteamWorkshopUpdateRequest.Builder(uploadable.ItemId.Value, uploadable.Name).SetContentPath(uploadable.ContentPath);
            builder.SetChangelog(note);
            if (uploadable.UpdateDescription) builder.SetDescription(uploadable.Description);
            if (uploadable.UpdateVisibility) builder.SetVisibility(uploadable.Visibility);
            if (uploadable.UpdatePreview) builder.SetPreviewPath(uploadable.PreviewPath);
            if (uploadable.UpdateTags) builder.AddMandatoryTags(uploadable.MandatoryTags).AddChosenTags(uploadable.ChosenTags);
            var request = builder.Build();
            uploadable.OnUpdateRequestCreated(request);

            // The callback captures no Steam types: a field of one in a closure
            // fails to load on copies of the game without Steam
            Action clear = uploadable.Clear;
            ulong item = request.ItemId;
            Plugin.Log($"Uploading {mod.Manifest.Name} {mod.Manifest.Version} to item {item}");
            _container.GetInstance<SteamWorkshopItemUpdater>().Update(request, response =>
            {
                clear();
                _outcomes.Add(response.Successful ? $"steam ok {item}" : $"steam failed {response.ResultMessage}");
                StartModIo(header, note);
            });
        }

        private void StartModIo(Dictionary<string, string> header, string note)
        {
            if (!header.TryGetValue("modio-zip", out string zip) || !header.TryGetValue("modio-game", out string game) || !header.TryGetValue("modio-mod", out string mod))
            {
                Done();
                return;
            }
            if (!File.Exists(TokenFile))
            {
                Plugin.Log("No mod.io token: skipping mod.io");
                Done();
                return;
            }
            string token = File.ReadAllText(TokenFile).Trim();
            string version = header.TryGetValue("version", out string v) ? v : "";
            _modio = System.Threading.Tasks.Task.Run(() => ModIo.Upload(game, mod, zip, version, note, token));
        }

        private void Done()
        {
            _busy = false;
            foreach (string outcome in _outcomes) Plugin.Log(outcome);
            File.WriteAllText(Result, string.Join("\n", _outcomes) + "\n");
        }
    }
}
