# CLAUDE.md

Dino Runner: a 2D endless runner (Chrome-dino style) for Android and WebGL.
Unity **6000.6.10f1** (Unity 6.6), Built-in Render Pipeline, C#.
Architecture, life cycle and "how to add a coin / effect" guides live in [README.md](README.md). Read it before changing gameplay code.

## Project

- The game: the character runs automatically and the player jumps over obstacles (cacti, birds). Coins change the character's behavior for a limited time (`Slow`, `Fast`, `Fly`). Score and high score (`hiScore` in `PlayerPrefs`) are shown in the top panel; after losing, the Retry popup is shown. Single scene: `Assets/Game.unity`.
- Goal: publish on Google Play (Android first, iOS possibly later), then monetize. Android `applicationIdentifier` is `com.ivan.kadyka.runner`; it cannot change after the first Play upload, so it is decided during the rebrand.
- ⚠️ **Rebrand required before monetization.** The art, sounds and name are close to Chrome's T-Rex game (the project grew out of zigurous/unity-dino-game-tutorial). Do not add new assets in the Chrome Dino style, and do not use "T-Rex", "Chrome", "offline" or "no internet" in names, texts or store materials.

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

## Architecture rules

- **One domain, one assembly (`.asmdef`).** Domains depend on each other only through contract assemblies (`App.GameCore`, `App.Character`, `App.Spawner`), never on another domain's implementation.
- **Controller ↔ model ↔ view (MVC).** Controllers derive from `ControllerBase` and depend on view interfaces (`ITopPanelView`, `IRoundView`, `IPopupView`), not on MonoBehaviours. Child controllers go to `base(...)` so they are disposed with the parent; logic goes into `OnStarted` / `OnStopped`.
- **Implementation classes are `internal`**; only interfaces are public. A domain with tests has `Assembly.cs` with `InternalsVisibleTo("<Assembly>.Tests")` and `InternalsVisibleTo("DynamicProxyGenAssembly2")` (required by Moq).
- **DI with Zenject.** Each domain has its own `MonoInstaller` in `Installers/`. Controllers are bound as `IController` with an ID (`"RoundController"`, `"TopPanelController"`, `"RetryPopupController"`, `"CharacterController"`, `"CompositeSpawnerController"`) and composed by the parent via `ResolveId`. Spawners are `ISpawnerController` with IDs `"CoinController"` / `"ObstaclesController"`.
- **Async is UniTask only**, with a `CancellationToken` on every method. `OperationCanceledException` is not logged as an error.
- **UniRx subscriptions** are added to `_disposables` so they are released together with the controller.
- **Per-frame logic** goes through `ITickableContext` / `ITickable.Update(deltaTime)`, not `MonoBehaviour.Update()` (MonoBehaviours live only in views).
- Content settings live in ScriptableObjects (`CoinsSO.asset`, `ObstacleSO.asset`), not in code.
- Known debt: `TopPanelController` writes to `PlayerPrefs` directly (TODO: extract into `IStorage`).

## Adding a new domain

1. Create `Assets/App/Domains/<Group>/<Name>/` with `App.<Name>.asmdef`, `Assembly.cs` (if it has tests), `Installers/<Name>Installer.cs`, `Src/` (Controllers / Models / View) and `Prefabs/` (if it has views).
2. Create the `.asmdef` and folders through the Editor (`unity command`) so Unity generates the `.meta` files. Reference only what is needed, usually `Infra.Controllers`, `Infra.Controllers.Base`, `Infra.Disposable`, `UniTask`, `UniRx`, `Zenject`, `App.GameCore`.
3. If the domain exposes a contract, put the interfaces in a separate assembly (like `App.Character` / `App.Character.Dino`).
4. Controller: `internal class <Name>Controller : ControllerBase`; dependencies are interfaces injected via the constructor.
5. Installer: `internal class <Name>Installer : MonoInstaller`; bind the `IController` with a unique ID.
6. Wire it in the scene: a GameObject with the installer in `Game.unity`, added to the `SceneContext` `Mono Installers` list (in the live Editor, not by editing YAML).
7. Plug the controller into its parent installer/controller via `ResolveId` and pass it to `base(...)`.
8. Add a test assembly (see "Tests").

Adding a coin or a character effect: see README ("How to add new coin", "How to add new character effect behavior").

## Unity files

- **Always commit `.meta` files together with their file or folder.** Never create `.meta` files by hand or change their `guid`; a lost or regenerated `.meta` breaks references.
- **Rename or move assets in the Editor** (or together with the `.meta` in one `git mv`). A MonoBehaviour/ScriptableObject class name must match its file name.
- **`.unity`, `.prefab`, `.asset` conflicts** are not resolved by a text merge: take one side and redo the changes in the Editor.
- Never commit Android signing keys (`*.keystore`, `*.jks`) or their passwords.

## Tests

- Each test assembly lives in `Assets/Tests/<Assembly>.Tests/` (`App.Character.Dino.Tests`, `App.Spawner.Tests`) with `overrideReferences: true`, `precompiledReferences` (`nunit.framework.dll`, plus `Moq.dll`, `System.Runtime.CompilerServices.Unsafe.dll`, `System.Threading.Tasks.Extensions.dll` when it uses Moq) and `defineConstraints: ["UNITY_INCLUDE_TESTS"]`. Packages: `com.unity.test-framework` 1.8.0, `nuget.moq` 2.0.1.
- Async tests: `[UnityTest]` + `UniTask.ToCoroutine(async () => ...)`; plain synchronous logic may use `[Test]`.
- Name tests `Method_Condition_ExpectedResult`, with `// Arrange`, `// Act`, `// Assert` blocks.
- Mock view/physics/sound dependencies with Moq (`Mock<ICharacterPhysics>`, etc.). Use `TestTickableContext` with a manual `ExecuteUpdate(deltaTime)` instead of a real `ITickableContext`.
- Collect subscriptions and models in a `CompositeDisposable` and clear it in `[TearDown]`.
- **All new logic must be covered by tests.**

## Workflow

- Before changing code, present a plan and wait for the user's approval.
- Work in a separate git branch (`feature/…`, `fix/…`, `chore/…`, `docs/…`); never commit directly to `master`.
- Code, comments, commit messages, branch names, PR descriptions and docs are written in English.
- Verify SDK versions, Google Play policies, prices and ad revenue figures against current documentation and cite the source.
- The user is in the EU: anything involving ads, analytics or player data must account for GDPR (consent before collecting data).
