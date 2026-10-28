using System;
using System.Collections.Generic;
using System.IO;

namespace LazyTurtle
{
    /// <summary>Progress stored in %APPDATA%\LazyTurtle\save.txt (plain key=value lines).</summary>
    sealed class SaveData
    {
        public int Unlocked = 1;                                // floors available
        public int[] Classes = new int[School.Floors];         // bit mask of classrooms with the gift collected
        public bool[] Master = new bool[School.Floors];
        public int[] Stars = new int[School.Floors];
        public int Coins;
        public string Lang;
        public bool Sound = true, Speech = true;

        public static string Folder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LazyTurtle");

        static string FilePath => Path.Combine(Folder, "save.txt");

        public static SaveData Load()
        {
            var s = new SaveData();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim(), val = line.Substring(eq + 1).Trim();
                    int n, f;
                    int.TryParse(val, out n);
                    if (key == "unlocked") s.Unlocked = Math.Max(1, Math.Min(School.Floors, n));
                    else if (key == "coins") s.Coins = Math.Max(0, n);
                    else if (key == "lang") s.Lang = val;
                    else if (key == "sound") s.Sound = val != "0";
                    else if (key == "speech") s.Speech = val != "0";
                    else if (key.Length > 1 && int.TryParse(key.Substring(key.Length - 1), out f) && f >= 0 && f < School.Floors)
                    {
                        string k = key.Substring(0, key.Length - 1);
                        if (k == "classes") s.Classes[f] = n & 0x3FF;
                        else if (k == "master") s.Master[f] = n != 0;
                        else if (k == "stars") s.Stars[f] = Math.Max(0, Math.Min(3, n));
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return s;
        }

        public void Write()
        {
            try
            {
                var lines = new List<string>
                {
                    "unlocked=" + Unlocked,
                    "coins=" + Coins,
                    "lang=" + (Lang ?? ""),
                    "sound=" + (Sound ? "1" : "0"),
                    "speech=" + (Speech ? "1" : "0"),
                };
                for (int i = 0; i < School.Floors; i++)
                {
                    lines.Add("classes" + i + "=" + Classes[i]);
                    lines.Add("master" + i + "=" + (Master[i] ? 1 : 0));
                    lines.Add("stars" + i + "=" + Stars[i]);
                }
                Directory.CreateDirectory(Folder);
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
