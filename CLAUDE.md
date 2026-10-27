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
    Combat/       Health, Weapon, WeaponPickup, AlienEnemy, AlienAnimator, WaveSpawner, PlayerDeath, ProceduralSfx
    Level/        LevelManager, LevelExitTrigger
    Settings/     GameSettings, MusicTrack, SfxVolumeApplier, MouseSensitivityApplier
    UI/           QuestionPanel, InteractionPromptUI, ScreenFader, MainMenu, SettingsPanel,
                  IntroSlideshow, KenBurns, BlinkingGraphic, CombatHud
    Editor/       Scene/prefab builders + UiBuildUtils, SceneBuildUtils, StationBuildUtils
  Data/Questions/ L1_D1_Q1_IfElse, L1_D1_Q2_And, L1_D2_Q3_Assignment, L1_D2_Q4_Float,
                  TR_D1_Q1_Equality, TR_D1_Q2_WhileLoop
  Scenes/         MainMenu, Intro, Level1, Transit (+ Transit_NavMesh.asset), Level2 (placeholder)
  Prefabs/UI/     StarfallHUD.prefab
  Prefabs/Combat/ Alien.prefab
  Art/Space/      Blender-made planet, moon, station, asteroids (SM_*.fbx, T_*.png, materials)
  Art/Combat/     Blender-made SM_Alien.fbx (flat jointed parts) and SM_Blaster.fbx, materials
  Art/Intro/      Intro_01..06.png (Higgsfield, 16:9)
  Art/UI/         T_ShadeGradient.png
  Art/FX/         M_Sparks, M_Smoke
  Settings/       Level1_PostFX.asset (bloom, ACES, vignette)
ArtSource/StarfallSpace.blend   source for the space models
ArtSource/StarfallCombat.blend  source for the alien and the blaster
```

Build order (`SceneBuildUtils.UpdateBuildSettings`): MainMenu → Intro → Level1 → Transit → Level2.

## Game flow

1. **MainMenu**: Ken Burns background, START / SETTINGS / QUIT. The settings panel has master/music/SFX
   volume, mouse sensitivity and fullscreen.
2. **Intro**: 6 illustrated slides with a crossfade (1.6 s) and a typewriter caption. Space/Enter/click
   continues, Esc skips, then it loads Level1.
3. **Level1**: repair 2 devices, then go through the exit door.
4. **Transit**: unlock the weapon locker (2 questions), pick up the dropped blaster (E), fight 3 alien waves,
   then go through the exit door.
5. **Level2**: placeholder ("Coming soon").

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

## Transit (alien attack between Level 1 and Level 2)

- Layout: armory x 0..18 (weapon locker on the west wall) → armory door (x=18) → corridor x 20..62
  with cover crates and red alarm lights → exit door (x=62) → vestibule with the Level2 trigger.
  Aliens spawn at the far end of the corridor.
- **Weapon locker**: a `RepairableDevice` with `actionVerb = "Unlock"` (questions TR_D1_Q1 equality `==`,
  TR_D1_Q2 `while` loop). Repairing it opens the armory door, and `OnRepaired` calls `WeaponPickup.Drop`. The blaster
  then falls out of the locker with physics (Rigidbody, clank sound, cyan glow) and lands about 1.5 m in front of it.
  Picking it up with E (`WeaponPickup` is an `IInteractable`) calls `Weapon.Equip`, and its `OnPickedUp` starts the waves,
  so the player is never attacked unarmed.
- **Waves**: `WaveSpawner` spawns 3 / 5 / 7 aliens. The next wave starts when the current one is dead, and
  each cleared wave heals the player by 30. `LevelManager.waveSpawners` keeps the exit closed until the waves
  are cleared; `activateOnComplete` turns on the exit lights.
- **Player**: `Health` (100) + `PlayerDeath` on the PlayerCapsule. At 0 HP the death screen shows and the
  scene restarts. `PlayerControlLock.alsoDisable` disables the weapon while a panel is open.
- **Weapon**: hitscan blaster view model under the camera (34 damage, 16 rounds, R to reload, hold left
  click to fire). It has a beam, muzzle flash, impact sparks and a recoil kick. Transit sets the Cinemachine lens near clip to
  0.05 so the view model is not clipped.
- **Aliens**: `AlienEnemy` handles behaviour. It screeches on spawn, chases the player on the NavMesh, weaves
  from side to side while far away, dashes now and then, and claws with a wind-up. Each alien has its own speed.
  `AlienAnimator` animates the jointed model in code (no rig): the walk cycle follows the distance
  travelled, the body bobs, leans and rolls into turns, the head tracks the player and twitches, plus
  attack, hit and death-collapse poses. The FBX holds the body parts flat (each part's origin is its joint),
  and `CombatAssetBuilder` parents them into the skeleton, because the FBX axis baking breaks nested hierarchies.
- **NavMesh**: baked in the builder (`NavMeshSurface`, physics colliders, door leaves ignored) and saved
  to `Transit_NavMesh.asset`. It is baked before the player is created.
- **HUD**: `CombatHud` lives in StarfallHUD (health bar, ammo, wave banners, red damage flash, death
  screen). It hides itself where the player has no `Health` (Level 1). `StarfallHudBuilder.UpdateCombatHud`
  adds it to the existing prefab without rebuilding it, so references from other scenes keep working.
- Combat sounds are generated in code (`ProceduralSfx`).

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
| Build Transit Scene | `TransitSceneBuilder` (also runs the two below) |
| Build Combat Assets | `CombatAssetBuilder` (materials, Alien prefab) |
| Update Combat HUD In Prefab | `StarfallHudBuilder.UpdateCombatHud` |

The Level2 placeholder is built by `PlaceholderSceneBuilder`. The shared station pieces (doors, player rig,
post-FX) live in `StationBuildUtils`.

⚠️ **Builders that remap model materials must call `AssetDatabase.SaveAssets()` before `ModelImporter.SaveAndReimport()`.**
Reimporting reloads the remapped materials from disk, which silently drops any unsaved edits, such as the `_EMISSION` keyword
(see `CombatAssetBuilder.SetupMaterials`, `SpaceBackdropBuilder.SetupMaterials`).

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

- Test in real Play Mode (`EditorSceneManager.playModeStartScene`). The team expects Play to start from
  **MainMenu**, so after a test restore `playModeStartScene` to `MainMenu.unity` (never `null`, which would play
  whichever scene is open).
- If frames stop advancing in Play Mode (`Time.frameCount` stays the same), the Unity window is probably in the
  background. Bring the Game view to the front, or step physics manually with `Physics.Simulate`.
- Videos: run the game with `Time.captureFramerate = 30`, capture each frame with `ScreenCapture.CaptureScreenshot`
  (with the Game view set to a fixed 1280x720) and encode the frames with ffmpeg (it is installed through winget).
  Unity Recorder is not installed.
- Automated input uses a virtual keyboard (`InputSystem.QueueStateEvent`) with `cursorInputForLook = false`,
  so that real mouse/keyboard input doesn't interfere.
- Restore `runInBackground` and `editorInputBehaviorInPlayMode` after tests.
- Combat tests: add a virtual `Mouse` (`InputSystem.AddDevice<Mouse>`), aim the PlayerCapsule from an
  `EditorApplication.update` callback and queue `MouseState` button presses. Remove the device afterwards.

## Known gaps

- The procedural spark crackle is basic. A real SFX would be better.
- The kit's console screens render white.
- There is no pause menu yet.
- The QuestionPanel's last button says "Finish repair" even on the weapon locker ("Unlock").
- Combat difficulty (waves, damage, speeds) has only been tested with an automated aimer, not a person.
- Picking up the blaster with E was checked in code (pickup ray distance, `Interact()`), not by pressing the key by hand.
- The blaster view model was tuned in a small Game view; check it at 1080p.
- Level 2 is a placeholder.
