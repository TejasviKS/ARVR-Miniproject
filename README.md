# ARVR Miniproject: Escape-Room Puzzle Game (Unity)

A first-person puzzle game built in Unity. You start in a **Classroom** and solve a light-switch code puzzle to open a portal to the **CAD Lab**. There, Lab Asst. Gemini quizzes you with three Software Engineering questions, and your score decides how many faulty computers you must find and fix before you escape.

Built to show: interaction, spatial (3D) audio, UI, lighting effects, and running on low-end laptops. All sounds are generated in code, so the project has no audio files.

---

## 1. Requirements

| What | Version / notes |
|---|---|
| **Unity Editor** | **6000.6.3f1** (Unity 6). Install it through **Unity Hub**. Using the exact same version avoids upgrade prompts. |
| **Render pipeline** | Universal Render Pipeline (URP) 17.6.0. Already set up in the project, nothing to install. |
| **Git** | Any recent version: https://git-scm.com/downloads |
| **Git LFS** | **Required.** The 3D models, font and textures are stored with Git LFS: https://git-lfs.com |
| **OS** | Windows, macOS or Linux (it was developed on Windows) |
| **Disk space** | About 1–2 GB after Unity builds its `Library/` cache |

> ⚠️ **Install Git LFS before you clone.** Without it, files like `FINAL_CAD_LAB.fbx` download as tiny text "pointer" files and the scenes open with missing models.

---

## 2. Setup steps

### Step 1: Install Git LFS (once per computer)
```bash
git lfs install
```

### Step 2: Clone the repository
```bash
git clone https://github.com/TejasviKS/ARVR-Miniproject.git
cd ARVR-Miniproject
```

Check that the big models came down properly (they should be MBs, not ~130 bytes):
```bash
git lfs ls-files
```
If the models look like small text files, run:
```bash
git lfs pull
```

**Alternative (no Git):** on GitHub click **Code → Download ZIP**.
GitHub ZIPs *may* contain LFS pointer files instead of the real models. If the models don't show up in Unity, use the `git clone` method above.

### Step 3: Install the right Unity version
1. Open **Unity Hub → Installs → Install Editor**.
2. Pick **6000.6.3f1** (if it's not listed, look in the **Archive** tab or at https://unity.com/releases/editor/archive).
3. No extra platform modules are needed to play inside the editor.

### Step 4: Open the project
1. In Unity Hub go to **Projects → Add → Add project from disk**.
2. Select the cloned `ARVR-Miniproject` folder (the one that contains `Assets/`, `Packages/` and `ProjectSettings/`).
3. Open it. **The first open takes several minutes**: Unity downloads the packages listed in `Packages/manifest.json` and rebuilds the `Library/` folder. This is normal.
4. If Unity asks to upgrade the project to a different version, choose the matching 6000.6.3f1 editor instead if you can.

### Step 5: Play
1. In the **Project** window open `Assets/Scenes/Classroom.unity` (double-click it).
2. Press ▶ **Play**.
3. Click inside the Game view to lock the mouse.

The scenes are already in the build list (**File → Build Profiles → Scene List**) in this order:
`Classroom` → `Cad_lab`. The Classroom portal loads `Cad_lab`, so both scenes must stay in that list.

---

## 3. Controls

| Key | Action |
|---|---|
| **W A S D** | Walk |
| **Shift** | Run |
| **Mouse** | Look around |
| **E** | Interact (sit, press switch, talk, switch off computer). A prompt appears under the crosshair. |
| **Left click** | Press a wall switch (same as **E**, only for the switches) |
| **Space** (or E / Left click) | In a conversation: skip the typing, then go to the next line (the box only says "Press Space to continue") |
| **1 / 2 / 3** or click | Pick an answer to a question |
| **R** | Emergency reset in every level: stand up, close a conversation, and put you back at that level's start (facing the same way). Nothing else changes: puzzles, notes, lights and score are kept. In Cad_lab, after the Lab portal, it returns you to Lab 1's arrival point. |
| **Esc** | Release the mouse cursor (click the Game view to lock it again) |

---

## 4. How the game works

### Level 1: Classroom (`Classroom.unity`)
- The wall switches are numbered like a keypad: **1 2 3 4 5 6** on the top row and **7 8 9 0** on the second row.
- A sticky note with the code is hidden **under one of the desks**. Sit on a bench (press **E**) and look down to find it.
- Press the switches in the right order. A right code flashes green and a **portal** appears; a wrong code flashes red and resets.
- Walk into the portal to go to the CAD Lab. The portal is an orange vortex (the colour of the next level): you rush in, a round wipe closes over the screen, and it opens again on the other side.

<details>
<summary>Spoiler: the code</summary>

The code is set on the `CodePanel` component in the Classroom scene (currently `3-7-1-9`). The sticky note reads it from there automatically, so if you change it the note updates too.
</details>

### Level 4: Lab Asst. Gemini's quiz and the faulty computers (`Cad_lab.unity`)
- You arrive in an empty bench room with a glowing, see-through **lab assistant**, "Lab Asst. Gemini". You can hear his low hum from about 10 m away, and he slowly turns to face you.
- Walk up to him and press **E** to talk. The text types out with little "voice" blips. He says hi, explains that some computers are faulty, and asks you to **answer three questions to proceed**.
- The questions come from the Software Engineering course (testing types, development models ...). Each has 3 answers in a different order every time, and you get **one try** per question. A right answer flashes green with a chime; a wrong answer flashes red with a buzz, and he tells you the right answer.
- Your score decides how many computers you have to fix in the next room:

  | Questions right | Computers to fix |
  |---|---|
  | 3 | 3 |
  | 2 | 4 |
  | 1 | 5 |
  | 0 | 6 |

- After the last question, a **portal** appears by the door. It's one-way: walk in and you appear in Lab 1.
- In Lab 1 the objective reads "Fix N computers in the lab (done/N)". One computer is **beeping** (3D sound): follow the sound to get close. Its screen only glitches now and then and shows a small error message, so the audio is the main clue. Look at it and press **E** to fix it; then the next one starts beeping somewhere else. The computers are picked at random (from the 40 in Lab 1) each time you play.
- If you play `Cad_lab` directly without the quiz (for testing), it uses 3 computers.
- The questions and every line he says are plain fields on the Keeper object in `Cad_lab.unity` (`RiddleNPC`), so they are easy to change.

### The end (for now)
- When the last computer is fixed, a **Mission Passed!** screen appears with the mission-passed music, and a **Main Menu** button fades in. It quits the game (or stops Play mode in the editor) until the real main menu, the boss fight and the credits are made.
- The older "You escaped the multiverse!" end screen (`EndScreen.cs`) is still in the project but the puzzle no longer shows it (see the puzzle's `End With Mission Passed` setting).

---

## 5. Project structure

```
Assets/
├── Scenes/
│   ├── Classroom.unity        Level 1: switch-code puzzle
│   ├── Cad_lab.unity          Level 4: Lab Asst. Gemini's quiz + fix-the-computers puzzle
│   └── SampleScene.unity      Unity's default scene (not used, disabled in the build list)
├── Models/
│   ├── CLASSROOM.fbx          Classroom 3D model (~15 MB, Git LFS)
│   └── FINAL_CAD_LAB.fbx      CAD lab 3D model (~90 MB, Git LFS)
├── Materials/                 Materials taken from the models + PortalGlow
├── Prefab/
│   └── Player.prefab          First-person player (CharacterController + camera + scripts)
├── Scripts/                   Gameplay code (see below)
├── Editor/                    Editor-only tools for building colliders
├── Resources/
│   ├── Hologram.shader        See-through glowing hologram (Lab Asst. Gemini)
│   ├── HologramHostile.shader Hostile hologram (combat AIs): dark core, magenta rim, glitch
│   ├── ScreenGlow.shader      Glowing computer screens
│   ├── WorldText.shader       In-world text that gets hidden behind objects
│   ├── PortalSwirl.shader     The vortex drawn on portals
│   └── PortalWipe.shader      The round screen wipe used when going through a portal
├── Shaders/
│   └── RealmOutline.shader    Flat colour + outlines full-screen pass (the realm look)
├── Settings/                  URP render pipeline settings
├── TextMesh Pro/              TextMesh Pro resources (Unity package content)
└── TutorialInfo/              Unity template readme (can be ignored)
Packages/                      Package list. Unity downloads these when the project opens.
ProjectSettings/               Input, physics, graphics, build scene list, etc.
```

### Scripts (`Assets/Scripts/`)

| Script | What it does |
|---|---|
| `PlayerController.cs` | First-person movement: walk, run, mouse look, gravity, cursor lock. |
| `PlayerInteractor.cs` | Raycasts from the camera, shows "Press E to …" prompts, the crosshair and messages, and handles the **R** reset. |
| `Interactable.cs` | Base class for everything you can press **E** on. |
| `SitSpot.cs` | Lets you sit on a bench: the camera glides into a seated view facing the chalkboard. |
| `CodePanel.cs` | Classroom switchboard puzzle: finds the wall switches, numbers them, and checks the code you enter. |
| `PanelSwitch.cs` | A single wall switch (added automatically by `CodePanel` when the game starts). |
| `CodeNote.cs` | The sticky note under the desk that shows the code. |
| `RiddleNPC.cs` | Lab Asst. Gemini: builds his hologram body from simple shapes, adds his light and 3D sounds, runs the dialogue and the quiz, and saves the score. |
| `GameProgress.cs` | Remembers the quiz score, how many computers to fix and the start time while you move between scenes. |
| `Portal.cs` / `PortalTransition.cs` | A portal's vortex fades in over 50 ms when it opens, and stepping into it just teleports you (no animation). A portal to another scene starts loading that scene in the background about a second after it opens, so stepping in only has to switch to it. A portal that opens on top of you waits until you step out and back in; there is no arm delay. (`Resources/PortalWipe.shader` is no longer used.) |
| `MissionPassedScreen.cs` | The ending screen (for now): when the last computer in Lab 1 is fixed, the "mission passed" music plays (`Assets/Resources/Audio/mission_passed.mp3`, a ripped game sound: remove before publishing), the picture dims and a gold "Mission Passed!" in the Pricedown font fades in with "Attendance 75%" under it. After a moment a rounded **Main Menu** button fades in (click or Enter); for now it quits the game. The old end screen (`EndScreen.cs`) is bypassed by the puzzle's `End With Mission Passed` setting. |
| `WastedScreen.cs` | The defeat screen: slow motion, the picture turns black and white with dark corners, a big red "Attendance 0%" in the classic Pricedown font (`Assets/Resources/Fonts/PricedownBl.otf`, free for personal use only), the "wasted" sound (`Assets/Resources/Audio/wasted.mp3`, a ripped game sound) and a small rounded Try Again button (click or Enter). Fine for this course project; remove the sound before ever publishing. The combat tutorial restarts the fight only after you press it; later fights can use `WastedScreen.Show(...)` too. |
| `ObjectiveHUD.cs` | The level objective in the top left strip ("▶ Find a way out of this classroom"), the same strip the cabins use for "Find the light switch". The cabins give step-by-step instructions; later levels get abstract objectives, listed at the top of the script (Cad_lab has two stages and moves on when you go through the Lab portal). It creates itself when a scene loads. |
| `PickupItem.cs` | Something you can pick up with E (used for the **Letter of Recommendation** hidden on a windowsill in the Classroom). Saves the item in `GameProgress` (`HasItem("letter_of_recommendation")`) so later levels can use it. |
| `BeepingComputerPuzzle.cs` | CAD lab puzzle: makes screens glow, makes ONE computer beep (3D sound, subtle glitching screen) and moves on to the next one when you fix it with E. The computers are chosen at random from the list of Lab 1 monitors. It waits until the portal starts it. |
| `ComputerSwitch.cs` | The invisible "press E to fix" box on a faulty computer (created automatically). |
| `Portal.cs` | Portal you walk into. It either loads the next scene (Classroom → Cad_lab) or teleports you to a point in the same scene and then disappears (one-way, bench room → Lab 1). Draws itself as a glowing vortex in its **Portal Color** (set it to the colour of the place it leads to), with a light and a 3D hum. |
| `TutorialDirector.cs` | Runs the first level (`Cabin_Tutorial`) with a teaching box that shows one thing at a time: look (mouse icon + 4 arrows), walk (W A S D keys), sprint (SHIFT), R to reset, then a floor ring and on-screen marker to the cupboard, press E, the note, "find the light switch", "step through the portal". The note can be read again with E on the open cupboard. |
| `PlayerSpawn.cs` | Remembers where the player started. In a scene that has one (the cabins), R puts the player back there; nothing else is reset. |
| `PlayerCombat.cs` | The player's fighting (fight rooms only): left click = quick swing of the textbook, hold left click = charged heavy swing, hold right click = block, right click just before a hit lands = perfect block (stuns him), Space = dodge. Also draws the textbook in your hand. |
| `ProfessorAI.cs` | An AI professor you can fight: a hostile hologram (angular, magenta, glitchy: the combat cousin of the calm cyan Keeper) built from shapes, telegraphs every attack, can be stunned, and steps back to make room for a chalk throw if you crowd him. Health bar width and colour are per-opponent (purple, 300 px for the tutorial professor; the boss gets a much wider one). Your attendance starts at 74 with 75 as the maximum. The tutorial professor has only a pointer swing and one chalk throw; the boss will be a separate script with more abilities. |
| `Chalk.cs` / `Health.cs` / `CombatSfx.cs` | The thrown chalk (dodge it, or perfect-block to send it back), hit points (with a floor so the tutorial professor can't die early), and all fight sounds made in code. |
| `CombatHUD.cs` / `CombatTutorial.cs` / `TutorialUI.cs` | The fight screen (attendance bar, his health bar, subtitles, popups) and the combat tutorial's lessons (swing, heavy swing, block, perfect block, counter, dodge, then the real fight) in the same corner teaching box as the first tutorial. |
| `OpenableCupboard.cs` | A cupboard you open with E: the doors swing out and a hidden note appears. |
| `LightSwitch.cs` / `RoomLights.cs` | The one working rocker on the cabin switch panel, and the ceiling lights it flickers out. When the lights go out the portal opens. |
| `PortalTransition.cs` | The walk-through sequence: rush in with a wider field of view, a round wipe in the portal colour, the load/teleport happens while the screen is covered, then the wipe opens on the new place. |
| `RealmStyle.cs` | The flat "realm" look for a section: one fill colour for everything, drawn only by thick outlines (like SCHiM, in 3D). Switched on while the object is enabled and off when its portal is used. Used in `Cad_lab`: the bench room (orange) and Lab 1 (teal, starts when you go through the Lab portal), and in `Classroom` (mustard / dark brown). In a realm the switchboard buttons and the sticky note become plain light plates with dark bold numbers (`ScreenGlow`), so the code stays readable. A realm can start when a portal is used (**Starts When Used**) and hands over cleanly to the next one. |
| `EndScreen.cs` | The final screen: fade to dark, score and time, Play Again / Quit. |
| `WorldTextUtil.cs` | Helper that applies the `WorldText` shader to 3D text. |

### Editor tools (`Assets/Editor/`)
These add menu items in the Unity Editor. They are not part of the game itself:
- **Tools → Make Box Colliders For Selected** (`BenchColliderMaker.cs`): replaces a detailed mesh collider with simple box colliders, one per bench.
- **Tools → Rebuild CAD Lab Colliders** (`CadLabColliders.cs`): rebuilds clean wall, floor and furniture colliders for the CAD lab.

- **Tools → Cabins → Build Both And Update Build List** (`CabinSceneBuilder.cs`): builds `Cabin_Tutorial` (normal shading) and `Cabin_Combat` (flat realm look) from `Assets/Models/CABINS.fbx` and puts them first in the build list. Safe to re-run: it rebuilds both scenes from scratch. `CABINS.fbx` is exported from `seventhFloor/Rooms/cabin-nearviblab_edited_v7.blend` (local only, not in git).
- **Tools → Realm Outline → Install** (`RealmOutlineSetup.cs`): adds the realm-look pass to the URP renderers (switched off). Run it once per project.
- **Tools → Realm Outline → Add To Open Scene**: adds a `RealmStyle` object to the open scene, wired to the renderers and to a portal named `LabPortal`.

The two collider tools can be undone with **Ctrl+Z**.

### Packages used (from `Packages/manifest.json`)
Universal RP 17.6.0, Input System 1.20.0, AI Navigation 2.0.14, uGUI 2.6.0, Timeline 6.6.0, Visual Scripting 1.9.12, Test Framework 1.8.0, plus the standard Unity modules.

> This project uses the **new Input System**. If you see input errors, check **Edit → Project Settings → Player → Active Input Handling** (it should be set to *Input System Package* or *Both*).

---

## 6. Troubleshooting

| Problem | Fix |
|---|---|
| Models are missing or pink, or the `.fbx` files are tiny | Git LFS wasn't installed when you cloned. Run `git lfs install` and then `git lfs pull`. |
| Everything is **pink/magenta** | The URP asset isn't assigned. Go to **Edit → Project Settings → Graphics** and set the render pipeline asset to `Assets/Settings/PC_RPAsset.asset`. |
| The scene is empty when the project opens | Unity opened a blank scene. Double-click `Assets/Scenes/Classroom.unity`. |
| "Portal can't find scene …" | Add `Cad_lab` to **File → Build Profiles → Scene List**. |
| "Portal is not set up: its Teleport To slot is empty" | In Cad_lab, select **LabPortal** and drag **LabArrival** into its **Teleport To** slot. |
| Can't walk into the portal in the bench room | Finish Lab Asst. Gemini's three questions first: the portal only appears after the last one. |
| The Keeper or the portal looks pink | The shader didn't load. Check that `Hologram.shader` is in `Assets/Resources/`. |
| Mouse doesn't move the camera | Click inside the Game view. **Esc** releases the cursor. |
| Stuck in a seat or the camera is tilted | Press **R**. |
| Lots of compile errors on first open | Wait for the package import to finish, then go to **Assets → Reimport All**. Also check you're using Unity 6000.6.3f1. |

---

## 7. Notes for contributors
- `Library/`, `Temp/`, `Logs/`, `UserSettings/` and the IDE files (`*.csproj`, `*.sln`) are **git-ignored**. Unity regenerates them.
- **Always commit `.meta` files** together with their assets. Unity uses them to keep references between assets.
- Large binary files (`.fbx`, `.blend`, `.png`, `.wav`, `.ttf`, …) go through Git LFS automatically via `.gitattributes`.
- GitHub rejects any single file over 100 MB, and the free plan includes about 1 GB of LFS storage and bandwidth.
