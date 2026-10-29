# QuizExport

Exports the quiz questions, classmates' chat and UI texts of the original C# game to JSON for the Unreal port.
`Questions.cs`, `Chat.cs` and `Lang.cs` are the original sources (only `partial` was added to their classes);
`Program.cs` drives them and writes:

- `Content/Data/quiz.json` – questions, riddles, kids' names and chat lines (PL and EN)
- `Content/Data/strings.json` – UI texts: `"key": ["pl", "en"]` (Lang.cs keys plus a few chat / port keys)

Both are UTF-8 without BOM and are loaded at start-up by `ULTQuizSubsystem` (`Source/LazyTurtle/LTQuizSubsystem.cpp`).

## Regenerate

Requires the .NET 8 SDK (or newer SDK with the .NET 8 runtime):

```
cd tools/QuizExport
dotnet run -c Release            # writes ../../Content/Data/*.json (found by walking up to the repo root)
dotnet run -c Release -- <dir>   # or into another directory
```

The output is deterministic (fixed seed), so re-running without changes gives identical files.
After editing questions or texts, rerun and commit the JSON together with the C# change.

## quiz.json

```
{
  "version": 1,
  "listSubjects": [2, 3, 7, 8, 9],    // subjects served from hand-written lists (Questions.FromList)
  "listSpread": 2,                    // FromList's search distance for those subjects
  "kornikSpread": 9,                  // ... and for the Master Turtle's Kórnik questions
  "pl": {
    "subjects": [ [ [Q, ...] x 9 difficulties ] x 10 subjects ],
    "kornik":   [ [Q, ...] x 9 difficulties ],
    "riddles":  [Q, ...],
    "kids":     ["Kuba", ...],
    "school":   ["line", ...],        // chat in the school; {c} = captain's name (kids[0])
    "yard":     ["line", ...]         // chat in the schoolyard
  },
  "en": { ... }
}
Q = {"q": "text", "a": ["right answer", "wrong", ...], "c": 0}   // "c" (index of the right answer) defaults to 0
```

- **Generated subjects** (0 maths, 1 letters, 4 words/English, 5 logic, 6 time): each bucket holds up to 150 unique
  questions the generator produces at that difficulty during one long session (asked questions are remembered, so
  the list-based parts of those generators move on to nearby difficulties exactly as in the game). Fewer when the
  generator has less variety.
- **List subjects** (2 nature, 3 geography/history, 7 music/art, 8 health, 9 science) and **kornik**: each bucket
  holds the list items of exactly that difficulty. The game searches like `FromList`: a fresh item of difficulty d,
  else fresh ones up to `listSpread` levels away (`kornikSpread` for Kórnik), else a repeat within one level.
- Answers are stored with the right one first; the game shuffles them when asking.
