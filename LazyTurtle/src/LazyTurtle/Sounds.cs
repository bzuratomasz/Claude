using System;
using System.Collections.Generic;
using System.IO;
using System.Media;

namespace LazyTurtle
{
    /// <summary>
    /// Cheerful sound effects synthesized at start-up into in-memory WAV files
    /// (no asset files, works with the plain Windows sound API on Windows 7 and 11).
    /// Every effect is a little tune of overlapping notes, normalized to a moderate volume.
    /// </summary>
    sealed class Sounds : IDisposable
    {
        const int Rate = 22050;
        enum Wave { Sine, Square, Triangle, Bell }

        // Note frequencies (Hz) used by the tunes below.
        const double G3 = 196, C4 = 262, A4 = 440, C5 = 523, E5 = 659, G5 = 784,
            C6 = 1047, D6 = 1175, E6 = 1319, G6 = 1568, C7 = 2093, E7 = 2637, G7 = 3136, A7 = 3520;

        readonly Dictionary<string, SoundPlayer> players = new Dictionary<string, SoundPlayer>();
        public bool Enabled = true;

        public Sounds()
        {
            try
            {
                Add("coin", new Tune().Seq(0, Wave.Square, 1, true, 988, 0.07, E6, 0.3).Render(0.3));
                Add("jump", new Tune().Slide(0, 280, 720, 0.14, Wave.Triangle, 1, 0).Render(0.32));

                // bouncy "da-da-di-DING!" ending on a bright ringing chord with a twinkle on top
                Add("correct", new Tune()
                    .Seq(0, Wave.Triangle, 1, true, G5, 0.075, C6, 0.075, E6, 0.075)
                    .Chord(0.225, Wave.Bell, 0.6, 0.55, G6, C7)
                    .Note(0.3, E7, 0.2, Wave.Sine, 0.25, true)
                    .Render(0.42));

                // gentle "oh-oh": two soft, wobbly sine notes in the middle range, nothing low or buzzy
                Add("wrong", new Tune()
                    .Note(0, E5, 0.16, Wave.Sine, 1, false)
                    .Slide(0.17, C5, A4, 0.32, Wave.Sine, 1, 0.02)
                    .Render(0.3));

                // sparkly gift: a quick ringing arpeggio, a bell chord and little twinkles
                Add("gift", new Tune()
                    .Seq(0, Wave.Bell, 0.6, true, C6, 0.045, E6, 0.045, G6, 0.045, C7, 0.045, E7, 0.045, G7, 0.3)
                    .Chord(0.27, Wave.Bell, 0.4, 0.6, C6, G6, C7)
                    .Seq(0.38, Wave.Sine, 0.3, true, A7, 0.08, E7, 0.08, G7, 0.09, A7, 0.14)
                    .Render(0.42));

                Add("talk", new Tune().Seq(0, Wave.Triangle, 1, true, 660, 0.045, 880, 0.07).Render(0.28));

                // magic door: a rising chime
                Add("door", new Tune().Seq(0, Wave.Bell, 1, true, G5, 0.1, C6, 0.1, E6, 0.1, G6, 0.55).Render(0.38));

                // stairs open: a running scale and a small fanfare chord
                Add("stairs", new Tune()
                    .Seq(0, Wave.Triangle, 1, true, C5, 0.07, E5, 0.07, G5, 0.07, C6, 0.07, E6, 0.07)
                    .Chord(0.35, Wave.Bell, 0.5, 0.65, C6, E6, G6, C7)
                    .Note(0.35, C4, 0.5, Wave.Triangle, 0.5, true)
                    .Render(0.44));

                // floor done / match won: "ta-ta-ta-TAA ta-TAAA" fanfare with a bass line and a sparkle at the end
                Add("win", new Tune()
                    .Seq(0, Wave.Square, 0.55, false, C5, 0.12, E5, 0.12, G5, 0.12, C6, 0.26, G5, 0.12, C6, 0.6)
                    .Note(0, C4, 0.36, Wave.Triangle, 0.6, true)
                    .Note(0.36, G3, 0.38, Wave.Triangle, 0.6, true)
                    .Note(0.74, C4, 0.6, Wave.Triangle, 0.6, true)
                    .Chord(0.74, Wave.Bell, 0.3, 0.7, E6, G6, C7)
                    .Seq(0.98, Wave.Sine, 0.25, true, E7, 0.07, G7, 0.07, C7 * 2, 0.2)
                    .Render(0.45));

                // soft cartoon "boing" when walking into something (or choosing a locked floor)
                Add("bump", new Tune().Slide(0, 330, 190, 0.16, Wave.Triangle, 1, 0.06).Render(0.3));
                Add("click", new Tune().Note(0, C6, 0.04, Wave.Triangle, 1, true).Render(0.22));
                Add("select", new Tune().Seq(0, Wave.Triangle, 1, true, G5, 0.05, D6, 0.1).Render(0.28));
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
                // a soft "chiptune" square: only the first odd harmonics, so it never sounds harsh
                case Wave.Square: return 0.8 * (s + Math.Sin(3 * phase) / 3 + Math.Sin(5 * phase) / 5);
                case Wave.Triangle: return 2 / Math.PI * Math.Asin(s);
                case Wave.Bell: return 0.7 * s + 0.2 * Math.Sin(2 * phase) + 0.1 * Math.Sin(3 * phase);
                default: return s;
            }
        }

        /// <summary>
        /// A tiny mixer: notes are added at a start time (seconds) and may overlap (chords, ringing
        /// bells). Render() scales the mix to the given peak, so nothing ever clips.
        /// </summary>
        sealed class Tune
        {
            readonly List<double> buf = new List<double>();

            /// <summary>One note. A ringing note fades out like a bell; otherwise it holds and then releases.</summary>
            public Tune Note(double at, double freq, double len, Wave w, double vol, bool ring)
            {
                int start = (int)(at * Rate), n = (int)(len * Rate);
                Grow(start + n);
                double phase = 0;
                for (int k = 0; k < n; k++)
                {
                    double t = k / (double)n;
                    double env = Math.Min(1, k / (0.006 * Rate)) * Math.Min(1, (n - k) / (0.02 * Rate));
                    env *= ring ? Math.Exp(-4 * t) : (t < 0.7 ? 1 : (1 - t) / 0.3);
                    phase += 2 * Math.PI * freq / Rate;
                    buf[start + k] += Osc(w, phase) * env * vol;
                }
                return this;
            }

            /// <summary>A melody given as pairs: frequency (Hz), duration (s), starting at the given time.</summary>
            public Tune Seq(double at, Wave w, double vol, bool ring, params double[] fd)
            {
                for (int i = 0; i + 1 < fd.Length; i += 2)
                {
                    // ringing notes sound a little longer than their step, so the melody flows
                    Note(at, fd[i], ring ? fd[i + 1] * 1.6 : fd[i + 1], w, vol, ring);
                    at += fd[i + 1];
                }
                return this;
            }

            public Tune Chord(double at, Wave w, double vol, double len, params double[] freqs)
            {
                foreach (var f in freqs) Note(at, f, len, w, vol, true);
                return this;
            }

            /// <summary>A pitch glide from f0 to f1; wobble adds a playful vibrato.</summary>
            public Tune Slide(double at, double f0, double f1, double len, Wave w, double vol, double wobble)
            {
                int start = (int)(at * Rate), n = (int)(len * Rate);
                Grow(start + n);
                double phase = 0;
                for (int k = 0; k < n; k++)
                {
                    double t = k / (double)n;
                    double f = (f0 + (f1 - f0) * t) * (1 + wobble * Math.Sin(2 * Math.PI * 14 * k / Rate));
                    phase += 2 * Math.PI * f / Rate;
                    double env = Math.Min(1, k / (0.006 * Rate)) * (1 - t);
                    buf[start + k] += Osc(w, phase) * env * vol;
                }
                return this;
            }

            /// <summary>Converts the mix to 16-bit samples with the loudest point at peak (0..1) of full scale.</summary>
            public short[] Render(double peak)
            {
                double max = 1e-9;
                foreach (var v in buf) max = Math.Max(max, Math.Abs(v));
                double k = peak * 32767 / max;
                var s = new short[buf.Count];
                for (int i = 0; i < s.Length; i++) s[i] = (short)(buf[i] * k);
                return s;
            }

            void Grow(int n)
            {
                while (buf.Count < n) buf.Add(0);
            }
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
