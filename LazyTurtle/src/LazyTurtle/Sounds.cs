using System;
using System.Collections.Generic;
using System.IO;
using System.Media;

namespace LazyTurtle
{
    /// <summary>
    /// Retro sound effects synthesized at start-up into in-memory WAV files
    /// (no asset files, works with the plain Windows sound API on Windows 7 and 11).
    /// </summary>
    sealed class Sounds : IDisposable
    {
        const int Rate = 22050;
        enum Wave { Sine, Square, Triangle }

        readonly Dictionary<string, SoundPlayer> players = new Dictionary<string, SoundPlayer>();
        public bool Enabled = true;

        public Sounds()
        {
            try
            {
                Add("coin", Notes(Wave.Square, 0.16, 988, 0.07, 1319, 0.28));
                Add("jump", Sweep(Wave.Square, 0.12, 260, 640, 0.2, false));
                Add("correct", Notes(Wave.Triangle, 0.45, 784, 0.09, 988, 0.09, 1175, 0.09, 1568, 0.3));
                Add("wrong", Notes(Wave.Sine, 0.4, 392, 0.18, 330, 0.32));
                Add("gift", Notes(Wave.Square, 0.13, 1047, 0.07, 1319, 0.07, 1568, 0.07, 2093, 0.07, 1568, 0.07, 2093, 0.07, 2637, 0.35));
                Add("talk", Notes(Wave.Triangle, 0.3, 660, 0.04, 880, 0.05));
                Add("door", Notes(Wave.Sine, 0.45, 784, 0.12, 1047, 0.12, 1319, 0.12, 1568, 0.45));
                Add("stairs", Notes(Wave.Triangle, 0.4, 523, 0.08, 659, 0.08, 784, 0.08, 1047, 0.08, 1319, 0.08, 1568, 0.3));
                Add("win", Notes(Wave.Square, 0.14, 523, 0.13, 659, 0.13, 784, 0.13, 1047, 0.3, 784, 0.13, 1047, 0.6));
                Add("bump", Sweep(Wave.Triangle, 0.35, 220, 140, 0.1, false));
                Add("click", Notes(Wave.Triangle, 0.3, 880, 0.035));
                Add("select", Notes(Wave.Triangle, 0.35, 660, 0.05, 990, 0.09));
            }
            catch (Exception)
            {
                // No sound device or WAV playback failed: the game simply stays silent.
                players.Clear();
            }
        }

        public void Play(string name)
        {
            SoundPlayer p;
            if (!Enabled || !players.TryGetValue(name, out p)) return;
            try { p.Play(); }
            catch (Exception) { }
        }

        public void Dispose()
        {
            foreach (var p in players.Values) p.Dispose();
            players.Clear();
        }

        void Add(string name, short[] samples)
        {
            var player = new SoundPlayer(new MemoryStream(ToWav(samples)));
            player.Load();
            players[name] = player;
        }

        static double Osc(Wave w, double phase)
        {
            double s = Math.Sin(phase);
            switch (w)
            {
                case Wave.Square: return (s >= 0 ? 0.7 : -0.7) + s * 0.3;
                case Wave.Triangle: return 2 / Math.PI * Math.Asin(s);
                default: return s;
            }
        }

        /// <summary>Plays a melody given as pairs: frequency (Hz), duration (s).</summary>
        static short[] Notes(Wave w, double vol, params double[] fd)
        {
            var list = new List<short>();
            for (int i = 0; i + 1 < fd.Length; i += 2)
            {
                double f = fd[i];
                int n = (int)(fd[i + 1] * Rate);
                double phase = 0;
                for (int k = 0; k < n; k++)
                {
                    double t = k / (double)n;
                    double env = Math.Min(1, k / (0.005 * Rate)) * (t < 0.6 ? 1 : (1 - t) / 0.4);
                    phase += 2 * Math.PI * f / Rate;
                    list.Add((short)(Osc(w, phase) * env * vol * 32000));
                }
            }
            return list.ToArray();
        }

        static short[] Sweep(Wave w, double vol, double f0, double f1, double dur, bool vibrato)
        {
            int n = (int)(dur * Rate);
            var s = new short[n];
            double phase = 0;
            for (int k = 0; k < n; k++)
            {
                double t = k / (double)n;
                double f = f0 + (f1 - f0) * t;
                if (vibrato) f *= 1 + 0.08 * Math.Sin(t * 60);
                phase += 2 * Math.PI * f / Rate;
                double env = Math.Min(1, k / (0.004 * Rate)) * (1 - t);
                s[k] = (short)(Osc(w, phase) * env * vol * 32000);
            }
            return s;
        }

        static short[] Concat(short[] a, short[] b)
        {
            var r = new short[a.Length + b.Length];
            a.CopyTo(r, 0);
            b.CopyTo(r, a.Length);
            return r;
        }

        static byte[] ToWav(short[] samples)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                int dataBytes = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);          // PCM
                w.Write((short)1);          // mono
                w.Write(Rate);
                w.Write(Rate * 2);          // byte rate
                w.Write((short)2);          // block align
                w.Write((short)16);         // bits per sample
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);
                foreach (var s in samples) w.Write(s);
                w.Flush();
                return ms.ToArray();
            }
        }
    }
}
