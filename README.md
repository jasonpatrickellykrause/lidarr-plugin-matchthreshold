# Match Threshold for Lidarr

Lidarr rejects an import when the files don't match the MusicBrainz release closely enough:

```text
Album match is not close enough: 78.8% vs 80% [artist, album, country]
Worst track match: 55.0% vs 60% [track title]
```

The 80% and 60% values are hardcoded in Lidarr. This plugin makes them settings.

## Install

1. In Lidarr, go to **System > Plugins**.
2. Paste `https://github.com/matrix-runout/lidarr-plugin-matchthreshold` and select **Install**.
3. Restart Lidarr.

Plugins need a Lidarr build with plugin support (the nightly/plugins branch).

### Installed before the move to matrix-runout?

This repository moved to `matrix-runout/lidarr-plugin-matchthreshold` from its previous GitHub account. Lidarr keeps each plugin in a folder named after the repository owner, so an update from the new location installs a second copy next to the old one. Reinstall once to avoid that:

1. In **System > Plugins**, uninstall Match Threshold.
2. Install it again from `https://github.com/matrix-runout/lidarr-plugin-matchthreshold`.
3. Restart Lidarr.

Your Match Threshold settings are kept.

## Configure

1. Back up your music library and your Lidarr database.
2. Go to **Settings > Metadata** and open **Match Threshold**.
3. Select **Enable**.
4. Set the values.
5. If any value is below Lidarr's default, select **Backed up and accept data-loss risk**. You can't save lowered values without it.
6. Save.

> **Warning:** This plugin can lead to data loss. A lowered minimum lets Lidarr import the wrong release, and a wrong import can replace or remove files you already have. Keep a current backup and a recycle bin (Settings > Media Management).

While **Backed up and accept data-loss risk** is cleared, any value below Lidarr's default is ignored and Lidarr's 80% and 60% are used. Lidarr logs a warning once when this happens. Values above the defaults don't need the checkbox.

The lowest values allowed are 60% for album match and 40% for track match. Below those, a "match" is usually a different album by the same artist, or a different song. A value saved below them by an earlier version is treated as the lowest allowed value.

There are two sets of minimums. Which set applies depends on whether the album already has files:

| Setting | Lidarr default | Lowest allowed | Applies to |
|---|---|---|---|
| Missing albums: minimum album match (%) | 80 | 60 | Downloads of albums with no files yet, and files already in your library when Lidarr rescans them. Below this, the import is rejected with "Album match is not close enough". |
| Missing albums: minimum track match (%) | 60 | 40 | Downloads of albums with no files yet. Below this for any track, the import is rejected with "Worst track match". |
| Replacing files: minimum album match (%) | 80 | 60 | Downloads of albums that already have files, such as quality upgrades. |
| Replacing files: minimum track match (%) | 60 | 40 | Downloads of albums that already have files. |

A common setup is to lower only the missing-albums values, for example to 75, and leave replacing files at 80 and 60. Missing albums then fill in more easily, and a loose match can't overwrite music you already have.

When the two sets differ, a rejection message ends with the set that was used, for example `(album already has files, replacing-files setting used)`. If the plugin can't check the album's existing files, it uses the stricter value of each pair.

Changes apply to the next import. You don't need to restart.

Saving a value below Lidarr's default shows a warning in the dialog. You also get a warning when replacing files is set looser than missing albums. Select **Save** again to confirm. On the Metadata page, the card lists the settings under "Metadata". Lidarr's card shows setting names only, not their values.

While the entry is disabled, the plugin uses Lidarr's built-in 80% and 60%, so the result is the same as without the plugin.

The Match Threshold entry doesn't write any metadata files. It sits under Metadata only because that's the place Lidarr gives a plugin a settings page.

## Choosing a value

Lowering the minimum lets more albums import automatically. It also lets more wrong matches import, such as a different edition, a regional release, or a different album with a similar name. Lower it a little at a time. Something like 75% fixes most near misses caused by a missing country or a slightly different album title.

The bracketed list in the rejection message shows what cost points. Fixing the cause is often a better option than lowering the threshold:

- **country**: Tag the files with the release country, or pick a release with a country set in MusicBrainz.
- **artist** or **album**: Retag the files to match MusicBrainz, for example with MusicBrainz Picard.

To import a single album without changing any setting, use **Manual Import**.

## How it works

Lidarr runs a list of import checks, and every check must pass. A plugin can add a check but has no supported way to remove a built-in one. This plugin:

1. Adds `ConfigurableCloseAlbumMatchSpecification`, a copy of Lidarr's `CloseAlbumMatchSpecification` that reads its two thresholds from the settings above.
2. At startup, removes Lidarr's built-in `CloseAlbumMatchSpecification` from the import decision maker's list of checks.

Step 2 uses reflection on a private field (`ImportDecisionMaker._albumSpecifications`). If a future Lidarr version renames that field or class, the plugin logs an error at startup and leaves the built-in check in place. Imports keep working with Lidarr's 80% threshold, and your setting has no effect until the plugin is updated. Look for `Match Threshold:` in the Lidarr log to see which happened:

- `replaced Lidarr's built-in album match check with the configurable one`: working.
- `could not find Lidarr's album match checks`: this Lidarr version isn't supported yet.

## Build

```sh
python3 scripts/prepare-lidarr.py   # downloads and verifies Lidarr 3.1.3 reference assemblies
dotnet build MatchThreshold.sln -c Release
dotnet test tests/MatchThreshold.Test/MatchThreshold.Test.csproj -c Release
```

The plugin package is written to `_temp/bin/Release/net8.0/MatchThreshold/Lidarr.Plugin.MatchThreshold.net8.0.zip`. For Lidarr 4.x, build with `-p:PluginTargetFramework=net10.0` against Lidarr 4.x reference assemblies; see `.github/workflows/build.yml`.
