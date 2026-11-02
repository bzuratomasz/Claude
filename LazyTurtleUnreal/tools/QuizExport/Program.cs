using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace LazyTurtle
{
    /// <summary>
    /// Exports the original C# question generators, chat and UI texts to JSON for the Unreal port
    /// (Content/Data/quiz.json and Content/Data/strings.json). Usage: dotnet run [-- outputDir]
    /// </summary>
    static class Program
    {
        /// <summary>Questions kept per subject and difficulty (generated subjects never run out).</summary>
        const int PerBucket = 150;
        /// <summary>Give up on a bucket after this many generated questions in a row that were all repeats.</summary>
        const int MaxMisses = 1500;
        const int Seed = 20261029;

        static readonly JsonSerializerOptions Json = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        static int Main(string[] args)
        {
            string outDir = args.Length > 0 ? args[0] : FindDefaultOutput();
            Directory.CreateDirectory(outDir);
            var utf8 = new UTF8Encoding(false);

            var quiz = new StringBuilder();
            // listSubjects / listSpread / kornikSpread document how the port must search the list buckets (FromList).
            quiz.Append("{\n\"version\": 1,\n\"listSubjects\": [").Append(string.Join(", ", Questions.ListSubjects))
                .Append("],\n\"listSpread\": ").Append(Questions.ListSpread).Append(",\n\"kornikSpread\": ").Append(Questions.MaxDifficulty);
            foreach (var (code, pl) in new[] { ("pl", true), ("en", false) })
            {
                L.Pl = pl;
                quiz.Append(",\n\"").Append(code).Append("\": {\n");
                WriteLanguage(quiz);
                quiz.Append("}");
            }
            quiz.Append("\n}\n");
            File.WriteAllText(Path.Combine(outDir, "quiz.json"), quiz.ToString(), utf8);

            File.WriteAllText(Path.Combine(outDir, "strings.json"), BuildStrings(), utf8);
            L.Pl = true;

            // Sanity check: both files must parse.
            foreach (var f in new[] { "quiz.json", "strings.json" })
            {
                var path = Path.Combine(outDir, f);
                using (JsonDocument.Parse(File.ReadAllText(path, utf8))) { }
                Console.WriteLine($"{path}: {new FileInfo(path).Length / 1024} KB");
            }
            return 0;
        }

        static string FindDefaultOutput()
        {
            // Walk up from the working directory / binary to the repo root (the folder with Content/ or *.uproject).
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
                    if (d.GetFiles("*.uproject").Length > 0 || Directory.Exists(Path.Combine(d.FullName, "Content")))
                        return Path.Combine(d.FullName, "Content", "Data");
            }
            throw new InvalidOperationException("Repo root not found; pass the output directory as an argument.");
        }

        static string S(string s) => JsonSerializer.Serialize(s, Json);

        /// <summary>{"q": text, "a": [correct answer first, then the wrong ones]}</summary>
        static string Q(Question q)
        {
            var answers = new[] { q.CorrectText }.Concat(q.Answers.Where((_, i) => i != q.Correct));
            return "{\"q\":" + S(q.Text) + ",\"a\":[" + string.Join(",", answers.Select(S)) + "]}";
        }

        /// <summary>A JSON array of buckets (one array of questions per difficulty 1-9), one question per line.</summary>
        static string Buckets(IEnumerable<List<Question>> buckets, string indent) =>
            "[\n" + string.Join(",\n", buckets.Select(b => b.Count == 0 ? indent + " []"
                : indent + " [\n" + string.Join(",\n", b.Select(q => indent + "  " + Q(q))) + "\n" + indent + " ]"))
            + "\n" + indent + "]";

        static void WriteLanguage(StringBuilder sb)
        {
            var rng = new Random(Seed + (L.Pl ? 0 : 1));
            var levels = Enumerable.Range(1, Questions.MaxDifficulty);
            string lang = L.Pl ? "pl" : "en";
            int total = 0;

            sb.Append(" \"subjects\": [\n");
            for (int subject = 0; subject < 10; subject++)
            {
                bool list = Questions.ListSubjects.Contains(subject);
                var buckets = levels.Select(d => list ? Questions.ListItems(subject, d) : Questions.Collect(subject, d, rng, PerBucket, MaxMisses)).ToList();
                total += buckets.Sum(b => b.Count);
                Console.WriteLine($"[{lang}] subject {subject}{(list ? " (list)" : "")}: " + string.Join(" ", buckets.Select(b => b.Count)));
                sb.Append("  ").Append(Buckets(buckets, "  ")).Append(subject < 9 ? ",\n" : "\n");
            }
            sb.Append(" ],\n");

            var kornik = levels.Select(Questions.KornikItems).ToList();
            Console.WriteLine($"[{lang}] kornik: " + string.Join(" ", kornik.Select(b => b.Count)) + $", total questions {total}");
            sb.Append(" \"kornik\": ").Append(Buckets(kornik, " ")).Append(",\n");

            sb.Append(" \"riddles\": [\n").Append(string.Join(",\n", Chat.RiddleData().Select(d =>
                "  {\"q\":" + S(d[0]) + ",\"a\":[" + S(d[1]) + "," + S(d[2]) + "," + S(d[3]) + "],\"c\":0}"))).Append("\n ],\n");
            sb.Append(" \"kids\": [").Append(string.Join(", ", Chat.Names().Select(S))).Append("],\n");
            sb.Append(" \"school\": [\n").Append(string.Join(",\n", Chat.Lines(false).Select(s => "  " + S(s)))).Append("\n ],\n");
            sb.Append(" \"yard\": [\n").Append(string.Join(",\n", Chat.Lines(true).Select(s => "  " + S(s)))).Append("\n ]\n");
        }

        static string BuildStrings()
        {
            var keys = new List<(string, string, string)>();
            foreach (var kv in L.Texts()) keys.Add((kv.Key, kv.Value[0], kv.Value[1]));

            (string, string, string) Both(string key, Func<string> text)
            {
                L.Pl = true; var pl = text();
                L.Pl = false; var en = text();
                L.Pl = true;
                return (key, pl, en);
            }

            keys.Add(("yard_soon", "Boisko jest jeszcze w budowie - wkrótce zagramy w piłkę!", "The playground is still being built - football is coming soon!"));
            keys.Add(Both("chat_greeting", () => Chat.Greeting("{0}")));
            keys.Add(Both("chat_riddle_intro", () => Chat.RiddleIntro));
            keys.Add(Both("chat_riddle_right", () => Chat.RiddleRight));
            keys.Add(Both("chat_riddle_wrong", () => Chat.RiddleWrong("{0}")));
            keys.Add(Both("chat_yes", () => Chat.Yes));
            keys.Add(Both("chat_no", () => Chat.No));
            keys.Add(Both("match_won", () => Chat.MatchWon));
            keys.Add(Both("match_lost", () => Chat.MatchLost));
            keys.Add(Both("match_draw", () => Chat.MatchDraw));
            keys.Add(Both("football_offer", () => Chat.FootballOffer("{0}", "{1}", "{2}")));

            return "{\n" + string.Join(",\n", keys.Select(k => S(k.Item1) + ": [" + S(k.Item2) + ", " + S(k.Item3) + "]")) + "\n}\n";
        }
    }

    // ---- exporter access to the original classes (kept unchanged apart from the 'partial' keyword)

    static partial class Questions
    {
        /// <summary>Subjects served purely by FromList from a hand-written list (Generate's default branch).</summary>
        internal static readonly int[] ListSubjects = { 2, 3, 7, 8, 9 };
        /// <summary>FromList's default spread: how many levels away it looks once a difficulty runs out.</summary>
        internal const int ListSpread = 2;

        /// <summary>
        /// Generated subjects: what the game would serve for this subject and difficulty during one long session.
        /// Generates questions the way Make does (marking each as asked, so list-based parts of the generators move on
        /// to nearby difficulties like FromList does) and keeps the unique ones.
        /// </summary>
        internal static List<Question> Collect(int subject, int d, Random r, int max, int maxMisses)
        {
            asked.Clear();
            var result = new List<Question>();
            for (int misses = 0; result.Count < max && misses < maxMisses;)
            {
                var q = Generate(subject, Clamp(d), r);
                if (asked.Add(Key(q)))
                {
                    result.Add(q);
                    misses = 0;
                }
                else misses++;
            }
            asked.Clear();
            return result;
        }

        static Question FromItem(Item i) => new Question { Text = Pl ? i.Pl : i.En, Answers = Pl ? i.APl : i.AEn, Correct = 0 };

        /// <summary>
        /// List subjects: the items of exactly difficulty d (correct answer first). The port reproduces FromList at run
        /// time by widening the search to d +- 1, d +- 2 when a difficulty has no fresh items left.
        /// </summary>
        internal static List<Question> ListItems(int subject, int d) => Lists[subject].Where(i => i.D == d).Select(FromItem).ToList();

        /// <summary>The Kórnik items of exactly difficulty d (the Master Turtle searches them with spread MaxDifficulty).</summary>
        internal static List<Question> KornikItems(int d) => Kornik.Where(i => i.D == d).Select(FromItem).ToList();
    }

    static partial class Chat
    {
        internal static string[] Names() => L.Pl ? NamesPl : NamesEn;
        internal static string[][] RiddleData() => L.Pl ? RiddlesPl : RiddlesEn;
        /// <summary>Chat lines in the current language, with the {c} (captain name) placeholder kept.</summary>
        internal static IEnumerable<string> Lines(bool yard) => (yard ? YardLines : SchoolLines).Select(l => l[L.Pl ? 0 : 1]);
    }

    static partial class L
    {
        internal static IEnumerable<KeyValuePair<string, string[]>> Texts() => texts;
    }
}
