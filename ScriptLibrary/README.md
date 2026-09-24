# Script library

Do not put this folder inside Assets. It intentionally contains duplicate class names as reference editions.

Read [the beginner guide](../Docs/BEGINNER_CODE_GUIDE.md). Save and close Unity, then double-click **Switch-Script-Edition.cmd** to choose Basic/Advanced and one module. The tool copies complete matching files and metadata and removes obsolete companions. Your scene and artwork are retained. Each edition is a reference copy: edit the active `Assets/Game/Scripts` files instead.

Modules: Movement; Tool; CellWorld; Plants. They share stable calls and can be switched independently. The archived Advanced movement works with the Basic CellWorld, including its basic water levels; its full fluid behavior needs the Advanced CellWorld.

The initial full source backup is outside this repository in `../BURN_I_Feature_Library/Basic_Edition_Backup`. Automatic switch backups are in that library's `Script_Switch_Backups`.
