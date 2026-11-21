# CLAUDE.md

Dino Runner: a 2D endless runner (Chrome-dino style) for Android and WebGL.
Unity **6000.6.10f1** (Unity 6.6), Built-in Render Pipeline, C#.
Architecture, life cycle and "how to add a coin / effect" guides live in [README.md](README.md). Read it before changing gameplay code.

## Use the Unity skills: always

Unity's official Claude Code plugin (`unity@claude-plugins-official`) is installed. **Use its skills for any Unity work instead of improvising**:

- **`unity:unity-cli`**: the default for anything touching the Editor: status, recompile, Play mode, console, screenshots, tests, builds, running C#. Load it before running any `unity` command.
- **`unity-pipeline`** (project skill in `.claude/skills/`): the deeper command reference for the `com.unity.pipeline` package installed in this project.
- Task skills when they match, e.g. `unity:ui-ugui`, `unity:optimize-text-mesh-pro`, `unity:manage-sprite-atlas`, `unity:2d-pixel-perfect`, `unity:create-2d-physics`, `unity:physics-3d-collision`, `unity:unity-package-management` (add/remove UPM packages through it, not by hand-editing `Packages/manifest.json`).

Rules:

- **Run `unity status` first.** If an Editor is open on this project (usually it is), drive it with `unity command …`. Never hand-edit `.unity`, `.prefab` or `.asset` YAML while a live Editor is reachable.
- Pass `--caller plugin --skill <skill-name>` on every `unity command`, **before** any `--` fence; Editor-side parameters go after `--`.
- In zsh, don't put flags in a variable (`$U`), because it is not word-split. Use `export UNITY_PROJECT_PATH=$PWD` or a shell function instead.
- A Play mode session the user started is theirs: read it, don't stop or change it.
- After a Play mode session you drove, revert `editor.playmode set_run_in_background -- --enabled false` and `editor set_auto_tick -- --enabled false`.
- A green compile is not proof. Verify gameplay changes in Play mode (frame count advancing, `console get -- --level warn`, `capture game_view`).

## Common commands

```bash
unity status                                     # Editor connected? look for "ready"
unity recompile                                  # does the code compile? (needs the open Editor)
unity command --caller plugin --skill unity-cli console get -- --tail 50 --level warn
unity command --caller plugin --skill unity-cli capture game_view -- --source camera --output-path Screenshots/shot.png
unity build . --target StandaloneOSX --output-path Builds/macOS/DinoRunner.app \
  --provenance-path Builds/macOS/unity-build.provenance.json   # keep the manifest out of the .app (it breaks codesign)
```

- Editor menu / CI builds: `Assets/Editor/BuildScript.cs` (**Build → macOS / Windows / WebGL / Android / iOS Simulator**, or `-executeMethod App.Editor.BuildScript.BuildMacOS`). Output goes to `Builds/` (git-ignored).
- **Build → Run (<Platform>)** (`BuildRunMenu.cs`) is enabled only when that build exists:
  - macOS opens the app; Windows runs the `.exe` (Windows Editor only).
  - WebGL serves `Builds/WebGL` from a local server (`WebGLServer.cs`) and opens the browser.
  - Android installs and launches the APK on a device, booting the first AVD if none is connected (`AndroidLauncher.cs`).
  - iOS builds with `xcodebuild` and launches in the Simulator (`IOSSimulatorLauncher.cs`, needs Xcode).
- `unity build` can't build WebGL/Android/iOS while the Editor is open. Use the Build menu, or call `BuildScript` through `script eval`.
- Tests are PlayMode NUnit tests in `Assets/Tests/` (`App.Character.Dino.Tests`, `App.Spawner.Tests`). Run them in the open Editor: through the Pipeline (see `unity-cli`) or Rider's test runner. `unity test` refuses a project that is open in an Editor, so don't close the user's Editor to make room.

## Layout

- `Assets/App/Domains/*`: gameplay domains, each its own asmdef (`App.GameCore`, `App.Character[.Dino]`, `App.Spawner[.Coins|.Obstacle|.Composite]`, `App.Round`, `App.TopPanel`, `App.Popups`).
- `Assets/Infra/*`: controllers base (`IController` Start/Stop + `IDisposable`), tickable, observable, disposable helpers.
- `Assets/Plugins/`: vendored **Zenject** (DI), **UniRx**, **TextMesh Pro** resources. **UniTask** comes from UPM (git URL pinned to `#2.5.11`).
- `docs/`: GitHub Pages "Play online" WebGL build. `docs/Build/` is tracked on purpose. Don't delete or ignore it.

## Gotchas

- **Physics transforms aren't auto-synced** (`autoSyncTransforms` off). Setting `transform.position` on a GameObject with a Rigidbody doesn't move the physics body. Set `Rigidbody.position` too (see `SpawnView.OnEnable`), and get the components in `Awake`, not `Start`.
- **Pooled views can already be destroyed** when the scene unloads before a pool is disposed. Guard `Dispose()` with `if (this == null) return;`.
- **Use the current APIs:** `FindAnyObjectByType` / `FindObjectsByType<T>()` (no sort-mode argument), `Rigidbody.linearDamping` / `angularDamping`, and the generic `TreeView<int>`. The older forms are obsolete or fail to compile.
- **TextMesh Pro** is part of `com.unity.ugui` 2.x. The TMP essentials live in `Assets/Plugins/TextMesh Pro/`, not the default `Assets/TextMesh Pro/`. `TMP Settings.asset` must keep `assetVersion: 2`.
- **`.meta` files must be valid YAML.** A trailing whitespace-only line breaks the parser.
- Vendored UniRx/Zenject sources carry local patches for the current APIs. Keep them if you update those libraries.
