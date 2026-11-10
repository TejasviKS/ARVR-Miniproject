# Technical Report: the flat "realm" look, model clean-up, and other notes

*Grand Theft Attendance* (AVR mini-project, Unity 6 / URP). This report explains how the game gets its flat, outlined
"realm" look, how to fix inverted normals and other common model problems by hand, and a few other technical points worth
knowing before you change or extend the project.

> **About the name.** The look is inspired by the 2D platformer *SCHiM*: the whole picture is a few flat colours, with
> dark silhouettes drawn against them. Here it is done in **3D**, as a full-screen post-process, so any model drops into it
> without special art. Nothing in the project is taken from that game; it is a technique, not an asset.

---

## 1. The realm look: how it works

### 1.1 The idea in one paragraph
Every realm (a level or part of a level) has three colours: a **fill** colour for everything, a **line** colour, and a **void**
colour for empty space. The renderer ignores the scene's own materials, textures and lighting, paints the fill colour on
every pixel, and then draws the line colour wherever the *shape* of the world changes: at the silhouette of an object, and at
corners and creases. Because the shapes come from the geometry (depth and surface direction), not from the textures,
the result is a clean, hand-inked look from ordinary 3D models.

### 1.2 The pieces

| Piece | File | Job |
|---|---|---|
| Full-screen shader | `Assets/Shaders/RealmOutline.shader` | Does all the drawing: fill, outlines, void and the colour-reveal wave. |
| Renderer feature (set-up tool) | `Assets/Editor/RealmOutlineSetup.cs` | Adds the shader as a **Full Screen Pass Renderer Feature** to every URP renderer, switched **off**. |
| Controller | `Assets/Scripts/RealmStyle.cs` | Switches the feature on/off, pushes the palette and line settings to the shader, handles portals. |
| Palettes | one `RealmStyle` per realm, in each scene | See the table in 1.5. |

### 1.3 The shader, step by step
The pass runs *before transparents* and uses two things URP can provide for free: the **depth texture** and the
**normals texture** (the pass asks for them with `requirements = Depth | Normal`, which makes URP run a depth-normals
pre-pass). For every pixel:

1. **Void.** If the depth is exactly on the far plane, nothing was drawn there (open sky, a room with no ceiling), so the
   pixel gets the **void colour** and we stop.
2. **Pick four neighbours.** The shader samples the four diagonal neighbours of the pixel. The distance to them is the
   **line thickness**, given in pixels *at 1080p* and scaled with the screen height, so the lines look the same on every resolution.
3. **Silhouette edges (depth).** It does not compare depths directly. Across any flat surface, **1 / eye-depth** changes
   in a straight line on screen, so its *second difference* is zero on flat walls and floors and only spikes at a real edge
   (where one object ends and another begins behind it). The spike is divided by the depth itself, so near and far edges
   are treated alike, then passed through a `smoothstep` between `depthThreshold` and twice that.
4. **Crease edges (normals).** It compares the surface normal of the pixel with its four neighbours
   (`1 - dot(n1, n2)`, summed). A wall meeting a floor, or the corner of a box, changes the normal sharply and gets a line.
   Neighbours that are void are skipped: the depth test already draws that edge.
5. **Mix.** `edge = max(depthLine, creaseLine)`; the output is `lerp(fill, line, edge)`.
6. **Colour-reveal wave (optional).** When the boss is defeated, `_RealmReveal` holds a world-space point and a radius.
   The shader rebuilds each pixel's world position from the depth (`ComputeWorldSpacePosition`), and inside the radius it
   shows the real scene colour (the pass has *Fetch Color Buffer* on) instead of the flat look, with a bright rim at the
   front of the wave. This is the "colours spread out from where he fell" effect in the Claude fight.

All the numbers reach the shader as **global shader values** (`_RealmFill`, `_RealmLine`, `_RealmVoid`, `_RealmParams`,
`_RealmReveal`, `_RealmRevealEdge`), so there is a single material and each realm just sets different values.

> **Colour space.** The colours you choose in the Inspector are what you see in the picker. The shader needs them in
> linear space, so `RealmStyle` sends `color.linear`. If you ever set these values from another script, do the same, or the
> palette will look washed out.

### 1.4 The controller (`RealmStyle`)
- **On/off follows `enabled`.** While the component is enabled the look is on. `OnEnable` / `OnDisable` flip the renderer
  feature (`SetActive`) on **every** renderer listed in *Outline Features* (PC and Mobile each have their own copy).
- **One realm at a time.** A static `active` reference makes a realm that switches on hand over cleanly from the previous one,
  so the order of a portal's events does not matter.
- **Portals.** `endsWhenUsed` ends the realm when that portal is used; `startsWhenUsed` starts a (disabled) realm when a portal is used.
  This is how the bench room (purple) hands over to Lab 1 (teal) inside the same scene.
- **Post-processing off.** Bloom/tonemapping would change the colours you picked, so the player camera's post-processing is
  switched off while a realm is active and restored afterwards.
- **Live tuning.** In Play mode the shader values are refreshed every frame, so you can drag the sliders in the Inspector
  and watch the lines change.

### 1.5 The palettes in the game

| Scene | Fill | Line / void |
|---|---|---|
| `Cabin_Combat` | orange (0.95, 0.54, 0.19) | near-black indigo (0.11, 0.08, 0.19) |
| `Classroom` | mustard (0.96, 0.72, 0.20) | dark brown (0.20, 0.10, 0.05) |
| `Cad_lab` bench room | purple (0.49, 0.38, 0.72) | dark indigo (0.10, 0.07, 0.20) |
| `Cad_lab` Lab 1 | teal (0.18, 0.77, 0.71) | dark teal (0.04, 0.18, 0.23) |
| `Classroom_boss` | red (0.66, 0.06, 0.12) | near-black red (0.09, 0.01, 0.03) |

The portal colour of each level matches the realm it leads to; that is how the player reads where a door goes.
The menu poster uses the same family of colours (teal, orange, red, purple, gold).

### 1.6 Setting it up in a new scene
1. Once per project: **Tools → Realm Outline → Install** (adds the feature to the renderers; safe to re-run).
2. In the scene: **Tools → Realm Outline → Add To Open Scene** (adds a `RealmStyle` object, wired to the renderers and to a portal named `LabPortal` if one exists).
3. Choose the three colours. Then tune the lines (below).
4. Optional: set *Ends When Used* / *Starts When Used* to portals.

### 1.7 Tuning the lines

| Setting | Lower | Higher | Typical |
|---|---|---|---|
| `lineThickness` (px at 1080p) | thin, delicate | bold, comic-book | 4 |
| `depthThreshold` | more outlines (also on small steps) | only big silhouettes | 0.04 |
| `creaseThreshold` | lines on gentle curves too | only sharp corners | 0.5 |

Practical advice:
- A **messy model** (lots of tiny bevels, cables, text) gets too many lines. Raise both thresholds, or simplify the model.
- A **round, smooth model** gets no corner lines. Lower `creaseThreshold`, or add a slight bevel in the model.
- Lines flickering on a distant flat floor mean the depth threshold is too low for the camera's far plane.

### 1.8 Cost
Per pixel the pass does about 5 depth reads, 5 normal reads and a colour read when the wave is on, with no loops and no
extra render targets. That is cheap enough for the low-end laptops this project targets. The expensive part is the
**depth-normals pre-pass** URP adds, which draws the scene one more time; keep the polygon count of the realms reasonable
(see section 3).

### 1.9 Characters and UI use other techniques
- **Holograms** (Lab Asst. Gemini, the AI professors): `Hologram.shader` / `HologramHostile.shader` in `Assets/Resources/`: a
  see-through rim-lit shader with a glitch effect; the bodies are built from simple shapes in code.
- **UI**: everything is drawn with Unity's immediate-mode GUI (`OnGUI`) in a "GTA-poster" style: a big display font with a dark
  outline made by drawing the text eight times around itself (`GtaMenuUI.Outlined`), rounded pill buttons, procedural textures
  (circles, gradients, vignettes) generated in code, so the project has almost no UI image files.
- **Sound effects** are generated in code (`CombatSfx`, portal whoosh, hums), which keeps the project small.

---

## 2. Why normals matter here, and how to fix inverted ones by hand

### 2.1 Why this look is sensitive to normals
The crease lines come from the **normals texture**, and a face is only drawn if it faces the camera (**back-face culling**).
A face's front is decided by its **winding order** (the order its vertices are listed), and its normal normally follows it.
When a model has **inverted** (flipped) faces you see one of these:

| Symptom | Usual cause |
|---|---|
| A wall or object is invisible from one side, or you see "through" it | The face points away; back-face culling hides it |
| Holes in chairs, thin tabletops or the inside of a box | Some faces are inverted, or the object is a single flat sheet with no thickness |
| Lines where there should be none (or a missing corner line) | The normals of neighbouring faces disagree |
| A whole object looks inside-out, or lit from the wrong side | The object has a **negative scale**, or the entire mesh was exported inverted |
| Flickering, stripy surfaces | Two faces occupy the same place (duplicates): **z-fighting** |

In this project the Cad lab needed exactly this kind of work: table legs and leg tops, chair holes caused by back faces, board
strips of the wrong width. The fixes were made as corrected mesh copies (the `*_fixed` assets in `Assets/Models/`), small
"fill" meshes for the chair holes, and two-sided material copies for thin parts.

### 2.2 Find the problem first (do this before fixing anything)
**In Blender:**
1. Open the **Viewport Overlays** dropdown and tick **Face Orientation**. Faces pointing outward turn **blue**, inverted
   faces **red**. A correct model is blue everywhere you can see from outside (and, for a room, blue on the *inside* walls).
2. Switch to **Edit Mode** and use *Overlays → Normals → Display face normals* to see small lines on the faces. They should
   point out of solid objects, and *into* a room from its walls.
3. Turn on **Statistics** to watch triangle counts, and select **Select → Select All by Trait → Non Manifold** to find holes,
   loose geometry and edges shared by more than two faces.

**In Unity:**
- Scene view: set the shading mode to **Shaded Wireframe** to see the triangles, and look at the object from behind: if it vanishes, the faces are inverted.
- Select the object and check its **Transform → Scale**: a **negative** value flips the winding.
- Use **Window → Analysis → Rendering Debugger** to look at the depth/normal-style views when the outlines look wrong; if the
  normals look wrong there, the mesh is the problem, not the shader.
- Temporarily switch the material to render **both faces** (Render Face: *Both*): if the hole disappears, it was a back-face problem.

### 2.3 Fixing it in Blender (the real fix; do it in the source file)
Work in **Edit Mode** with the faces you want selected (press **A** for everything).

1. **Recalculate normals**: *Mesh → Normals → Recalculate Outside* (**Shift + N**). This makes the whole selection consistent and
   pointing away from the inside of a closed shape. For a **room** (an enclosed space you stand inside) use *Recalculate Inside*
   (**Shift + Ctrl + N**), so the walls face into the room.
2. **Flip just the wrong faces**: select only the red faces and use *Mesh → Normals → Flip* (**Alt + N → Flip**).
3. **Negative scale**: if an object was mirrored by scaling to -1, select it in Object Mode and **apply the scale**
   (**Ctrl + A → Scale**), then recalculate the normals again. (Applying a negative scale flips the faces; recalculating fixes the result.)
   While you are there apply **Rotation** too, so the object has scale (1, 1, 1) and rotation (0, 0, 0).
4. **Remove duplicates and junk**: *Mesh → Clean Up → Merge by Distance* (removes doubled vertices, which also cures z-fighting from
   duplicated faces), then *Delete Loose*. Merge by distance with a small threshold (0.0001 m) is usually safe.
5. **Give flat sheets thickness**: a chair back or a thin shelf modelled as a single plane has only one visible side. Add a
   **Solidify** modifier (a few millimetres) and apply it, or extrude the faces. This is more robust than relying on two-sided materials.
6. **Fix bad polygons**: triangulate or "beauty-fill" large n-gons that are not flat (*Face → Triangulate Faces*, **Ctrl + T**), so
   different programs do not triangulate them differently when exporting.
7. **Shading**: if faces look faceted or smooth in the wrong places, use *Shade Smooth* / *Shade Flat* on the right parts, or
   *Shade Auto Smooth* with an angle (about 30°) so hard corners stay hard. For this flat look, hard edges are fine and even good.
8. **Tidy the object**: set the origin where it makes sense (e.g. the base of a chair: *Object → Set Origin*), use real-world
   scale (1 Blender unit = 1 m), name the objects clearly, and delete hidden faces nobody will see (underside of a floor, the back of
   a wall against another wall).

**Doing it for many objects at once.** A quick Blender script (paste into the Scripting tab with the objects selected):

```python
import bpy, bmesh

for obj in bpy.context.selected_objects:
    if obj.type != 'MESH':
        continue
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)   # consistent, pointing outward
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0001)
    bm.to_mesh(obj.data)
    bm.free()
print("done")
```

(Do not run it on a room that must face inward; use *Recalculate Inside* by hand for those.)

### 2.4 Exporting from Blender to Unity
Use **File → Export → FBX** and check:
- **Selected Objects** only (when you want one part), and **Apply Transform** ticked, so scale and rotation arrive as you see them.
- Forward **-Z**, Up **Y** (Unity's convention) and the default *FBX Units Scale* (the model arrives in metres).
- **Smoothing: Face** (or *Normals Only*) so the exported normals are the ones you fixed.
- **Triangulate Faces** ticked if you had n-gon trouble.

In Unity, select the `.fbx` and in the **Model** tab of the import settings:
- **Normals: Import** (to trust your fixed normals) or **Calculate** (Unity recomputes them: pick **Angle Weighted**; use *Smoothing angle* to keep hard corners). The big project models currently *import* their normals.
- **Weld Vertices** on, **Mesh Compression** off (compression can distort small models), **Read/Write** on only if a script needs the mesh (the lab models do, for finding monitors and building colliders; otherwise leave it off to save memory).
- Press **Apply**, and if you changed the file, make sure Unity re-imported it (the `.meta` file changes).

### 2.5 Fixing it inside Unity when you cannot go back to Blender
These are workarounds; prefer the Blender fix when the source file exists (the `.blend` files live in the local `seventhFloor/` folder).

**a) Show both sides of the material.** In the material, set **Render Face** to **Both** (Cull Off). The face then draws from either side. Caveat: the back face of a normal-mapped or lit material gets lit from the wrong side, and the outline pass still sees the face normal as written, so *prefer a solid fix for anything the player stands next to*.

**b) Flip the winding of a mesh with a small editor script** and save the result as a new mesh asset (never edit the imported mesh in place):

```csharp
// Editor-only helper: makes a copy of the selected object's mesh with the triangles reversed.
using UnityEditor;
using UnityEngine;

public static class FlipMesh
{
    [MenuItem("Tools/Flip Selected Mesh Normals")]
    static void Flip()
    {
        var mf = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<MeshFilter>() : null;
        if (mf == null) { Debug.LogWarning("Select an object with a MeshFilter."); return; }

        Mesh m = Object.Instantiate(mf.sharedMesh);                 // work on a copy
        for (int s = 0; s < m.subMeshCount; s++)
        {
            int[] t = m.GetTriangles(s);
            for (int i = 0; i < t.Length; i += 3) (t[i], t[i + 1]) = (t[i + 1], t[i]);   // swap two corners = flip the face
            m.SetTriangles(t, s);
        }
        m.RecalculateNormals();
        AssetDatabase.CreateAsset(m, "Assets/Models/" + mf.sharedMesh.name + "_flipped.asset");
        mf.sharedMesh = m;
    }
}
```
This is the same idea as the `*_fixed.asset` meshes in `Assets/Models/`: a corrected copy of the mesh that the scene uses instead of the imported one.

**c) Fill a hole with a small extra mesh.** The chair holes were closed with little "fill" meshes (`ChairFill_*`) placed in the gap, which costs a few triangles instead of changing the big model.

**d) ProBuilder** (if installed): select the faces and use *Flip Face Normals*, or *Conform Face Normals* to make a selection consistent.

**e) Negative scale in the Inspector.** Unity flips the winding for you when a Transform has a negative scale, so a mirrored object usually renders right, but the *normals pass* and some shaders can still misbehave with non-uniform negative scales. If you see odd lines on a mirrored object, replace the negative scale with a mirrored mesh made in Blender.

### 2.6 A short checklist after fixing
- [ ] Walk around the object: nothing disappears from any angle you can reach.
- [ ] No red faces in Blender's Face Orientation view.
- [ ] No lines where there should be none, and corner lines where there should be (check in a realm, since the look exposes normal errors).
- [ ] No flickering on flat surfaces.
- [ ] Scale (1, 1, 1), sensible pivot, sensible name; it still lines up with its collider.

---

## 3. Model refinement for a flat, low-end-friendly game

- **Polygon budget.** Realms are drawn twice (scene + depth-normals). Remove everything the camera never sees: undersides of floors, hidden backs
  of furniture, interior faces of walls that touch. Decimate (Blender *Decimate* modifier, Collapse, ratio ~0.5) objects that are
  small on screen.
- **Textures do not matter in a realm.** The flat look ignores them, so a realm model needs no detailed textures; only the *shape* counts.
  (The normal levels, such as the cabin tutorial, still show materials, so keep clean base colours.)
- **Bevel big edges slightly.** A tiny bevel on a hard corner gives a cleaner line than a perfectly sharp 90° edge, and avoids pixel shimmer.
- **Avoid tiny details** (screws, text, cables) in realm models: each one becomes a line. Put them on a separate object so you can hide them.
- **One object per thing that moves or needs a collider** (a door, a bench), so scripts can find and treat it separately.
- **Colliders.** Detailed mesh colliders are slow and snag the player. The project replaces them with simple box colliders:
  - `Tools → Rebuild CAD Lab Colliders` builds clean walls/floor/desks for the lab (positions were measured from the Blender file).
  - `Tools → Make Box Colliders For Selected` replaces a mesh collider with boxes for each bench.
  Chairs, cables and computers get **no** collider on purpose, so the player cannot snag on them.
- **Consistent materials.** Reusing a few materials lets Unity batch draws. The two-sided material copies exist only for the few parts that needed them.
- **Naming.** Keep the Blender names meaningful (`teachers_table`, `chair_orange`): the scripts and fixes in this project find objects by name.

---

## 4. Other technical points worth knowing

### 4.1 Audio
- **Music is streamed** (`Assets/Resources/Music/`): the long tracks (the Claude fight track is nearly 14 minutes, about 160 MB decoded) froze the
  game for over a second when decoded at level start. `MusicImportSettings.cs` imports every file in that folder as *Streaming*.
- **Loudness without decoding.** A streamed track cannot be measured at run time, so **Tools → Music → Measure Loudness** measures each file once and writes
  `Assets/Resources/Music/loudness.txt` (`name=rms` per line). `MusicDirector` turns each number into a per-track volume
  (`min(1, ReferenceRms * weight / rms)`), so any file you drop in sounds equally loud, scaled by a per-track *weight*.
  **Re-run the tool whenever you add or replace a music file.** A track whose file is already at full scale cannot be made louder by volume alone (volume is capped at 1); raise the file itself.
- **The director** (`MusicDirector.cs`) keeps one audio layer per track and fades them by level², and combines several multipliers: dialogue
  ducking, the portal whoosh dip, the Lab-1 "listen" dip, the pause dip and the player's volume option. It never cuts music on a scene change.
- **Leaving for the menu** (`MenuExit.cs`) fades the screen and the music out together over 1.25 s before loading the menu.
- **Original music:** `menu.ogg` and `credits.ogg` are the author's own compositions (Speedbird-One). The other tracks are third-party; see section 6.

### 4.2 Scenes, loading and performance
- **Scene order in the build:** `Splash`, `MainMenu`, `Cabin_Tutorial`, `Cabin_Combat`, `Classroom`, `Cad_lab`, `Classroom_boss`.
- **Portal preloading.** A portal to another scene starts loading it in the background (low priority, so the frame rate holds) about 0.75 s after it
  opens; stepping in only flips the switch (`Portal.cs`, `PortalTransition.cs`). The vortex is also drawn invisibly for a few frames at level start to warm up its shader.
- **Frame rate** is capped at 60 (`FrameRateCap.cs`), and the game keeps running when the window loses focus.
- **No domain reload.** The project has *Enter Play Mode Options* on, so static variables survive between Play sessions. Every script with static state resets it in a
  `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` method; do the same in new scripts or you will see stale state.
- **Input.** The project uses the **new Input System only**; use `Keyboard.current` / `Mouse.current`, not `UnityEngine.Input`.
- **IMGUI text colour.** A `GUIStyle` copied from the skin can lose its text colour; the project pins the colour of every state before drawing
  (`WastedScreen.Pin`). If text draws black or invisible, that is why.

### 4.3 The game flow, in short
`Splash` (a 5 s black pause, then Cave Labs and Kengeri Studios; a build shows Unity's own "Made with Unity" screen before it) → `MainMenu` (poster, Play / Options / Credits / Quit; Credits plays the credits music with a bang reveal and a
rolling list) → `Cabin_Tutorial` → `Cabin_Combat` (combat tutorial vs GPT) → `Classroom` (switch-code puzzle; the Letter of Recommendation is hidden here) →
`Cad_lab` (Gemini's quiz, then fix the beeping computers) → `Classroom_boss` (Prof. Claude; the Letter unlocks a secret Fable phase) → Mission Passed → Main Menu.

---

## 5. Building the game (Windows)
1. **File → Build Profiles**, select **Windows**, and check the scene list order above (Splash first).
2. **Player Settings:** set Company Name, Product Name, and Fullscreen Mode (*Fullscreen Window* recommended).
3. **Build**, choose an empty folder. Zip the *whole* output folder to share it.
4. Run the built game once from start to finish before sharing: editor behaviour (cursor, audio, scene-load hitches) can differ from a build.

---

## 6. Licensing and things to fix before publishing
- **Third-party music.** `calm.ogg` (*Aria Math*, drum cover by Drummy Walnut), and the combat/Fable tracks (*Halo 3 "Warthog Run"* edit by Dirghadarzana) are other people's
  recordings of other people's music, not licensed for redistribution. Fine for a class demo; replace them (same file names, then run **Tools → Music → Measure Loudness**) before
  any public release or video.
- **Ripped sounds.** `Assets/Resources/Audio/mission_passed.mp3` and `wasted.mp3` are ripped game sounds: remove them before publishing.
- **Fonts.** The Pricedown font is free for personal use only.
- **Unity splash screen.** The game has no "Made with Unity" screen of its own: a build shows Unity's automatically at the very start (see *Player Settings → Splash Image*), which is why the `Splash` scene begins with a 5 s black pause.
- **Models.** The building models were provided by Cave Labs @ PES University: keep the credit.
- The `Assets/_Recovery/` folder is editor auto-recovery output and is intentionally left out of git.

---

## 7. Quick reference

| I want to... | Do this |
|---|---|
| Change a realm's colours | Select the scene's `RealmStyle` and edit Fill / Line / Void |
| Add the look to a new scene | Tools → Realm Outline → Install (once), then Add To Open Scene |
| Too many / too few outlines | Raise / lower `depthThreshold` and `creaseThreshold` |
| A model is invisible from one side | Blender Face Orientation → fix → Shift+N → re-export (or Render Face: Both as a stopgap) |
| A music file is too quiet or loud | Change its *weight* in `MusicDirector.cs`, or replace the file and run Tools → Music → Measure Loudness |
| The game stutters when a level starts | Check for very long audio decoded on load, or heavy `Start()` work; stream or defer it |
