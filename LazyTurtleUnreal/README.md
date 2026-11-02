# Leniwy Żółw / Lazy Turtle: a school adventure in Kórnik

A 3D school quiz adventure for children aged 8 to 11, set in
**Szkoła Podstawowa nr 2 im. Teofili z Działyńskich Szołdrskiej-Potulickiej w Kórniku**.
Version 5 is built with **Unreal Engine 5.8** for photorealistic 3D (Lumen global illumination and
reflections, Nanite, virtual shadow maps, hardware ray tracing when the GPU has it).

You play a little turtle with a yellow school backpack, seen from behind (third-person view).
The school has **8 floors**, and each floor has **10 classrooms**. In every classroom an old turtle
teacher waits at the desk with a gift. Answer the question correctly and the gift is yours. A wrong
answer shows the right one, and the teacher asks a new question. When all 10 gifts are collected, the
door to the **Master Turtle's hall** at the end of the corridor opens. Answer the Master's harder
question and the stairs to the next floor open. After floor 8 you get a diploma.

## Status of the Unreal port

This is the first pass (vertical slice) of the move from the old C# / OpenGL game to Unreal:

* **Ported:** all 8 floors (built in code from the original floor plan), the quiz rules, gifts, the Master
  Turtle, stairs, stars, coins, saving, Polish and English, reading questions aloud, classmates' chats and
  riddles, the paintings and Kórnik posters with captions, menus, HUD and diploma.
* **Not yet ported:** the schoolyard, the football match and the map (Tab / M). The green BOISKO door
  in the lobby says the playground is coming soon.
* **Art:** the code builds every floor with placeholder shapes tinted in each floor's colours. Real
  photorealistic look comes from assets you add (see [Making it photorealistic](#making-it-photorealistic)).

## Requirements

* Windows 10 or 11, 64-bit
* A DirectX 12 graphics card (NVIDIA GTX 1070 / AMD RX 5700 or newer recommended; RTX for ray tracing)
* To build: Unreal Engine 5.8 (Epic Games Launcher), Visual Studio 2022 with the
  *Game development with C++* workload, and the .NET 8 SDK (for the quiz data tool)

Windows 7, 32-bit Windows and OpenGL 2 graphics are no longer supported. The last version that runs
there is the C# edition in git history (commit `e8f098b`).

## Building

1. Install Unreal Engine 5.8 from the Epic Games Launcher.
2. Right-click `LazyTurtle.uproject` > *Generate Visual Studio project files*, open `LazyTurtle.sln`,
   and build `LazyTurtleEditor` (Development Editor, Win64). Or double-click the `.uproject` and let the
   editor compile the module.
3. Press Play in the editor. The game starts in an empty map and builds the school from code.

Developer switches (packaged game or `-game` in the editor): `-floor=3` starts on floor 3; `-debug` enables
F5 (go to the next teacher), F6 (go to the Master Turtle) and F7 (go to the next painting).

## Package

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 [-Version 5.0.0] [-EngineDir "C:\Program Files\Epic Games\UE_5.8"]
```

Regenerates the quiz data, packages a Shipping Win64 build with Unreal Automation Tool and writes
`out\LazyTurtle-<version>-win64.zip`.

## Install package

```powershell
powershell -ExecutionPolicy Bypass -File .\build-installer.ps1 [-Version 5.0.0] [-EngineDir ...] [-SkipBuild]
```

Runs `build.ps1` (or reuses `out\package` with `-SkipBuild`) and creates:

* `out\LazyTurtle-<version>-Setup.exe`: give this one to users. It installs the Unreal prerequisites (Visual C++
  runtime) when they are missing, then the game.
* `out\LazyTurtle-<version>.msi`: the game alone (x64, per-machine, Start menu and desktop shortcuts), for
  computers that already have the prerequisites or for deployment tools.

The script downloads WiX 5 from nuget.org on first run (local dotnet tool, `.config\dotnet-tools.json`).

## Making it photorealistic

The game never needs an asset to run, but every surface, prop and character can be swapped for a real one in
**Project Settings > Game > Lazy Turtle Art** (saved in `Config/DefaultGame.ini`):

1. Get free photoreal assets from [Fab](https://www.fab.com) (Quixel Megascans are free for Unreal): wood
   parquet, terrazzo/marble, painted plaster, carpet, school desks and chairs, blackboard, plants, benches,
   lockers, bookshelves, a globe, a piano, and so on. Put them under `Content/LazyTurtle/`.
2. Pick the materials for walls, floors and ceilings, and map prop keys (`tdesk`, `desk0`..`desk3`, `plant`,
   `bench`, `gift`, `coin`, `globe`, the subject artifacts such as `m_abacus` or `sc_telescope`, and so on) to
   static meshes. Meshes should face +X, have the pivot on the floor and use real-world centimetres.
3. Add turtle characters: a skeletal mesh and an animation blueprint for the player, teachers, the Master Turtle
   and classmates. Without them the turtles are built from basic shapes.
4. Paintings and posters: map caption ids (0-4 paintings, 100+ Kórnik posters, 200+ subject posters) to textures.

Prefer world-aligned materials for walls and floors: the floor builder places one tile per floor-plan cell.

## Controls

| Key | Action |
| --- | --- |
| Arrows / W S A D | walk and turn |
| Q / E | step sideways |
| Space | jump (gold coins float in the air) |
| Shift | run |
| Enter (or click the button) | talk to a teacher or a classmate |
| 1 2 3 4, arrows + Enter, or mouse click | choose an answer |
| R | read the question aloud |
| Right mouse button + move | look around / tilt the camera |
| Mouse wheel | camera closer / farther |
| Esc | pause / leave a conversation |
| F11 or Alt+Enter | full screen |
| F3 | FPS counter |

Polish is the default language; English can be chosen in the menu. Progress (gifts per floor, stars,
coins) is saved with Unreal's save system (`%LOCALAPPDATA%\LazyTurtle\Saved\SaveGames`). On first start
the old `%APPDATA%\LazyTurtle\save.txt` is imported, so children keep their progress.

Reading aloud uses a Windows voice in the game language. English voices come with every Windows 10 and 11,
and Polish voices come with a Polish Windows or can be added in Settings > Time & language > Speech.

## Quiz

10 subjects, one per classroom: Maths, Letters & words (Polish language), Nature, Geography & history,
English, Logic, Time & calendar, Music & art, Health & sport, Science & space. Difficulty has 9 levels:
floor 1 is for about age 8 and floor 8 for about age 11, and the Master Turtle asks one level harder.
All questions are in Polish and English.

The questions are written and generated in C# in `tools/QuizExport` (the generators from the original game)
and exported to `Content/Data/quiz.json` and `Content/Data/strings.json`, which the game reads at runtime.
See `tools/QuizExport/README.md`.

## Project layout

| Path | What |
| --- | --- |
| `LazyTurtle.uproject`, `Config/` | Unreal project and settings (renderer, maps, packaging, art settings) |
| `Source/LazyTurtle/LTTypes.h` | shared constants, map-to-world coordinates, question type |
| `Source/LazyTurtle/LTSchool.*` | floor plan, classrooms, subjects, floor themes, teachers, captions |
| `Source/LazyTurtle/LTWorld.*` | builds a floor in 3D: walls, lights, decorations, props, turtles, gifts, coins |
| `Source/LazyTurtle/LTArtSettings.h` | the photoreal asset slots (Project Settings > Game > Lazy Turtle Art) |
| `Source/LazyTurtle/LTGameMode.*` | game rules, screens, quiz dialogs, floors |
| `Source/LazyTurtle/LTTurtleCharacter.*`, `LTPlayerController.*` | the player turtle, third-person camera, input |
| `Source/LazyTurtle/LTGameUI.*` | menus, HUD, dialogs, diploma (Slate) |
| `Source/LazyTurtle/LTServices.h`, `LTQuizSubsystem.cpp`, `LTSaveSubsystem.cpp`, `LTAudioSubsystem.cpp`, `LTSpeechSubsystem.cpp` | quiz data, saving, synthesized sounds, reading aloud |
| `Content/Data/` | exported quiz questions and texts (JSON) |
| `tools/QuizExport/` | question generators and the exporter (.NET 8) |
| `build.ps1`, `build-installer.ps1` | packaging and installer scripts |
| `installer/` | WiX MSI (`Package.wxs`) and Setup.exe bundle (`Bundle.wxs`) |
| `Build/Windows/Application.ico` | game and installer icon |
