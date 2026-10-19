# Starfall

First-person educational sci-fi game: the player repairs a damaged space station by answering
programming questions at broken devices. Each level teaches a few basic coding concepts.

## Tech stack

- Unity 6000.6.5f1, URP 17.6 (`PC_RPAsset`), new Input System only (`activeInputHandler = 1`)
- TextMeshPro (built into uGUI 2.x), Cinemachine 3 (`com.unity.cinemachine` 6.6.0)
- Starter Assets – FirstPerson (`PlayerCapsule`, `FirstPersonController`, `StarterAssetsInputs`)
- Environment kit: `Assets/Creepy_Cat/3D Scifi Kit Starter Kit_HD`
- Git LFS for `.asset`, `.png`, `.fbx`, `.blend`. Remote: `https://github.com/EmreQraz/Starfall.git`, branch `main`

## Project rules

- **Everything in-game is English**: code, comments, UI text, question data.
- Never guess prefab/material paths — look them up (the builders load kit prefabs by name and fail loudly).
- Do not add, remove or update packages without asking.
- Never commit without the user's explicit approval. Commit messages: `<type>: <description>`
  (feat, fix, refactor, docs, test, chore, perf, ci). No AI co-author / attribution lines.
- Before starting a phase or larger change, explain the plan (in Turkish) and then start.
- Scene work that Unity MCP can't do is done with Editor scripts (see "Scene builders").

## Folder layout

```
Assets/Starfall/
  Scripts/
    Interaction/  IInteractable, PlayerInteractor, PlayerControlLock
    Questions/    QuestionData (ScriptableObject)
    Devices/      RepairableDevice, Door, FlickeringLight, SparkEmitter
    Level/        LevelManager, LevelExitTrigger
    Settings/     GameSettings, MusicTrack, SfxVolumeApplier, MouseSensitivityApplier
    UI/           QuestionPanel, InteractionPromptUI, ScreenFader, MainMenu, SettingsPanel,
                  IntroSlideshow, KenBurns, BlinkingGraphic
    Editor/       Scene/prefab builders + UiBuildUtils, SceneBuildUtils
  Data/Questions/ L1_D1_Q1_IfElse, L1_D1_Q2_And, L1_D2_Q3_Assignment, L1_D2_Q4_Float
  Scenes/         MainMenu, Intro, Level1, Level2 (placeholder)
  Prefabs/UI/     StarfallHUD.prefab
  Art/Space/      Blender-made planet, moon, station, asteroids (SM_*.fbx, T_*.png, materials)
  Art/Intro/      Intro_01..06.png (Higgsfield, 16:9)
  Art/UI/         T_ShadeGradient.png
  Art/FX/         M_Sparks, M_Smoke
  Settings/       Level1_PostFX.asset (bloom, ACES, vignette)
ArtSource/StarfallSpace.blend   source for the space models
```

Build order (`SceneBuildUtils.UpdateBuildSettings`): MainMenu → Intro → Level1 → Level2.

## Game flow

1. **MainMenu**: Ken Burns background, START / SETTINGS / QUIT. The settings panel has master/music/SFX
   volume, mouse sensitivity and fullscreen.
2. **Intro**: 6 illustrated slides with a crossfade (1.6 s) and a typewriter caption. Space/Enter/click
   continues, Esc skips, then it loads Level1.
3. **Level1**: repair 2 devices, then go through the exit door.
4. **Level2**: placeholder ("Coming soon").

Scene changes always go through `ScreenFader.LoadScene(name)`, which fades both picture and audio.

## Level 1 systems

### Interaction
- `IInteractable`: `Prompt`, `CanInteract`, `Interact()`.
- `PlayerInteractor`: raycast (2.5 m) from the camera. Shows `[E] {Prompt}` via `InteractionPromptUI`
  and calls `Interact()` on E (an `InputAction`, default `<Keyboard>/e`).
- `PlayerControlLock`: `Lock()` disables movement/look and the interactor and frees the cursor.
  `Unlock()` restores them. It is used while a UI panel is open.

### Questions
- `QuestionData` (menu *Create → Starfall/Question*): question text, 4 options, correct index and
  explanation. Rich text is allowed (code is colored with `<color=#7FDBFF>`).
- `QuestionPanel.Open(title, questions, completed, cancelled)`: asks the questions in order.
  - Wrong answer: red header, the explanation and "Try again".
  - Correct answer: "Continue" ("Finish repair" on the last question).
  - Esc cancels and the device stays broken.

### Devices
- `RepairableDevice` (IInteractable): `deviceName` plus a list of `QuestionData`. When all of them are
  answered correctly it switches to Repaired and then:
  - opens `doorToOpen`
  - toggles `activateOnRepair[]` / `deactivateOnRepair[]`
  - fires `OnRepaired` (UnityEvent) and `Repaired` (C# event)
  A repaired device shows no prompt.
- `Door`: slides its leaves by an offset in a coroutine, plays a sound and fires `OnOpened`.
  The kit doors have no animator, so the builder assembles them from `DoorWay_01_Large` + `Door_Left_01`/`Door_Right_01`.
- **Malfunction visuals** (added because players couldn't tell what was broken). `MalfunctionBuilder.Add` creates:
  - sparks (`SparkEmitter`: bursts with a light flash and a procedurally generated crackle)
  - smoke
  - an optional red `FlickeringLight`
  - a blinking world-space "! SYSTEM FAILURE !" label
  The whole root goes in `deactivateOnRepair`, so everything stops when the device is repaired.

### Level
- `LevelManager`: tracks all devices. When every device is repaired it opens `exitDoor`.
  `LoadNextLevel()` checks that the next scene is in Build Settings.
- `LevelExitTrigger`: a trigger that calls `LoadNextLevel()` when the "Player" enters it.

### Level 1 layout and content
- Cabin x 0..18 → door (x=18) → corridor x 20..44 with portholes → exit door (x=44) → vestibule with a Level2 trigger.
- **Device 1 – Intercom** (cabin wall): questions Q1 if/else and Q2 AND. Repairing it opens the cabin door
  and turns on the corridor lights. Until then the corridor has only red emergency lights.
- **Device 2 – Terminal console** (end of the corridor): questions Q3 assignment and Q4 float. Repairing it
  turns on a green glow. When both devices are done, LevelManager opens the exit.
- **Space backdrop**: panoramic starfield skybox, plus a planet, moon, station and spinning asteroids
  visible through the portholes (layer `SpaceBackdrop`).

### Settings and audio
- `GameSettings`: static wrapper around PlayerPrefs (volumes, sensitivity 0.2–3, fullscreen) with a
  `Changed` event. It sets `AudioListener.volume`.
- `MusicTrack` (music volume and fade), `SfxVolumeApplier` (scales non-music sources),
  `MouseSensitivityApplier` (scales `FirstPersonController.RotationSpeed`).

## Scene builders (Editor, menu "Starfall/…")

The scenes are **generated by code**:

| Menu | Builds |
|---|---|
| Build Level 1 Scene | `Level1SceneBuilder` |
| Build Menu And Intro Scenes | `MenuSceneBuilder` |
| Build HUD Prefab | `StarfallHudBuilder` |
| Setup Space Materials | `SpaceBackdropBuilder` |

The Level2 placeholder is built by `PlaceholderSceneBuilder`.

⚠️ **Re-running a builder overwrites the scene and discards manual edits made in the Unity editor.**
Change the builder code instead, or stop using the builder for that scene. `BuildSceneAt` refuses to
run if the open scene has unsaved changes.

## Making a new level

1. Write the `QuestionData` assets in `Data/Questions/` (naming `L{level}_D{device}_Q{n}_{Topic}`).
2. Create the scene: copy `Level1SceneBuilder` as a starting point, or build it by hand. It needs:
   - `PlayerCapsule` with `PlayerInteractor`, `PlayerControlLock` and `MouseSensitivityApplier`
   - the `StarfallHUD` prefab (QuestionPanel + prompt)
   - a `RepairableDevice` on each device, with its questions, door and malfunction effect
   - a `LevelManager` with the device list, exit door and `nextSceneName`
   - a `LevelExitTrigger`, plus `ScreenFader`, music and `SfxVolumeApplier`
3. Add the scene to Build Settings (`UpdateBuildSettings`) and set the previous level's `nextSceneName`.

## Decisions taken

- Everything is in English, including question texts. The UI tone is "station terminal".
- Missing art is generated in Blender (space objects) and Higgsfield (6 intro slides, `gpt_image_2_5`,
  dark cinematic sci-fi, 16:9). If one image is disliked, only that image is regenerated.
- The story intro is illustrated slides (at most 6 sentences) rather than a cutscene.
- A start screen with sound settings comes before the game.
- The flow is linear: device → questions → door. Players can't add questions; questions are designer-authored assets.
- Broken devices must be visibly broken (sparks, smoke, flicker, label).
- Interior lighting is intentionally dark. Judge visuals in Play Mode, not in the edit-mode preview
  (the preview showed a misleadingly bright floor).

## Testing notes

- Test in real Play Mode (`EditorSceneManager.playModeStartScene`).
- Automated input uses a virtual keyboard (`InputSystem.QueueStateEvent`) with `cursorInputForLook = false`,
  so that real mouse/keyboard input doesn't interfere.
- Restore `runInBackground` and `editorInputBehaviorInPlayMode` after tests.

## Known gaps

- The procedural spark crackle is basic. A real SFX would be better.
- The kit's console screens render white.
- There is no pause menu yet.
- Level 2 is a placeholder.
