# ARVR Miniproject: Escape-Room Puzzle Game (Unity)

A first-person puzzle game built in Unity. You start in a **Classroom** and solve a light-switch code puzzle to open a portal to the **CAD Lab**. There, a hologram professor asks you three riddles, and your score decides how many beeping computers you must find and switch off before you escape.

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
| **E / Space / Left click** | In a conversation: skip the typing, then go to the next line |
| **1 / 2 / 3** or click | Pick an answer to a riddle |
| **R** | Emergency reset: stand up, close a conversation, and fix the camera if you get stuck. Progress is kept. |
| **Esc** | Release the mouse cursor (click the Game view to lock it again) |

---

## 4. How the game works

### Level 1: Classroom (`Classroom.unity`)
- The wall switches are numbered like a keypad: **1 2 3 4 5 6** on the top row and **7 8 9 0** on the second row.
- A sticky note with the code is hidden **under one of the desks**. Sit on a bench (press **E**) and look down to find it.
- Press the switches in the right order. A right code flashes green and a **portal** appears; a wrong code flashes red and resets.
- Walk into the portal to go to the CAD Lab.

<details>
<summary>Spoiler: the code</summary>

The code is set on the `CodePanel` component in the Classroom scene (currently `3-7-1-9`). The sticky note reads it from there automatically, so if you change it the note updates too.
</details>

### Level 2: The Keeper's riddles (`Cad_lab.unity`, bench room)
- You arrive in an empty bench room with a glowing, see-through **hologram professor**, "The Keeper". You can hear his low hum from about 10 m away, and he slowly turns to face you.
- Walk up to him and press **E** to talk. The text types out with little "voice" blips.
- He asks **3 riddles**. Each has 3 answers in a different order every time, and you get **one try** per riddle. A right answer flashes green with a chime; a wrong answer flashes red with a buzz.
- Your score sets the next level's difficulty:

  | Riddles right | Computers to switch off |
  |---|---|
  | 3 | 1 |
  | 2 | 2 |
  | 1 | 3 |
  | 0 | 4 |

- After the last riddle, a **portal** appears by the door. It's one-way: walk in and you appear in the computer lab. The doorway is a solid wall, so you can't see or walk into the lab before solving, and you can't walk back afterwards.

### Level 3: The beeping computers (`Cad_lab.unity`, Lab 1)
- Going through the portal starts the puzzle. One computer is **beeping** (3D sound), and its screen glitches now and then and shows a small error message.
- Follow the sound, look at the computer and press **E** to switch it off. Then the next one starts beeping somewhere else.
- Switch off as many as your riddle score decided. The computers are picked at random each time you play.
- If you play `Cad_lab` directly without the riddles (for testing), it uses 2 computers.

### The end
- When the last computer shuts down, the screen fades to dark and shows **"You escaped the multiverse!"** with your riddle score, the number of computers switched off, and your time.
- **Play Again** restarts from the Classroom. **Quit** closes the game (or stops Play mode in the editor).

---

## 5. Project structure

```
Assets/
├── Scenes/
│   ├── Classroom.unity        Level 1: switch-code puzzle
│   ├── Cad_lab.unity          Levels 2 and 3: riddle NPC + beeping-computer puzzle
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
│   ├── Hologram.shader        See-through glowing hologram (the Keeper)
│   ├── ScreenGlow.shader      Glowing computer screens
│   └── WorldText.shader       In-world text that gets hidden behind objects
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
| `RiddleNPC.cs` | The hologram professor: builds his body from simple shapes, adds his light and 3D sounds, runs the dialogue and riddles, and saves the score. |
| `GameProgress.cs` | Remembers the riddle score, the penalty and the start time while you move between scenes. |
| `BeepingComputerPuzzle.cs` | CAD lab puzzle: makes screens glow, makes one computer beep, and moves on to the next one when you switch it off. It waits until the portal starts it. |
| `ComputerSwitch.cs` | The invisible "press E" box on the beeping computer (created automatically). |
| `Portal.cs` | Portal you walk into. It either loads the next scene (Classroom → Cad_lab) or teleports you to a point in the same scene and then disappears (one-way, bench room → Lab 1). |
| `EndScreen.cs` | The final screen: fade to dark, score and time, Play Again / Quit. |
| `WorldTextUtil.cs` | Helper that applies the `WorldText` shader to 3D text. |

### Editor tools (`Assets/Editor/`)
These add menu items in the Unity Editor. They are not part of the game itself:
- **Tools → Make Box Colliders For Selected** (`BenchColliderMaker.cs`): replaces a detailed mesh collider with simple box colliders, one per bench.
- **Tools → Rebuild CAD Lab Colliders** (`CadLabColliders.cs`): rebuilds clean wall, floor and furniture colliders for the CAD lab.

Both tools can be undone with **Ctrl+Z**.

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
| Can't walk into the portal in the bench room | Finish the Keeper's riddles first: the portal only appears after the last riddle. |
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
