# Mod Uploader

A Timberborn mod for release scripts. It uploads a mod to the Steam Workshop, and to mod.io, from a request file instead of the game's upload panel.

Put `ModUploader-request.json` in your Timberborn folder (`~/Documents/Timberborn`, `%USERPROFILE%\Documents\Timberborn` on Windows) and open the game. At the main menu the mod reads it, uploads, and writes the outcome to `ModUploader-request.json.result`, one line per target: `steam ok <item id>`, `modio ok <file id>` or `<target> failed <reason>`. `examples/upload.sh` (macOS) and `examples/upload.ps1` (Windows, untested) are release scripts that does this: it starts the game, waits for the result and closes the game again.

```json
{
  "mod": "a mod's id or folder name",
  "title": "the Workshop item's title",
  "description": "...", "descriptionFile": "a file with it",
  "changelog": "...", "changelogFile": "a file with it",
  "preview": "path to the preview image",
  "visibility": "Private | Friends | Public | Unlisted",
  "tags": ["the chosen tags"],
  "content": "the folder to upload, the mod's folder in Mods by default",
  "steam": true,
  "modio": { "game": 3659, "mod": 123, "zip": "path", "version": "1.0", "active": true, "metadata": "...", "tokenFile": "..." }
}
```

Real ones: [Timber Empires](https://github.com/FezVrasta/TimberEmpires/blob/main/Tools/Package/upload.sh) and [BeaverBuddies](https://github.com/FezVrasta/BeaverBuddies-CoOp-PvP-Edition/blob/master/Tools/Package/upload.sh) upload through it with their own `upload.sh`.

Only `mod` is required, and `create` for a mod's first upload. What the request leaves out is sent as the last upload left it (the mod's manifest description and thumbnail, by default). To leave the Workshop's description, visibility, preview or tags alone, set `updateDescription`, `updateVisibility`, `updatePreview` or `updateTags` to `false`: `examples/changelog-only.json` uploads just the content and the change note. `examples/request.json` sets everything. The title is always sent.

- **Steam.** Steam has to be running and signed in. A mod with no Workshop item gets one when the request sets `"create": true` (private unless `visibility` says otherwise). `"steam": false` skips it.
- **mod.io.** Add `modio` to the request and put an OAuth2 access token with write access (an API key can only read) in `~/.config/timberborn-mod-uploader/modio-token` (under your user profile on Windows), or in the file `modio.tokenFile` names. Without the token it's skipped.
- **Required items.** `Dependencies.json`, next to the mod's DLL, maps a mod id to Workshop items. After each upload of that mod, from the panel or a request, they're added as its required items, which the panel can't do.

## Building

Copy `env.props.<os>-template` to `env.props`, set the paths, and run `dotnet build`. The mod installs itself into your Mods folder. `Tools/make_cover.py` draws the cover, with the brush strokes of Timber Empires' icons (it finds that repo next to this one).

MIT licensed.
