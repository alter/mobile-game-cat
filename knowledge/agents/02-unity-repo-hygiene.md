# Unity repository hygiene for agent work: overview as of 2026-08-24 (Unity 6.3 LTS stack)

Date material was collected: 2026-08-24. Stack version the data was collected for: Unity 6.3 LTS (6000.3).

## In brief

- `.unity`, `.prefab`, `.asset` files are YAML with internal identifiers `fileID` (an object's number within the file) and `guid` (the asset's global identifier, stored in the corresponding `.meta` file). Any tool that edits these files not through the Unity API but as plain text/via copying risks desynchronizing these identifiers.
- A `.meta` file is required for every file and folder in `Assets` — if it's lost, Unity creates a new one and deletes the old one, causing all existing references to that asset by the old GUID to become "broken." This is documented Unity behavior, not a hypothesis.
- Force Text (Asset Serialization Mode) and Visible Meta Files are enabled in `Edit > Project Settings > Editor` and `Edit > Project Settings > Version Control` respectively; since February 2019, Force Text has been the default for new projects.
- The official `.gitignore` template for Unity lives in the `github/gitignore` repository and is already used as the industry standard; its full text is given verbatim below.
- The UnityYAMLMerge library ships directly inside the Unity Editor and is hooked up via `.gitconfig`; the binary path on macOS when installed via Unity Hub is `/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/Tools/UnityYAMLMerge` (when installed not via Hub, official documentation gives a different path — `/Applications/Unity/Unity.app/Contents/Helpers/UnityYAMLMerge`).
- The repository must contain `ProjectSettings/` and `Packages/manifest.json` (and `packages-lock.json`) — without them the project opens with different input, physics, tag, and layer settings; `Library/`, `Temp/`, `Obj/`, `Build*/`, `Logs/`, `UserSettings/` must never go into git.
- Practices that reduce risk from agents: moving data into ScriptableObjects instead of duplicating it across scenes and prefabs, building UI on UI Toolkit (UXML/USS) instead of heavy prefab hierarchies, assembling part of a scene by code — all of this reduces the volume of YAML that could even be touched by a bad automatic edit.
- For CI, it's reasonable to: cache `Library/`, run the Unity Test Runner (EditMode/PlayMode) mandatorily via `game-ci/unity-test-runner`, add a separate check for missing scripts/references, and architecturally separate the domain layer from `UnityEngine`-dependent code.

## 1. Unity's file format: why an agent breaks it

`.unity` (scenes), `.prefab` (prefabs), and `.asset` (ScriptableObject assets and others) files in text serialization mode are YAML documents, where each Unity object (GameObject, Transform, MonoBehaviour, etc.) is represented by a YAML block with a numeric `fileID`, unique within that file. References between objects within one file (for example, a Transform's reference to its parent and children) are encoded via `fileID`. References to objects located in other files (a material, texture, script, another prefab) are encoded as a `fileID` + `guid` pair, where `guid` is the global identifier of the target asset.

Official Unity documentation describes the origin and role of GUIDs and `.meta` files as follows, verbatim:

> "As part of the importing process, Unity creates metadata about any assets you import into your project. The metadata contains information such as the asset's import settings, and where your project uses the asset. When you import an asset, Unity does the following: Assigns the asset a unique ID. Creates a .meta file for the asset. Processes the asset."

And further, on the ID-assignment mechanism itself:

> "The Unity Editor frequently checks the contents of the Assets folder against the list of assets it already knows about. When you place an asset in the Assets folder, Unity detects that you have added a new file and assigns a unique ID to the asset. This is an ID that Unity uses internally to reference the asset, so that it can move or rename the asset without breaking anything."

Source: [Unity - Manual: Asset metadata (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/AssetMetadata.html).

### Why an agent (or any external tool) breaks them

The key reason: Unity itself manages the "file path ↔ GUID" correspondence only when changes happen through the editor itself (drag-and-drop, renaming in the Project Window, etc.). If a file or folder is moved or renamed bypassing Unity — for example, by an ordinary text-editor file tool or an agent's script — the `.meta` file might not follow the asset, and then the following happens, verbatim per the documentation:

> "Meta files contain important information about how the asset is used in your project, and they must stay with the asset file they relate to. If you move or rename an asset within the Project window, Unity automatically moves or renames the corresponding .meta file. However, if you move or rename an asset outside of Unity, you must move or rename the .meta file to match. If an asset loses its .meta file, any reference to that asset is broken in your project. In this situation, Unity generates a new .meta file for the moved or renamed asset as if it's a brand new asset, and deletes the old .meta file."

And the consequences by asset type specifically:

> "If a texture asset loses its .meta file, any materials that use that texture lose their reference to that texture. To fix it, you must manually re-assign that texture to any materials which require it. If a script asset loses its .meta file, any GameObjects or Prefabs that have that script assigned instead have an unassigned script component, and lose their functionality. To fix it, you must manually re-assign that script to any GameObjects which require it."

Source: same, [docs.unity3d.com/6000.3/Documentation/Manual/AssetMetadata.html](https://docs.unity3d.com/6000.3/Documentation/Manual/AssetMetadata.html).

The second channel for breakage is direct editing of the scene/prefab YAML file's text by hand or via a generic text-editing tool, rather than through Unity's serializer. If the YAML structure is disrupted (mismatched indentation, a duplicate `fileID`, a corrupted anchor), Unity either won't be able to open the file or will silently create a desync between objects. This is exactly why known rule sets for AI agents working with Unity through MCP separately forbid the agent from touching `Assets` contents with generic file tools (`edit_file`, `apply`, `copy`, `move`) — see details and source in the `01-unity-mcp.md` file, "Constraints and dangers" section.

Even empty folders become a source of problems when working with version control systems, because many VCSes (including git) don't store empty directories as such — only files. Unity explicitly describes special behavior for this case:

> "Unity assigns each folder in your project's Assets folder its own .meta file. However, some version control systems (VCS) can't store empty folders. When you add or delete an empty folder from your project, the VCS stores the .meta file as added or removed, but doesn't store the change of adding or removing the folder itself."

Source: same, [docs.unity3d.com/6000.3/Documentation/Manual/AssetMetadata.html](https://docs.unity3d.com/6000.3/Documentation/Manual/AssetMetadata.html).

## 2. Force Text Serialization and Visible Meta Files

Both settings relate to how Unity stores scenes, prefabs, assets, and metadata on disk, and both are critical for git to be able to show a meaningful diff at all and allow a conflict to be merged manually.

**Visible Meta Files.** Enabled in `Edit > Project Settings > Editor`, under Version Control, the "Visible Meta Files" value (in newer editor versions this same toggle is also available as part of the `Version Control` settings). When enabled, Unity places a `.meta` file next to each asset on disk in the open, rather than hiding it in an internal cache. This is a mandatory setting if the project is under git/any other VCS at all — without it, `.meta` files are unavailable for commit, and GUID references, accordingly, aren't versioned along with the assets themselves.

**Force Text (Asset Serialization Mode).** Enabled in the same place, `Edit > Project Settings > Editor`, under Asset Serialization, the "Force Text" value. In this mode Unity writes `.unity`, `.prefab`, `.asset`, and `.meta` files in text YAML format instead of binary. This makes the content readable and, more importantly, available for line-by-line diffing and (sometimes) manual or automatic merging — a binary format is categorically unsuited for this, because a binary conflict can't be shown line by line and almost never can be merged by hand.

According to an independent technical review (JetBrains, ReSharper for Unity plugin documentation), since February 2019 Force Text has been the default for new Unity projects, but older projects created earlier may have kept binary mode and need to be switched manually.

The official Unity page on the contents of the Asset Database (current version 6000.3) describes the connection between the setting and file readability, verbatim (paraphrased, since the page returns content via a proxy model): text file types (scenes, prefabs, materials, ScriptableObject assets) are human-readable if Asset Serialization Mode is set to the default Force Text, whereas binary files like textures or audio don't become readable under any serialization mode. Source: [Unity - Manual: Contents of the Asset Database (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/asset-database-contents.html).

The official Smart Merge page confirms the connection between the settings and the merge tool's operation: for UnityYAMLMerge to be able to do anything at all, the files must already be in text YAML format — otherwise there's nothing to compare. Source: [Unity - Manual: Smart merge (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/SmartMerge.html).

Practical recommendation, confirmed by numerous independent practitioners (the MRTK community, guides on configuring Unity for VCS): both settings are enabled once when the project is created and committed as part of `ProjectSettings/EditorSettings.asset`, so that all team members and all agents work in the same serialization mode without manual configuration on each machine.

## 3. Git and Unity

### 3.1. The complete official `.gitignore`

Below is the verbatim contents of the `Unity.gitignore` file from the official `github/gitignore` repository, obtained directly from `raw.githubusercontent.com` on 2026-08-24:

```gitignore
# This .gitignore file should be placed at the root of your Unity project directory
#
# Get latest from https://github.com/github/gitignore/blob/main/Unity.gitignore
#
# Recommended: add any editor/OS/tool-specific ignore rules from the Global/ templates as needed.
# See: https://github.com/github/gitignore/tree/main/Global
#
.utmp/
/[Ll]ibrary/
/[Tt]emp/
/[Oo]bj/
/[Bb]uild/
/[Bb]uilds/
/[Ll]ogs/
/[Uu]ser[Ss]ettings/
*.log

# By default unity supports Blender asset imports, *.blend1 blender files do not need to be commited to version control.
*.blend1
*.blend1.meta

# MemoryCaptures can get excessive in size.
# They also could contain extremely sensitive data
/[Mm]emoryCaptures/

# Recordings can get excessive in size
/[Rr]ecordings/

# Uncomment this line if you wish to ignore the asset store tools plugin
# /[Aa]ssets/AssetStoreTools*

# Autogenerated Jetbrains Rider plugin
/[Aa]ssets/Plugins/Editor/JetBrains*
# Jetbrains Rider personal-layer settings
*.DotSettings.user

# Visual Studio cache directory
.vs/

# Gradle cache directory
.gradle/

# Autogenerated VS/MD/Consulo solution and project files
ExportedObj/
.consulo/
*.csproj
*.unityproj
*.sln
*.slnx
*.suo
*.tmp
*.user
*.userprefs
*.pidb
*.booproj
*.svd
*.pdb
*.mdb
*.opendb
*.VC.db

# Unity3D generated meta files
*.pidb.meta
*.pdb.meta
*.mdb.meta

# Unity3D generated file on crash reports
sysinfo.txt

# Mono auto generated files
mono_crash.*

# Builds
*.apk
*.aab
*.unitypackage
*.unitypackage.meta
*.app

# Crashlytics generated file
crashlytics-build.properties

# TestRunner generated files
InitTestScene*.unity*

# Addressables default ignores, before user customizations
/ServerData
/[Aa]ssets/StreamingAssets/aa*
/[Aa]ssets/AddressableAssetsData/link.xml*
/[Aa]ssets/Addressables_Temp*
# By default, Addressables content builds will generate addressables_content_state.bin
# files in platform-specific subfolders, for example:
# /Assets/AddressableAssetsData/OSX/addressables_content_state.bin
/[Aa]ssets/AddressableAssetsData/*/*.bin*

# Visual Scripting auto-generated files
/[Aa]ssets/Unity.VisualScripting.Generated/VisualScripting.Flow/UnitOptions.db
/[Aa]ssets/Unity.VisualScripting.Generated/VisualScripting.Flow/UnitOptions.db.meta
/[Aa]ssets/Unity.VisualScripting.Generated/VisualScripting.Core/Property Providers
/[Aa]ssets/Unity.VisualScripting.Generated/VisualScripting.Core/Property Providers.meta

# Auto-generated scenes by play mode tests
/[Aa]ssets/[Ii]nit[Tt]est[Ss]cene*.unity*

# Auto-generated cache in Assets folder
/[Aa]ssets/[Ss]ceneDependencyCache*
```

Source: [github.com/github/gitignore/blob/main/Unity.gitignore](https://github.com/github/gitignore/blob/main/Unity.gitignore).

Note the use of paired character classes like `[Ll]ibrary/` — this guards against different path casing across OSes and against cases where some tool creates a `library` folder with a lowercase letter.

### 3.2. Git LFS for art and audio

No official Unity page specifically dedicated to configuring Git LFS was found within this research; below are mutually consistent recommendations from independent practical sources (Medium, Hextant Studios, riptutorial) that converge on the same basic recipe.

Installing and enabling LFS for file types is done once with commands like:

```bash
git lfs install
git lfs track "*.psd"
git lfs track "*.png"
git lfs track "*.fbx"
git lfs track "*.wav"
```

After running `git lfs track`, Git creates or updates a `.gitattributes` file — this is exactly what must be committed to the repository so that the LFS rule applies the same way for all participants and agents. A typical set of categories found across several independent guides: images (`*.jpg`, `*.png`, `*.psd`, `*.tif`, `*.cubemap`), audio (`*.mp3`, `*.wav`, `*.ogg`), video (`*.mp4`, `*.mov`), 3D models (`*.fbx`, `*.blend`, `*.obj`) — each line gets `filter=lfs diff=lfs merge=lfs -text`.

A separate practical warning from the same circle of sources: it's recommended not to route the `LightingData.asset` file through the `unityyamlmerge` filter, but to treat it as an ordinary binary file — in practice teams have run into it getting corrupted when trying to merge it as YAML.

Sources: [Getting Started With Git LFS in Unity Without Wrecking Your Repo (Medium)](https://medium.com/@0xJake/getting-started-with-git-lfs-in-unity-without-wrecking-your-repo-89c1140cedbd), [.gitattributes for Unity Projects (Hextant Studios)](https://hextantstudios.com/unity-gitattributes/), [unity3d Tutorial: Using Git Large File Storage (LFS) with Unity (riptutorial)](https://riptutorial.com/unity3d/example/7178/using-git-large-file-storage--lfs--with-unity).

### 3.3. `.gitattributes` and the UnityYAMLMerge merge driver

The official Unity Manual page "Smart merge" (checked for version 6000.3, i.e. exactly Unity 6.3 LTS) gives the following instruction for Git verbatim:

> "Git: Add the following text to your .git or .gitconfig file:
> [merge]
> tool = unityyamlmerge
> [mergetool "unityyamlmerge"]
> trustExitCode = false
> cmd = '<path to UnityYAMLMerge>' merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED""

Source: [Unity - Manual: Smart merge (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/SmartMerge.html).

The official path to the UnityYAMLMerge binary, per the same source (scenario "Unity installed in the standard location," without Unity Hub):

```
Windows: C:\Program Files\Unity\Editor\Data\Tools\UnityYAMLMerge.exe
         (or C:\Program Files (x86)\Unity\Editor\Data\Tools\UnityYAMLMerge.exe)
macOS:   /Applications/Unity/Unity.app/Contents/Helpers/UnityYAMLMerge
```

The official documentation separately clarifies how to get to this path on macOS: "To access this folder from the Finder, right-click the Unity.app and select the Show Package Contents option." Same source.

In practice, almost all modern installations are done via Unity Hub, rather than as a standalone application at `/Applications/Unity/Unity.app` — and the Hub installation's path is different. According to independent but mutually consistent sources (the No Time to Make Games guide, a JetBrains YouTrack thread about UnityYAMLMerge support in Rider), the actual path on macOS when installed via Unity Hub looks like this:

```
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/Tools/UnityYAMLMerge
```

For example, for Rider the following case is documented: "UnityYamlMerge location on Mac: /Applications/Unity/Hub/Editor/2018.4.0f1/Unity.app/Contents/Tools/UnityYAMLMerge." Equivalents for other OSes with a Hub install: Windows — `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Data\Tools\UnityYAMLMerge.exe`; Linux — `/home/<user>/Unity/Hub/Editor/<version>/Editor/Data/Tools/UnityYAMLMerge`. Sources: [Tutorial: Setup Smart Merge for Unity Assets with Git (No Time to Make Games)](https://nagachiang.github.io/tutorial-setup-smart-merge-for-unity-assets-with-git/), [UnityYAMLMerge / Smart Merge support: RIDER-33411 (JetBrains YouTrack)](https://youtrack.jetbrains.com/issue/RIDER-33411/UnityYAMLMerge-Smart-Merge-support).

Practical conclusion: before configuring this, it's worth checking both path variants (`Contents/Tools` and `Contents/Helpers`) inside the specific Hub-installed Unity 6.3 LTS version, because the official documentation only describes the standalone-install scenario, and the location inside the `.app` bundle has historically changed between Unity versions.

For `unityyamlmerge` to actually be invoked automatically on `git merge`/`git rebase`, rather than only via a manual `git mergetool` call, the repository's `.gitattributes` additionally needs to specify the merge driver for the relevant extensions, for example:

```gitattributes
*.unity merge=unityyamlmerge eol=lf
*.prefab merge=unityyamlmerge eol=lf
*.asset merge=unityyamlmerge eol=lf
*.mat merge=unityyamlmerge eol=lf
*.anim merge=unityyamlmerge eol=lf
```

This is consistent with independent `.gitattributes` configurations found among several practitioners (Hextant Studios, public gist examples), where Unity's text YAML types are marked `merge=unityyamlmerge eol=lf`, while binary types are separately marked for the LFS filter (`filter=lfs diff=lfs merge=lfs -text`) — meaning one and the same `.gitattributes` file covers both LFS and smart-merge for different file categories at once.

The official Unity Smart Merge documentation also separately describes three modes of the tool's behavior in the `Edit > Project Settings > Version Control` settings, the Smart Merge field (available when a third-party VCS such as Perforce or UVCS is selected in the Mode field): "Off: use only the default merge tool set in the preferences with no smart merging. Premerge: enable smart merging, accept clean merges... Ask: enable smart merging but when a conflict occurs, show a dialog to let the user resolve it (this is the default setting)." Source: [Unity - Manual: Smart merge (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/SmartMerge.html).

An important practical caveat from independent sources, consistent with the tool's own logic: the result of `unityyamlmerge`'s work shouldn't be automatically committed without checking — the merged scene or prefab file should be opened in Unity itself to make sure the scene loads and contains no broken references, before finalizing the merge result.

## 4. What should and shouldn't be in the repository

The list of folders the official `.gitignore` (see section 3.1) excludes from the repository fully matches what Unity regenerates automatically every time the project is opened: `Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/`, `UserSettings/`. The source of this list is the same file [github.com/github/gitignore/blob/main/Unity.gitignore](https://github.com/github/gitignore/blob/main/Unity.gitignore), given verbatim above.

**`Library/`** — a local cache of imported assets and compiled builds. It's unambiguously not version-controlled: Unity automatically rebuilds it from the contents of `Assets/` and the `.meta` files the first time the project is opened. If this folder has already accidentally ended up in git, it's not enough to just add it to `.gitignore` — entries remain in the commit history, and actually shrinking the repository's size requires rewriting history (e.g., `git filter-repo`), which changes the hashes of every commit and requires everyone to re-clone the repository.

**`Temp/`, `Obj/`** — temporary files from the current editor/compiler session, not needed even locally once Unity is closed.

**`UserSettings/`** — a specific developer's personal editor settings (window layout, Layout state, etc.); unlike `ProjectSettings/`, this folder must not be placed under version control, because it describes one person's preferences rather than the project's behavior.

**`ProjectSettings/`** — must be committed. This stores input axes, physics layers, quality levels, Player Settings, tags — that is, behavior shared across the whole team and the agent. Losing this folder means the reopened project will behave differently, and the difference can be subtle enough not to be noticed immediately.

**`Packages/manifest.json` and `Packages/packages-lock.json`** — must be committed. These are the Unity Package Manager's dependency lists: without `manifest.json`, Unity doesn't know which packages and which versions should be installed in the project, and `packages-lock.json` pins the exact resolved versions, similar to lock files in npm/yarn.

**`Assets/` along with all `.meta` files inside it** — this is, essentially, the entire content of the project: code, scenes, prefabs, materials, textures. Not optional under any scenario.

Summary table:

| Path | In the repository? | Reason |
|---|---|---|
| `Assets/` (including all `.meta`) | Yes | The project's core content |
| `ProjectSettings/` | Yes | Shared project settings (input, physics, tags, quality) |
| `Packages/manifest.json`, `Packages/packages-lock.json` | Yes | UPM dependencies and their pinned versions |
| `Library/` | No | Import cache, rebuilt automatically |
| `Temp/`, `Obj/` | No | Temporary files from the current session |
| `Build/`, `Builds/` | No | Build artifacts |
| `Logs/` | No | Compiler/editor logs |
| `UserSettings/` | No | A specific developer's personal settings |

## 5. Practices that reduce risk from agents

The common logic behind all three practices below is the same: the less significant data sits directly in scene and prefab YAML files, the lower the chance that a bad automatic edit (by an agent or a human) will corrupt something hard to recover, and the more compact the diffs are for code review.

### 5.1. Data in ScriptableObjects and JSON, not in scenes

Unity's official engineering blog states this directly as a recommended pattern:

> "ScriptableObjects are perfect containers for static data" — and further: splitting data out into ScriptableObjects "help to split your GameObjects into multiple smaller files... reducing the risk of merge conflicts."

Source: [Achieve better Scene workflow with ScriptableObjects (blogs.unity3d.com / unity.com)](https://unity.com/blog/2020/07/01/achieve-better-scene-workflow-with-scriptableobjects/).

The same source explains the mechanism through the duplication problem: if data (item stats, configuration) is stored directly in a MonoBehaviour on a prefab, every instance of the prefab gets its own copy of that data, and editing one value requires finding and syncing the copies manually; when the data is moved out to a ScriptableObject, all instances reference the same asset by GUID, and the scene/prefab object itself contains only that one reference instead of a whole block of values.

An independent practitioner adds an organizational argument to this, verbatim:

> "Each prefab is saved to its own file. If you change something in the prefab, only the prefab's file is changed."

Source: [Merge Conflicts in Unity - How to avoid them? (Manuel Rauber)](https://manuel-rauber.com/2023/01/25/merge-conflicts-in-unity-how-to-avoid-them/).

The same author states an organizational rule directly applicable to agent work as well: "Each developer should work in his own working scene where no other developer will make a change ever" — meaning the scene an agent is editing at a given moment shouldn't be the same scene a human or another process is editing in parallel.

### 5.2. UI in UXML/USS, not in prefabs

No direct source formulating exactly "reducing risk from AI agents" via UI Toolkit was found within this research. But the independently confirmed connection is this: Unity's UI Toolkit describes the interface declaratively in text files `.uxml` (structure) and `.uss` (styles) — that is, with the same kind of tooling as HTML/CSS, instead of a GameObject hierarchy inside a heavy `.prefab`. This doesn't make UXML/USS immune to merge conflicts by themselves (these are also text files that can conflict), but it noticeably reduces the share of UI logic that's physically stored as an opaque graph of `fileID` references inside a `.prefab`, and thereby reduces the surface where GUID corruption specific to Unity's prefab format can occur.

Additionally, per the same sources on ScriptableObjects, UI Toolkit and data pair naturally: a UI Toolkit screen "builds itself only once and needs to be notified when the data has been altered" via an event on a ScriptableObject — meaning data binding happens through code and assets, not through manual references to specific GameObjects in a scene.

### 5.3. Assembling the scene by code

No dedicated specialized source describing programmatic scene assembly specifically as a defense against AI agents was found. The general principle is confirmed by the same logic as with ScriptableObjects: if scene objects are created and configured by code (for example, via `PrefabUtility.InstantiatePrefab` followed by configuring components in `Awake`/`Start`, or via a dedicated editor script that builds the level), then the `.unity` file itself holds less state — and therefore less that can be accidentally corrupted by a direct YAML edit. Practical compromise: the part of the scene that handles an artist's placement of static environment usually stays as an ordinary scene/prefabs, while repeatable or generated structure (an enemy pool, UI containers, service managers) is a good candidate for assembly by code at startup.

## 6. CI checks that catch agent breakage

### 6.1. Compilation and tests via `game-ci/unity-test-runner`

An officially documented and widely used way in the community to run the Unity Test Runner (EditMode and PlayMode modes) in GitHub Actions is the `game-ci/unity-test-runner` action. The basic form of the step, from the official GameCI documentation, verbatim in structure (not a literal quote, but a direct rendering of the documented parameters):

```yaml
- uses: actions/cache@v3
  with:
    path: path/to/your/project/Library
    key: Library-MyProjectName-TargetPlatform

- uses: game-ci/unity-test-runner@v4
  with:
    projectPath: path/to/your/project
    githubToken: ${{ secrets.GITHUB_TOKEN }}
```

Caching `Library/` in CI is officially recommended precisely because rebuilding this folder from scratch on every run is the slowest part of the process; the documentation separately notes that caching in this form applies to Unity projects, but not to packages. Source: [Test runner | GameCI](https://game.ci/docs/github/test-runner/), [github.com/game-ci/unity-test-runner](https://github.com/game-ci/unity-test-runner).

An important practical caveat from the project's own open issues: cases have been recorded where a compilation error didn't cause an explicit CI check failure — the runner showed "0/0 tests passed" instead of an explicit failure, even though the compiler log had `compilationhadfailure: True`. Conclusion for CI setup: it's not enough to rely only on "tests passed green" — compilation status should be checked separately, rather than relying on the test runner correctly propagating the compilation error upward on its own. Source: [Tests not running · Issue #105 · game-ci/unity-test-runner](https://github.com/game-ci/unity-test-runner/issues/105).

### 6.2. Checking for missing references

No direct official Unity tool for this specific check was found within this research. There are third-party open-source editor utilities that find missing scripts and references and can be embedded into CI as a separate step before the build — for example, `RimuruDev/Unity-MissingScriptsFinder` (36 stars on GitHub at the time of the 2026-08-24 check, MIT license, description: "Unity Missing Scripts Finder Editor Tool. Updated for Unity 6000 and above.", checked via `gh api repos/RimuruDev/Unity-MissingScriptsFinder`). Such a tool can be run in Unity's batch mode (`-batchmode -executeMethod`) as a CI step before the tests stage, to catch broken GUID references before they reach the tests or the build. Source: [github.com/RimuruDev/Unity-MissingScriptsFinder](https://github.com/RimuruDev/Unity-MissingScriptsFinder).

### 6.3. Banning `UnityEngine` in a separate directory (an architectural boundary)

The practice of moving domain logic into a separate assembly/directory that doesn't depend on `UnityEngine` is documented in several open sample repositories on clean architecture in Unity. For example, an independent sample repository on clean architecture states the layer-isolation principle like this: "In this architecture the components from an inner layer cannot speak with components in an outer layer, helping to keep our domain testable and decoupled from everything." Technically in Unity this is implemented via `.asmdef` (assembly definition files) — the assembly with domain logic simply isn't given a reference to the `UnityEngine`/`UnityEditor` assembly, and trying to write `using UnityEngine;` in a file of that assembly results in a compilation error, not merely a linter warning.

Within this research, no separately documented example was found of specifically a `grep` check for `using UnityEngine` in CI as a standalone third-party pattern — this could be a simple additional step (`grep -rl "using UnityEngine" path/to/DomainLayer/ && exit 1`) on top of the main protection via `.asmdef`, but as a separately documented practice it couldn't be confirmed by a source; the more reliable, source-confirmed mechanism is precisely restricting assembly references via `.asmdef`, which Unity checks automatically at compile time, without an additional `grep` step.

### 6.4. Combined set of CI checks

Based on the collected material, a reasonable minimal set of checks for a project where scenes and code are edited by an agent:

1. Compiling the project in Unity's batch mode (`-batchmode -quit -logFile - -projectPath ...`) as a separate step, independent of the Test Runner's status — so as not to rely on a compilation failure automatically failing the tests (see 6.1).
2. Running the Unity Test Runner (EditMode at minimum, PlayMode if the tests need a runtime) via `game-ci/unity-test-runner` with `Library/` caching.
3. A separate step to search for missing references/scripts using an editor tool like `Unity-MissingScriptsFinder`, run in batch mode before/after the main build.
4. Restricting dependencies via `.asmdef`: the domain assembly must not reference `UnityEngine`/`UnityEditor` — Unity checks this itself at compile time; an additional `grep` step can serve as a cheap safety net, but doesn't replace the `.asmdef` boundary.
5. Manual or automatic verification that merge conflicts in `.unity`/`.prefab` files are either absent or resolved via `UnityYAMLMerge`, rather than blindly accepted from one side (`ours`/`theirs`) — see section 3.3 on the necessity of opening the merged scene in Unity itself before committing.

## Sources

- [Unity - Manual: Asset metadata (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/AssetMetadata.html)
- [Unity - Manual: Contents of the Asset Database (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/asset-database-contents.html)
- [Unity - Manual: Smart merge (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/SmartMerge.html)
- [github.com/github/gitignore — Unity.gitignore](https://github.com/github/gitignore/blob/main/Unity.gitignore)
- [Achieve better Scene workflow with ScriptableObjects (unity.com)](https://unity.com/blog/2020/07/01/achieve-better-scene-workflow-with-scriptableobjects/)
- [Merge Conflicts in Unity - How to avoid them? (Manuel Rauber)](https://manuel-rauber.com/2023/01/25/merge-conflicts-in-unity-how-to-avoid-them/)
- [Getting Started With Git LFS in Unity Without Wrecking Your Repo (Medium)](https://medium.com/@0xJake/getting-started-with-git-lfs-in-unity-without-wrecking-your-repo-89c1140cedbd)
- [.gitattributes for Unity Projects (Hextant Studios)](https://hextantstudios.com/unity-gitattributes/)
- [unity3d Tutorial: Using Git Large File Storage (LFS) with Unity (riptutorial)](https://riptutorial.com/unity3d/example/7178/using-git-large-file-storage--lfs--with-unity)
- [Tutorial: Setup Smart Merge for Unity Assets with Git (No Time to Make Games)](https://nagachiang.github.io/tutorial-setup-smart-merge-for-unity-assets-with-git/)
- [UnityYAMLMerge / Smart Merge support: RIDER-33411 (JetBrains YouTrack)](https://youtrack.jetbrains.com/issue/RIDER-33411/UnityYAMLMerge-Smart-Merge-support)
- [Test runner | GameCI](https://game.ci/docs/github/test-runner/)
- [github.com/game-ci/unity-test-runner](https://github.com/game-ci/unity-test-runner)
- [Tests not running · Issue #105 · game-ci/unity-test-runner](https://github.com/game-ci/unity-test-runner/issues/105)
- [github.com/RimuruDev/Unity-MissingScriptsFinder](https://github.com/RimuruDev/Unity-MissingScriptsFinder)
- [01-unity-mcp.md — "Constraints and dangers" section (internal link within this same knowledge base)](./01-unity-mcp.md)
