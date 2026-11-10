# Grand Theft Attendance

A first-person puzzle-and-combat game for the AVR course (PES University), built in **Unity 6** with the Universal Render Pipeline.
You are a student who must reach **75% attendance**. To get there you walk through portals into different "realms", each with its own flat,
outlined colour look, solve puzzles, and fight your way past two AI professors.

Made by **Kengeri Studios**. See [Credits](#credits) below, and the [technical report](docs/TECHNICAL_REPORT.md) for how the look, the music
and the models work.

**Highlights:** a GTA-poster style main menu, a flat 3D "realm" look made with a full-screen shader (inspired by SCHiM), a code-generated sound system plus a
streamed music system, portal-based levels with background preloading, a combat system with perfect blocks and dodges, a quiz and an audio-clue puzzle, two boss
fights (one with a secret phase), an opening splash sequence and a rolling credits screen.

---

## 1. Requirements

| What | Version / notes |
|---|---|
| **Unity Editor** | **6000.6.3f1** (Unity 6). Install it through **Unity Hub**. Using the exact same version avoids upgrade prompts. |
| **Render pipeline** | Universal Render Pipeline (URP) 17.6.0. Already set up in the project. |
| **Git** | Any recent version: https://git-scm.com/downloads |
| **Git LFS** | **Required.** The 3D models, images, fonts and audio are stored with Git LFS: https://git-lfs.com |
| **OS** | Windows (developed and tested there). macOS / Linux should work in the editor. |
| **Disk space** | About 1–2 GB after Unity builds its `Library/` cache |

> ⚠️ **Install Git LFS before you clone.** Without it, big files (models, music, images) download as tiny text "pointer" files and the game opens with missing models and silent music.

---

## 2. Setup

```bash
git lfs install
git clone https://github.com/TejasviKS/ARVR-Miniproject.git
cd ARVR-Miniproject
git lfs ls-files        # the big files should be listed; if they look tiny, run: git lfs pull
```

1. In **Unity Hub → Installs**, install **6000.6.3f1** (look in the *Archive* tab if it is not listed). No extra modules are needed to play inside the editor; for a Windows build the standard Windows module is enough.
2. **Projects → Add → Add project from disk**, pick the `ARVR-Miniproject` folder (the one with `Assets/`, `Packages/`, `ProjectSettings/`).
3. The first open takes several minutes while Unity imports packages and builds `Library/`. This is normal.
4. Open `Assets/Scenes/Splash.unity` and press ▶ **Play**: this is the real start of the game (opening screens, then the main menu).
   To test one level directly, open its scene (for example `Cad_lab.unity`) and press Play.

The scenes are already in the build list (**File → Build Profiles → Scene List**), in this order:

`Splash` → `MainMenu` → `Cabin_Tutorial` → `Cabin_Combat` → `Classroom` → `Cad_lab` → `Classroom_boss`

> The project uses the **new Input System only**. If you see input errors, check **Edit → Project Settings → Player → Active Input Handling**.

---

## 3. How to play

### Controls

| Key / button | Action |
|---|---|
| **W A S D** | Walk |
| **Shift** | Run |
| **Mouse** | Look around |
| **E** | Interact: every prompt reads `[E] Action` under the crosshair (sit, press, open, talk, pick up, fix) |
| **Left click** | Quick swing of your textbook (in a fight); also presses a wall switch |
| **Hold left click** | Charge a heavy swing, release to swing (slow, hard-hitting, breaks the professor's concentration) |
| **Hold right click** | Block (you take a fifth of the damage) |
| **Right click just before a hit lands** | **Perfect block**: no damage, and it stuns the professor (a thrown chalk goes back at him) |
| **Space** | Dodge (a quick sidestep you cannot be hit during). In a conversation: skip the typing / next line. |
| **1 / 2 / 3** or click | Pick an answer in the quiz |
| **R** | Emergency reset: stand up, close a conversation, and go back to this level's start. Puzzles and score are kept. |
| **Esc** | Pause menu (Resume, Options, Main Menu, Quit). In the menus and credits it goes back / skips. |

### The levels

| # | Scene | What happens |
|---|---|---|
| 1 | `Cabin_Tutorial` | A short, guided tutorial: look, walk, sprint, find the note in the cupboard, switch off the lights to open the portal. |
| 2 | `Cabin_Combat` | The combat tutorial against **Prof. GPT**: swing, heavy swing, block, perfect block, counter, dodge, then the real fight. Flat orange realm. |
| 3 | `Classroom` | A numbered wall-switch keypad opens the portal. The code is on a sticky note under a desk (sit on a bench and look down). A **Letter of Recommendation** is hidden on a windowsill: take it. Mustard/brown realm. |
| 4 | `Cad_lab` | **Lab Asst. Gemini** asks three Software Engineering questions; your score decides how many faulty computers you must fix (3 right = 3 computers, 0 right = 6). In Lab 1 one computer **beeps**: follow the 3D sound, press **E** to fix it, repeat. Purple bench room, then a teal Lab 1. |
| 5 | `Classroom_boss` | **Prof. Claude**, the final boss, in a red realm. If you picked up the Letter of Recommendation you can offer it at low health, which starts a secret extra phase. |
| – | Mission Passed | The colour spreads back across the room, the "Mission Passed" screen appears, and a **Main Menu** button returns you to the menu. |

Your **attendance** starts at 74% (the maximum is 75%): it is your health bar in the fights.

### Menus
- **Main menu:** Play, Options, Credits, Quit. W/S or the arrow keys (or the mouse) to choose, Enter to select.
- **Options** (also reachable from the pause menu): *Menu Music Volume* and *Game Music Volume*, saved between runs.
- **Credits:** the menu fades, the credits music hits with a bang, and the credits roll. Esc / Backspace / Space / Enter skips back to the menu.

---

## 4. Project structure

```
Assets/
├── Scenes/            Splash, MainMenu, Cabin_Tutorial, Cabin_Combat, Classroom, Cad_lab, Classroom_boss
├── Models/            CLASSROOM.fbx, FINAL_CAD_LAB.fbx, CABINS.fbx (Git LFS) + corrected mesh copies (*_fixed, ChairFill_*, ...)
├── Materials/         Materials taken from the models, two-sided copies, portal glow, the realm-outline material
├── Prefab/            Player.prefab (first-person controller + camera + scripts)
├── Scripts/           All gameplay and UI code (see section 5)
├── Editor/            Editor-only tools (see section 6)
├── Shaders/           RealmOutline.shader: the flat "realm" look
├── Resources/
│   ├── *.shader       Hologram, HologramHostile, ScreenGlow, WorldText, PortalSwirl, PortalWipe (unused), WastedGrey
│   ├── Fonts/         Pricedown (title font, personal use only), Comic Sans
│   ├── Audio/         mission_passed.mp3, wasted.mp3 (ripped game sounds: remove before publishing)
│   ├── Music/         calm, combat_gpt, combat_claude, fable, menu, credits (.ogg, streamed) + loudness.txt
│   ├── Sting/         The two opening-screen stings (cave, kengeri)
│   └── Logos/         cave_labs, kengeri_studios
├── Settings/          URP render pipeline settings (PC and Mobile renderers)
└── _Recovery/         Unity auto-recovery output (not tracked in git)
docs/TECHNICAL_REPORT.md   The technical report
Packages/                  Package list (Unity downloads these when the project opens)
ProjectSettings/           Input, physics, graphics, build scene list, ...
seventhFloor/              Local Blender source files (not in git)
```

All other game sounds are generated in code (`CombatSfx`, the portal whoosh and hum, the NPC voice blips).

---

## 5. Scripts (`Assets/Scripts/`), grouped

| Area | Scripts |
|---|---|
| **Player** | `PlayerController` (walk, run, look, gravity), `PlayerInteractor` (the `[E]` prompts, crosshair, **R** reset), `PlayerSpawn`, `PlayerCombat` (swings, block, perfect block, dodge, the textbook in hand) |
| **Interaction & puzzles** | `Interactable`, `SitSpot`, `CodePanel` / `PanelSwitch` / `CodeNote` (classroom keypad), `PickupItem` (Letter of Recommendation), `OpenableCupboard`, `LightSwitch` / `RoomLights`, `BeepingComputerPuzzle` / `ComputerSwitch` (Lab 1), `RiddleNPC` (Lab Asst. Gemini's dialogue and quiz) |
| **Combat** | `ProfessorAI` (the hologram professors), `BossFight` / `BossDifficulty` (Prof. Claude's phases and the Fable secret phase), `Chalk`, `Health`, `CombatSfx`, `CombatHUD`, `CombatTutorial` |
| **Tutorials & story** | `TutorialDirector`, `TutorialUI`, `CabinIntroDialogue`, `ObjectiveHUD`, `GameProgress` (score, items and timer between scenes) |
| **The look** | `RealmStyle` (the flat realm look; see the technical report), `WorldTextUtil` |
| **Portals** | `Portal` (a vortex that loads a scene or teleports; preloads its scene in the background), `PortalTransition` (the walk-through) |
| **Menus & screens** | `SplashScreen`, `MainMenu`, `OptionsPanel`, `PauseMenu`, `CreditsScreen`, `MissionPassedScreen`, `WastedScreen` (defeat), `EndScreen` (old, unused), `MenuExit` (fade to the menu), `GtaMenuUI` (the shared drawing helpers), `GameCursor` (the one place that decides whether the mouse is captured) |
| **Audio** | `MusicDirector` (all music: tracks per level, fades, ducking, menu and credits music) |
| **Misc** | `FrameRateCap` (60 FPS) |

---

## 6. Editor tools (`Assets/Editor/`)

| Menu item | What it does |
|---|---|
| **Tools → Realm Outline → Install** | Adds the realm-look pass to the URP renderers (switched off). Run once per project. |
| **Tools → Realm Outline → Add To Open Scene** | Adds a `RealmStyle` object to the open scene, wired to the renderers and a portal named `LabPortal`. |
| **Tools → Music → Measure Loudness** | Measures every file in `Assets/Resources/Music` and writes `loudness.txt`. **Run it whenever you add or replace a music file.** |
| **Tools → Rebuild CAD Lab Colliders** | Replaces the lab's messy colliders with clean wall / floor / furniture boxes. |
| **Tools → Make Box Colliders For Selected** | Replaces a bench's mesh collider with boxes. |
| **Tools → Cabins → Build Both And Update Build List** | Rebuilds `Cabin_Tutorial` and `Cabin_Combat` from `CABINS.fbx`. |

Also automatic: `MusicImportSettings` imports every music file as *Streaming*, and `LogoImportSettings` imports the logos readable and uncompressed.
The collider tools can be undone with **Ctrl+Z**.

---

## 7. Replacing the music

Put your file in `Assets/Resources/Music/` using one of these names (`.ogg` works best), then run **Tools → Music → Measure Loudness**:

| File | Used for |
|---|---|
| `calm.ogg` | Cabin tutorial, Classroom, Cad lab |
| `combat_gpt.ogg` | The GPT fight (`Cabin_Combat`) |
| `combat_claude.ogg` | The Claude fight (`Classroom_boss`) |
| `fable.ogg` | The secret Fable phase of the Claude fight |
| `menu.ogg` | Main menu (loops with a 3 s pause) |
| `credits.ogg` | Credits (its opening snare is the bang; it plays to its end) |

Per-track loudness is set by a *weight* at the top of `MusicDirector.cs`. A track cannot be made louder than its file allows (volume is capped at 1), so raise the file itself if it is too quiet.

---

## 8. Building a Windows version

1. **File → Build Profiles**, select **Windows**; check that `Splash` is the first scene.
2. **Player Settings:** set Company Name, Product Name and Fullscreen Mode (*Fullscreen Window* is a good choice).
3. **Build** into an empty folder, then zip the **whole** folder to share it (the `_Data` folder and DLLs are needed next to the `.exe`).
4. Play the built game once from start to finish before sharing it.

---

## 9. Troubleshooting

| Problem | Fix |
|---|---|
| Models are missing / pink, or `.fbx` / `.ogg` files are tiny | Git LFS was not installed when you cloned. Run `git lfs install`, then `git lfs pull`. |
| Everything is **pink/magenta** | The URP asset is not assigned: **Edit → Project Settings → Graphics**, set it to `Assets/Settings/PC_RPAsset.asset`. |
| The scene is empty when the project opens | Open `Assets/Scenes/Splash.unity`. |
| "Portal can't find scene …" | Add the scene to **File → Build Profiles → Scene List**. |
| The flat realm look does not show | Run **Tools → Realm Outline → Install** and **Add To Open Scene**; check the `RealmStyle` object is enabled and has *Outline Features* filled in. |
| A model is invisible from one side / has holes | Flipped normals: see section 2 of the [technical report](docs/TECHNICAL_REPORT.md). |
| No music, or a "No loudness number" warning in the console | Check the music files are real (LFS) and run **Tools → Music → Measure Loudness**. |
| Mouse does not move the camera | Click inside the Game view. **Esc** releases the cursor / opens the pause menu. |
| Stuck in a seat or the camera is tilted | Press **R**. |
| Lots of compile errors on first open | Wait for the package import to finish, then **Assets → Reimport All**; make sure you use Unity 6000.6.3f1. |

---

## 10. Notes for contributors
- `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `seventhFloor/` and the IDE files (`*.csproj`, `*.sln`) are **git-ignored**. Unity regenerates most of them.
- **Always commit `.meta` files** together with their assets.
- Large binaries (`.fbx`, `.blend`, `.png`, `.jpg`, `.ogg`, `.wav`, `.ttf`, ...) go through Git LFS via `.gitattributes`. GitHub rejects any file over 100 MB.
- The project has *Enter Play Mode Options* on (no domain reload): any script with static state must reset it in a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` method.
- Use the new Input System (`Keyboard.current`, `Mouse.current`), not `UnityEngine.Input`.

---

## Credits

| Role | Person |
|---|---|
| Director | Tanmay T A |
| Game Concept | Shashank Arya |
| Game Design | Tanmay T A |
| Puzzle Design | Pramiti Udupa |
| Combat Design | Shashank Arya |
| Audio Puzzle Design | Tejasvi K S |
| Level Design | Tejasvi K S |
| Quiz Writing | Pramiti Udupa |
| World Design | Shashank Arya |
| Lead Programming | Tanmay T A |
| Combat Playtesting | Shashank Arya |
| Art Direction | Tanmay T A |
| UI Design | Tanmay T A |
| Creative Consultant | Pramiti Udupa |
| Audio Director | Tanmay T A |
| Puzzle Playtesting | Pramiti Udupa |
| Music Composed by | Tanmay T A |
| Sound Design | Tanmay T A |

**Special thanks:** Cave Labs @ PES University (3D building models) and AVR Course Faculty Dr. Adithya Balasubramanyam.

**Music:** "Aria Math (Drum Cover)" by Drummy Walnut; "Halo 3 Warthog Run (Complete Version)" by Dirghadarzana; "Hardpoint Shipment" and the main menu theme by Speedbird-One.
The third-party tracks are not licensed for redistribution: replace them before any public release (see the technical report, section 6).
