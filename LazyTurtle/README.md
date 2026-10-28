# Leniwy Żółw / Lazy Turtle: a school adventure in Kórnik

A colorful 3D school quiz adventure for children aged 8 to 11, set in
**Szkoła Podstawowa nr 2 im. Teofili z Działyńskich Szołdrskiej-Potulickiej w Kórniku**.

You play a little turtle with a yellow school backpack, seen from behind (third-person view).
The school has **8 floors**, and each floor has **10 classrooms**. In every classroom a dedicated old turtle
teacher waits at the desk with a gift. Each teacher looks like their subject: the science teacher in a lab coat
with a flask, the sports teacher in a tracksuit with a whistle, and so on. Every floor has different teacher names. Answer the question correctly and the gift is yours. A wrong
answer shows the right one, and the teacher asks a new question. When all 10 gifts are collected, the
door to the **Master Turtle's hall** at the end of the corridor opens. Answer the Master's harder
question and the stairs to the next floor open. After floor 8 you get a diploma.

## 3D engine

Version 3 renders with **OpenGL 2.1** (works with practically every graphics driver on Windows 7
and Windows 11; no extra installs). Features:
* real 3D school, characters and furniture, all built in code;
* baked lighting from the ceiling lamps, sunlight and shadows outdoors;
* 4x MSAA anti-aliasing, mipmapping and anisotropic filtering;
* vsync at 60+ FPS.

If OpenGL 2.0 is not available (for example in a virtual machine without a graphics driver), the game
falls back to the built-in software renderer automatically. `--software` forces it.

## Schoolyard, football and classmates

* A green **BOISKO** door in the lobby of every floor always leads out to the schoolyard. There you
  find a football pitch, Kórnik Castle across the lake, an Arboretum of trees, a lion fountain like
  the one in Teofila's garden, a pond with swans, peacocks and benches.
* Talk to **Kuba, the captain**, to play a match: you and Kuba against two classmates, first to 3
  goals or 2 minutes. Walk into the ball to dribble; press Space near the ball to kick. A win earns
  10 coins, a draw 5, a loss 3.
* Every classmate has a name. Press Enter next to one to chat (tips, jokes, Kórnik facts) or to
  get a riddle worth 3 coins.

## Kórnik in the game

* The school name and patron appear on the title screen, the lobby banner, the entrance sign and the diploma.
* The floors are named after Kórnik places and people: Arboretum, Jezioro Kórnickie, Zamek, Biblioteka Kórnicka,
  Biała Dama, the Działyński family, Teofila, and the Bnin town hall.
* A painting gallery hangs in the lobby and in every Master Turtle hall: **Teofila z Działyńskich
  Szołdrska-Potulicka (the White Lady of Kórnik, the school's patron)**, Tytus Działyński, Celina
  Działyńska, Jan Kanty Działyński, and Kórnik Castle by the lake. Walk up to a painting to read who
  it shows. The paintings are stylized in the game's own art style, not copies of the originals.
* The corridors show Kórnik posters with captions (swans on Lake Kórnik, the Arboretum, the Bnin
  town hall of 1777, the White Lady legend, the Kórnik Library, a map of Kórnik, Teofila's garden),
  plus display cases, an "SP 2 KÓRNIK" floor mosaic in the lobby and swan-pattern carpets.
* The quiz includes questions about Kórnik: the castle, the White Lady legend, Lake Kórnik, the
  Arboretum, the Kórnik Library and the school's patron.

## Quiz

10 subjects, one per classroom: Maths, Letters & words (Polish language), Nature, Geography & history,
English, Logic, Time & calendar, Music & art, Health & sport, Science & space. Every classroom has
3D objects for its subject (for example a telescope, a volcano model and a planet mobile for science)
and three subject posters with captions. Difficulty has 9 levels: floor 1 is for about age 8 and floor 8
for about age 11, and the Master Turtle asks one level harder. Topics include fractions, area and perimeter,
Roman numerals, parts of speech and spelling, English grammar, the history of Poland, biology and physics. Maths, logic, letters, words and time questions are generated,
so they rarely repeat. All questions are in Polish and English.

Children who can't read yet can have the question **read aloud** (R key or the button). This needs a
Windows voice in the game language: English voices come with Windows 7 and 11, and Polish voices come
with a Polish Windows 10 or 11.

## Controls

| Key | Action |
| --- | --- |
| Arrows / W S A D | walk and turn |
| Q / E | step sideways |
| Space | jump (gold coins float in the air) |
| Shift | run |
| Enter (or click the button) | talk to a teacher |
| 1 2 3 4, arrows + Enter, or mouse click | choose an answer |
| R | read the question aloud |
| Space near the ball | kick (in the schoolyard) |
| Right mouse button + move | look around / tilt the camera |
| Mouse wheel | camera closer / farther |
| Tab / M | big map |
| Esc | pause / leave a conversation |
| F11 or Alt+Enter | full screen |
| F3 | FPS counter |

Polish is the default language; English can be chosen in the menu. Progress (gifts per floor,
stars, coins) is saved in `%APPDATA%\LazyTurtle\save.txt`, so a child can stop and continue later.

## Requirements

* Windows 7 SP1, 8, 10 or 11 (32- or 64-bit)
* .NET Framework 4.x. Windows 10 and 11 already have it; on Windows 7 install .NET Framework 4.8 if it is missing.
* Any graphics chip with OpenGL 2.0 (practically all from 2006 on). Without one the game still runs with its software renderer.

## Building

Open `LazyTurtle.sln` in Visual Studio 2019/2022 and press F5, or run `dotnet build LazyTurtle.sln -c Release`.

Developer switches: `LazyTurtle.exe --floor 3` starts on floor 3; `--debug` enables F5 (go to the
next teacher), F6 (go to the Master Turtle) and F7 (go to the next painting).

## Install package

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 [-Version 2.0.0]
```

Creates `out\LazyTurtle-<version>.msi` (x86 installer with Start menu and desktop shortcuts and a
.NET Framework 4 check) and `out\LazyTurtle-<version>-portable.zip`. The script needs the .NET SDK
8+ and downloads WiX 5 from nuget.org on first run.

## Project layout

| Path | What |
| --- | --- |
| `src/LazyTurtle/School.cs` | floor plan, classrooms, subjects, floor themes, gallery |
| `src/LazyTurtle/Questions.cs` | the question bank (PL/EN) |
| `src/LazyTurtle/Game.cs` | rules, third-person camera, quiz dialogs, floors |
| `src/LazyTurtle/GLRenderer.cs`, `GL.cs`, `Mesh.cs` | OpenGL 3D engine, bindings, mesh builder |
| `src/LazyTurtle/Models.cs` | procedural 3D models (turtles, teachers, furniture, trees, fountain, animals, goals) |
| `src/LazyTurtle/Yard.cs`, `Football.cs`, `Chat.cs` | schoolyard, football match, classmates' chats and riddles |
| `src/LazyTurtle/ArtKornik.cs` | Kórnik posters, mosaic, display case, yard textures |
| `src/LazyTurtle/Renderer.cs` | software raycaster (fallback) |
| `src/LazyTurtle/Art.cs` | all textures, characters, paintings and gifts, painted in code |
| `src/LazyTurtle/Hud.cs` | HUD, dialogs, menus, diploma; mouse hit areas |
| `src/LazyTurtle/Speech.cs` | reading questions aloud (System.Speech) |
| `installer/Package.wxs` | WiX MSI definition |
