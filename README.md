# ODDS CAB

An Istanbul taxi and puzzle game built with Unity, with passenger stories, city scenery, ferry crossings, and Turkish/English presentation.

## Open the project

1. Install Unity **6000.6.6f1** through Unity Hub. Android builds need Android Build Support, SDK/NDK, and OpenJDK.
2. Clone this repository and open its root folder in Unity Hub.
3. Allow Unity to restore the packages and import the assets.
4. Open `Assets/Scenes/SampleScene.unity` and enter Play mode.

The project targets portrait orientation. The editor menu **ODDS CAB > Build Android APK** invokes the included Android build script.

## Repository contents

- `Assets`: game scripts, runtime Resources, the game scene, rendering settings, and their asset dependencies and `.meta` files.
- `Packages`: Unity package manifest and lock file.
- `ProjectSettings`: settings required to reopen the project.

This is a curated export of the working project. Unity caches, builds, logs, personal editor settings, recovery scenes, source archives, backups, and unreferenced generated content are excluded. Dynamic Resources and fallback models are retained to avoid breaking runtime loading.

## Validation and licensing

The original project had no Unity script-compilation failure when this export was prepared. Assets were selected using Unity AssetDatabase dependency traversal, project-setting GUID references, and runtime Resources roots. A fresh import/build of this exported copy has not yet been completed.

No project-wide open-source license has been granted by this repository. Public visibility alone does not grant reuse rights. Third-party fonts, plugins, and other assets retain their own licenses; included font license notices must be preserved. See `THIRD_PARTY_NOTICES.md` before redistribution.
