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

Only `mod` is required. What the request leaves out stays as the last upload from the game's panel left it.

- **Steam.** The first upload of a mod goes through the game's panel, which creates the Workshop item. Steam has to be running and signed in. `"steam": false` skips it.
- **mod.io.** Add `modio` to the request and put an OAuth2 access token with write access (an API key can only read) in `~/.config/timberborn-mod-uploader/modio-token` (under your user profile on Windows), or in the file `modio.tokenFile` names. Without the token it's skipped.
- **Required items.** `Dependencies.json`, next to the mod's DLL, maps a mod id to Workshop items. After each upload of that mod, from the panel or a request, they're added as its required items, which the panel can't do.

## Building

Copy `env.props.<os>-template` to `env.props`, set the paths, and run `dotnet build`. The mod installs itself into your Mods folder. `Tools/make_cover.py` draws the cover, with the brush strokes of Timber Empires' icons (it finds that repo next to this one).

MIT licensed.
