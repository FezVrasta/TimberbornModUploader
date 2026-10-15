# Mod Uploader

A Timberborn mod for release scripts. A request file in `~/Documents/Timberborn/ModUploader-request` makes the main menu upload a mod to the Steam Workshop (and to mod.io) without the upload panel. The format is at the top of `UploadRequests.cs`.

- **Steam.** Updates the item the game's panel created once, as the panel's toggles were left.
- **mod.io.** Optional: only when the request carries a zip and `~/.config/timberempires/modio-token` exists.
- **Required items.** `Dependencies.json` maps a mod id to Workshop items added as its required items after each upload.

Used by `Tools/Package/upload.sh` in Timber Empires and BeaverBuddies.
