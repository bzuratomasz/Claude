using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace LazyTurtle
{
    /// <summary>
    /// Subject graphics for 8–11-year-olds: educational wall posters (10 subjects x 3 variants) with
    /// captions, dedicated portraits of the old turtle teachers and floor-dependent chalk blackboards.
    /// </summary>
    sealed partial class Art
    {
        // ------------------------------------------------------------------ small subject helpers

        static string SL(string pl, string en) => L.Pl ? pl : en;

        static readonly StringFormat SFmt = SMakeFmt(true), SFmtL = SMakeFmt(false);

        static StringFormat SMakeFmt(bool center)
        {
            var f = (StringFormat)StringFormat.GenericTypographic.Clone();
            f.Alignment = center ? StringAlignment.Center : StringAlignment.Near;
            f.LineAlignment = center ? StringAlignment.Center : StringAlignment.Near;
            f.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
            return f;
        }

        /// <summary>Tight (typographic) centered text that shrinks until it fits the box; good for tiny labels.</summary>
        static void ST(Graphics g, string s, RectangleF box, float size, Color color, bool bold = true)
        {
            if (s.IndexOf('\n') >= 0)
            {
                var ls = s.Split('\n');
                float lh = box.Height / ls.Length, fit = size;
                foreach (var line in ls) fit = Math.Min(fit, SFitSize(g, line, new SizeF(box.Width, lh), size, bold));
                for (int i = 0; i < ls.Length; i++) ST(g, ls[i], new RectangleF(box.X, box.Y + i * lh, box.Width, lh), fit, color, bold);
                return;
            }
            var style = bold ? FontStyle.Bold : FontStyle.Regular;
            Font f = null;
            try
            {
                for (; ; size -= 0.5f)
                {
                    f?.Dispose();
                    f = new Font(Family, size, style, GraphicsUnit.Pixel);
                    if (size <= 4.5f) break;
                    var m = g.MeasureString(s, f, new SizeF(10000, 10000), SFmt);
                    if (m.Width <= box.Width && m.Height <= box.Height * 1.2f) break;
                }
                using (var b = B(color)) g.DrawString(s, f, b, box, SFmt);
            }
            finally { f?.Dispose(); }
        }

        static float SFitSize(Graphics g, string s, SizeF box, float size, bool bold)
        {
            for (; size > 4.5f; size -= 0.5f)
                using (var f = new Font(Family, size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    var m = g.MeasureString(s, f, new SizeF(10000, 10000), SFmt);
                    if (m.Width <= box.Width && m.Height <= box.Height * 1.2f) break;
                }
            return size;
        }

        static void ST(Graphics g, string s, float x, float y, float w, float h, float size, Color color, bool bold = true)
            => ST(g, s, new RectangleF(x, y, w, h), size, color, bold);

        /// <summary>Text with a light halo so it stays readable on top of pictures.</summary>
        static void STO(Graphics g, string s, RectangleF box, float size, Color color, Color halo)
        {
            for (int i = 0; i < 4; i++)
                ST(g, s, new RectangleF(box.X + (i == 0 ? -0.8f : i == 1 ? 0.8f : 0), box.Y + (i == 2 ? -0.8f : i == 3 ? 0.8f : 0), box.Width, box.Height), size, halo);
            ST(g, s, box, size, color);
        }

        /// <summary>One line made of differently coloured pieces (e.g. a word with the tricky letters in red).</summary>
        static void SRuns(Graphics g, string[] parts, Color[] cols, RectangleF box, float size, bool bold = true)
        {
            var style = bold ? FontStyle.Bold : FontStyle.Regular;
            Font f = null;
            try
            {
                float[] w = new float[parts.Length];
                float total = 0, h = 0;
                for (; ; size -= 0.5f)
                {
                    f?.Dispose();
                    f = new Font(Family, size, style, GraphicsUnit.Pixel);
                    total = 0;
                    for (int i = 0; i < parts.Length; i++)
                    {
                        w[i] = parts[i].Length == 0 ? 0 : g.MeasureString(parts[i], f, new PointF(0, 0), SFmtL).Width;
                        total += w[i];
                    }
                    h = g.MeasureString("Ag", f, new PointF(0, 0), SFmtL).Height;
                    if (size <= 4.5f || (total <= box.Width && h <= box.Height * 1.2f)) break;
                }
                float x = box.X + (box.Width - total) / 2, y = box.Y + (box.Height - h) / 2;
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i].Length > 0)
                        using (var b = B(cols[i % cols.Length])) g.DrawString(parts[i], f, b, x, y, SFmtL);
                    x += w[i];
                }
            }
            finally { f?.Dispose(); }
        }

        static void SArrow(Graphics g, Color c, float w, float x1, float y1, float x2, float y2)
        {
            using (var cap = new AdjustableArrowCap(3, 3))
            using (var p = new Pen(c, w))
            {
                p.StartCap = LineCap.Round;
                p.CustomEndCap = cap;
                g.DrawLine(p, x1, y1, x2, y2);
            }
        }

        static void SFill(Graphics g, Color c, params PointF[] pts)
        {
            using (var b = B(c)) g.FillPolygon(b, pts);
        }

        static void SDisc(Graphics g, Color c, float cx, float cy, float r)
        {
            using (var b = B(c)) g.FillEllipse(b, cx - r, cy - r, 2 * r, 2 * r);
        }

        static void SLine(Graphics g, Color c, float w, float x1, float y1, float x2, float y2)
        {
            using (var p = P(c, w)) g.DrawLine(p, x1, y1, x2, y2);
        }

        static PointF SP(float x, float y) => new PointF(x, y);

        static readonly Color SInk = C(40, 45, 70);
        static readonly Color[] SRainbow = { C(230, 60, 55), C(245, 145, 35), C(225, 185, 20), C(70, 170, 70), C(40, 160, 170), C(60, 110, 220), C(150, 80, 200), C(230, 90, 160) };

        // ------------------------------------------------------------------ posters

        public const int SubjectPosterVariants = 3;

        static string SPosterTitle(int s, int v)
        {
            switch (s * 3 + v)
            {
                case 0: return SL("Tabliczka mnożenia", "Times table");
                case 1: return SL("Figury", "Shapes");
                case 2: return SL("Ułamki", "Fractions");
                case 3: return SL("Alfabet", "Alphabet");
                case 4: return SL("Części mowy", "Parts of speech");
                case 5: return SL("Ortografia", "Spelling");
                case 6: return SL("Łańcuch pokarmowy", "Food chain");
                case 7: return SL("Budowa rośliny", "Parts of a plant");
                case 8: return SL("Rozwój motyla", "Butterfly life cycle");
                case 9: return SL("Mapa Polski", "Map of Poland");
                case 10: return SL("Historia Polski", "Polish history");
                case 11: return SL("Kontynenty", "Continents");
                case 12: return SL("Hello! Cześć!", "Cześć! Hello!");
                case 13: return SL("Colours & numbers", "Kolory i liczby");
                case 14: return SL("London", "Londyn");
                case 15: return SL("Szachy", "Chess");
                case 16: return SL("Co dalej?", "What comes next?");
                case 17: return "Tangram";
                case 18: return SL("Która godzina?", "What time is it?");
                case 19: return SL("Pory roku", "Seasons & months");
                case 20: return SL("Pory dnia", "Times of day");
                case 21: return SL("Nuty", "Musical notes");
                case 22: return SL("Koło barw", "Colour wheel");
                case 23: return SL("Instrumenty", "Instruments");
                case 24: return SL("Zdrowy talerz", "Healthy plate");
                case 25: return SL("Sport", "Sports");
                case 26: return SL("Ruch i sen", "Move & sleep");
                case 27: return SL("Układ Słoneczny", "Solar System");
                case 28: return SL("Obieg wody", "Water cycle");
                default: return SL("Magnes i prąd", "Magnets & circuits");
            }
        }

        /// <summary>
        /// A framed educational poster on the painted wall for subject 0..9 and variant 0..2
        /// (see SubjectPosterCaption for what each one teaches).
        /// </summary>
        public static Tex SubjectPoster(FloorTheme th, int subject, int variant)
        {
            int s = ((subject % 10) + 10) % 10, v = ((variant % 3) + 3) % 3;
            var col = School.Subjects[s].Color;
            return Wall(th, 90 + s * 3 + v, g =>
            {
                var frame = new RectangleF(12, 8, 104, 106);
                g.FillRectangle(B(CA(80, 0, 0, 0)), frame.X + 3, frame.Y + 3, frame.Width, frame.Height);
                Grad(g, frame, Shade(col, 45), Shade(col, -45), 45f, false);
                using (var pen = P(CA(140, 255, 255, 255), 1f)) g.DrawRectangle(pen, frame.X + 1.5f, frame.Y + 1.5f, frame.Width - 3, frame.Height - 3);
                var paper = new RectangleF(16, 12, 96, 98);
                g.FillRectangle(B(C(255, 253, 245)), paper);
                var strip = new RectangleF(16, 12, 96, 16);
                Grad(g, strip, Shade(col, 25), Shade(col, -20), 90f, false);
                ST(g, SPosterTitle(s, v), strip.X + 3, strip.Y + 1, strip.Width - 6, strip.Height - 2, 12, Color.White);
                var a = new RectangleF(17, 29, 94, 80);
                var st = g.Save();
                g.SetClip(a);
                g.TranslateTransform(a.X, a.Y);
                switch (s)
                {
                    case 0: SMaths(g, v); break;
                    case 1: SLetters(g, v); break;
                    case 2: SNature(g, v); break;
                    case 3: SGeo(g, v); break;
                    case 4: SEnglish(g, v); break;
                    case 5: SLogic(g, v); break;
                    case 6: STime(g, v); break;
                    case 7: SMusic(g, v); break;
                    case 8: SHealth(g, v); break;
                    default: SScience(g, v); break;
                }
                g.Restore(st);
                using (var pen = P(Shade(col, -60), 1.2f)) g.DrawRectangle(pen, paper.X, paper.Y, paper.Width, paper.Height);
                g.FillPolygon(B(CA(30, 255, 255, 255)), new[] { SP(paper.X, paper.Y), SP(paper.X + 34, paper.Y), SP(paper.X, paper.Y + 34) });
            });
        }

        // Every picture below is painted in a local 94 x 80 box.

        static void SMaths(Graphics g, int v)
        {
            if (v == 0)
            {
                float cw = 9.4f, ch = 8f;
                for (int r = 0; r < 10; r++)
                    for (int c = 0; c < 10; c++)
                    {
                        bool head = r == 0 || c == 0;
                        var bg = head ? C(95, 145, 230) : r == c ? C(255, 210, 60) : ((r + c) % 2 == 0 ? C(232, 240, 255) : Color.White);
                        using (var b = B(bg)) g.FillRectangle(b, c * cw, r * ch, cw, ch);
                    }
                // tiny digits read better in a plain sans-serif font than in Comic Sans
                using (var small = new Font(FontFamily.GenericSansSerif, 6.2f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var tiny = new Font(FontFamily.GenericSansSerif, 5.2f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var white = B(Color.White))
                using (var ink = B(SInk))
                    for (int r = 0; r < 10; r++)
                        for (int c = 0; c < 10; c++)
                        {
                            int n = (r + 1) * (c + 1);
                            g.DrawString(n.ToString(), n >= 100 ? tiny : small, r == 0 || c == 0 ? white : ink,
                                new RectangleF(c * cw - 1, r * ch + 0.5f, cw + 2, ch), SFmt);
                        }
                using (var pen = P(C(150, 170, 215), 0.6f))
                {
                    for (int i = 0; i <= 10; i++) g.DrawLine(pen, i * cw, 0, i * cw, 80);
                    for (int i = 0; i <= 10; i++) g.DrawLine(pen, 0, i * ch, 94, i * ch);
                }
                using (var pen = P(C(230, 50, 50), 1.4f)) g.DrawEllipse(pen, 7 * cw - 1, 6 * ch - 1, cw + 2, ch + 2);
            }
            else if (v == 1)
            {
                var names = L.Pl ? new[] { "trójkąt", "kwadrat", "koło", "prostokąt" } : new[] { "triangle", "square", "circle", "rectangle" };
                using (var outl = P(SInk, 1.4f))
                {
                    var tri = new[] { SP(8, 30), SP(38, 30), SP(8, 4) };
                    SFill(g, C(240, 90, 80), tri);
                    g.DrawPolygon(outl, tri);
                    g.DrawRectangle(outl, 8, 24, 6, 6);
                    ST(g, "90°", 15, 20, 16, 8, 7, Color.White);

                    using (var b = B(C(80, 140, 235))) g.FillRectangle(b, 58, 3, 26, 26);
                    g.DrawRectangle(outl, 58, 3, 26, 26);
                    g.DrawRectangle(outl, 58, 23, 6, 6);

                    SDisc(g, C(90, 190, 90), 23.5f, 56, 13);
                    g.DrawEllipse(outl, 10.5f, 43, 26, 26);
                    SDisc(g, SInk, 23.5f, 56, 1.2f);
                    using (var dash = new Pen(SInk, 1f) { DashStyle = DashStyle.Dash }) g.DrawLine(dash, 23.5f, 56, 36.5f, 56);
                    ST(g, "r", 26, 49, 8, 7, 7, SInk);

                    using (var b = B(C(250, 170, 50))) g.FillRectangle(b, 52, 46, 36, 21);
                    g.DrawRectangle(outl, 52, 46, 36, 21);
                    g.DrawRectangle(outl, 52, 61, 6, 6);
                }
                ST(g, names[0], 0, 31, 47, 9, 8.5f, C(200, 50, 40));
                ST(g, names[1], 47, 31, 47, 9, 8.5f, C(40, 90, 200));
                ST(g, names[2], 0, 71, 47, 9, 8.5f, C(40, 140, 40));
                ST(g, names[3], 47, 71, 47, 9, 8.5f, C(210, 120, 10));
            }
            else
            {
                string[] den = { "2", "4", "8" };
                for (int i = 0; i < 3; i++)
                {
                    float cx = 16 + i * 31, cy = 22;
                    SPizza(g, cx, cy, 14, 2 << i);
                    SFrac(g, "1", den[i], cx, 40, 20, SRainbow[i == 0 ? 0 : i == 1 ? 5 : 6]);
                }
                FillRound(g, new RectangleF(4, 63, 86, 15), 6, C(255, 240, 200));
                ST(g, "1/2 = 2/4 = 4/8", 6, 64, 82, 13, 11, C(200, 70, 30));
            }
        }

        static void SPizza(Graphics g, float cx, float cy, float r, int n)
        {
            SDisc(g, C(215, 155, 80), cx, cy, r);
            SDisc(g, C(255, 210, 90), cx, cy, r - 2.5f);
            var rnd = new Random(n);
            for (int i = 0; i < 7; i++)
            {
                double a = rnd.NextDouble() * Math.PI * 2;
                float d = (float)(0.25 + rnd.NextDouble() * 0.5) * (r - 3);
                SDisc(g, C(200, 50, 40), cx + (float)Math.Cos(a) * d, cy + (float)Math.Sin(a) * d, 2f);
            }
            using (var b = B(CA(110, 255, 60, 40))) g.FillPie(b, cx - r, cy - r, 2 * r, 2 * r, -90, 360f / n);
            using (var pen = P(C(150, 90, 40), 1.1f))
                for (int k = 0; k < n; k++)
                {
                    double a = -Math.PI / 2 + k * Math.PI * 2 / n;
                    g.DrawLine(pen, cx, cy, cx + (float)Math.Cos(a) * r, cy + (float)Math.Sin(a) * r);
                }
            using (var pen = P(C(210, 30, 30), 1.6f)) g.DrawPie(pen, cx - r, cy - r, 2 * r, 2 * r, -90, 360f / n);
        }

        static void SFrac(Graphics g, string num, string den, float cx, float y, float h, Color c)
        {
            ST(g, num, cx - 10, y, 20, h / 2 - 1, h / 2, c);
            SLine(g, c, 1.6f, cx - 6, y + h / 2, cx + 6, y + h / 2);
            ST(g, den, cx - 10, y + h / 2 + 1, 20, h / 2 - 1, h / 2, c);
        }

        static void SLetters(Graphics g, int v)
        {
            if (v == 0)
            {
                string[] letters = L.Pl
                    ? "A Ą B C Ć D E Ę F G H I J K L Ł M N Ń O Ó P R S Ś T U W Y Z Ź Ż".Split(' ')
                    : "A B C D E F G H I J K L M N O P Q R S T U V W X Y Z".Split(' ');
                int cols = L.Pl ? 8 : 7;
                float cw = 94f / cols, ch = 20;
                for (int i = 0; i < letters.Length; i++)
                {
                    int r = i / cols, c = i % cols;
                    int inRow = Math.Min(cols, letters.Length - r * cols);
                    float x = c * cw + (cols - inRow) * cw / 2, y = r * ch;
                    bool special = L.Pl && "ĄĆĘŁŃÓŚŹŻ".IndexOf(letters[i], StringComparison.Ordinal) >= 0;
                    if (special) FillRound(g, new RectangleF(x + 0.8f, y + 1.5f, cw - 1.6f, ch - 3), 4, C(255, 225, 120));
                    ST(g, letters[i], x, y + 1, cw, ch - 1, 15, SRainbow[i % SRainbow.Length]);
                }
            }
            else if (v == 1)
            {
                string[] word = L.Pl ? new[] { "rzeczownik", "czasownik", "przymiotnik" } : new[] { "noun", "verb", "adjective" };
                string[] ex = L.Pl ? new[] { "kto? co?  kot, dom", "co robi?  biega, czyta", "jaki?  wesoły, duży" }
                                   : new[] { "a thing: cat, house", "an action: run, read", "describes: happy, big" };
                Color[] cols = { C(60, 110, 220), C(220, 70, 60), C(60, 160, 70) };
                for (int i = 0; i < 3; i++)
                {
                    float y = i * 26.7f;
                    FillRound(g, new RectangleF(0, y + 1, 94, 25), 6, Shade(cols[i], 95));
                    SDisc(g, cols[i], 13, y + 13.5f, 11);
                    SWordIcon(g, i, 13, y + 13.5f);
                    ST(g, word[i], 26, y + 2, 66, 13, 11, Shade(cols[i], -30));
                    ST(g, ex[i], 26, y + 15, 66, 10, 7, SInk, false);
                }
            }
            else
            {
                Color k = SInk, red = C(220, 40, 40), badgeText = Color.White;
                string[] badges;
                string[][] parts;
                if (L.Pl)
                {
                    badges = new[] { "ó/u", "rz/ż", "ch/h" };
                    parts = new[]
                    {
                        new[] { "g", "ó", "ra,  ", "u", "l" },
                        new[] { "", "rz", "eka,  ", "ż", "aba" },
                        new[] { "", "ch", "leb,  ", "h", "erbata" },
                    };
                }
                else
                {
                    badges = new[] { "ie", "cei", "-ies" };
                    parts = new[]
                    {
                        new[] { "fr", "ie", "nd,  bel", "ie", "ve" },
                        new[] { "re", "cei", "ve,  ", "cei", "ling" },
                        new[] { "bab", "ies", ",  cit", "ies", "" },
                    };
                }
                Color[] bc = { C(60, 110, 220), C(150, 80, 200), C(40, 150, 120) };
                for (int i = 0; i < 3; i++)
                {
                    float y = i * 26.7f;
                    FillRound(g, new RectangleF(0, y + 1, 94, 25), 6, C(250, 244, 225));
                    FillRound(g, new RectangleF(2, y + 3, 30, 21), 6, bc[i]);
                    ST(g, badges[i], 3, y + 4, 28, 19, 11, badgeText);
                    SRuns(g, parts[i], new[] { k, red }, new RectangleF(34, y + 4, 58, 19), 11);
                }
            }
        }

        /// <summary>White icon for a part of speech: 0 noun (apple), 1 verb (runner), 2 adjective (smiling star).</summary>
        static void SWordIcon(Graphics g, int kind, float cx, float cy)
        {
            if (kind == 0)
            {
                SDisc(g, Color.White, cx - 2.5f, cy + 1, 5.5f);
                SDisc(g, Color.White, cx + 2.5f, cy + 1, 5.5f);
                SLine(g, Color.White, 1.5f, cx, cy - 4, cx + 1, cy - 8);
                SFill(g, C(170, 230, 140), SP(cx + 1, cy - 6), SP(cx + 6, cy - 9), SP(cx + 3, cy - 4));
            }
            else if (kind == 1)
            {
                using (var p = P(Color.White, 2.2f))
                {
                    SDisc(g, Color.White, cx + 2, cy - 7, 2.6f);
                    g.DrawLine(p, cx + 1, cy - 4, cx - 1, cy + 2);
                    g.DrawLines(p, new[] { SP(cx - 6, cy - 2), SP(cx, cy - 4), SP(cx + 5, cy - 1) });
                    g.DrawLines(p, new[] { SP(cx - 1, cy + 2), SP(cx + 4, cy + 5), SP(cx + 3, cy + 9) });
                    g.DrawLines(p, new[] { SP(cx - 1, cy + 2), SP(cx - 5, cy + 6), SP(cx - 8, cy + 5) });
                }
            }
            else
            {
                using (var b = B(C(255, 220, 60))) g.FillPolygon(b, StarPoints(cx, cy + 0.5f, 9, 4));
                SDisc(g, SInk, cx - 2, cy - 0.5f, 0.9f);
                SDisc(g, SInk, cx + 2, cy - 0.5f, 0.9f);
                using (var p = P(SInk, 0.9f)) g.DrawArc(p, cx - 2.2f, cy - 1, 4.4f, 3.6f, 20, 140);
            }
        }

        static void SNature(Graphics g, int v)
        {
            if (v == 0)
            {
                Grad(g, new RectangleF(0, 0, 94, 44), C(200, 230, 255), C(240, 250, 255), 90f, false);
                using (var b = B(C(140, 200, 100))) g.FillRectangle(b, 0, 38, 94, 4);
                SDisc(g, C(255, 210, 60), 86, 7, 5);
                SGrass(g, 12, 38);
                SRabbit(g, 47, 38);
                SFox(g, 82, 38);
                var arrow = C(200, 60, 40);
                SArrow(g, arrow, 2f, 23, 26, 32, 26);
                SArrow(g, arrow, 2f, 59, 26, 68, 26);
                string[] n = L.Pl ? new[] { "trawa", "zając", "lis" } : new[] { "grass", "rabbit", "fox" };
                string[] role = L.Pl ? new[] { "roślina", "roślinożerca", "drapieżnik" } : new[] { "plant", "herbivore", "predator" };
                Color[] c = { C(50, 140, 50), C(110, 100, 90), C(210, 100, 20) };
                for (int i = 0; i < 3; i++)
                {
                    float cx = 12 + i * 35;
                    if (i == 2) cx = 81;
                    ST(g, n[i], cx - 16, 44, 32, 11, 10, c[i]);
                    ST(g, role[i], cx - 16, 55, 32, 8, 6.5f, SInk, false);
                }
                FillRound(g, new RectangleF(6, 66, 82, 12), 5, C(235, 245, 225));
                ST(g, SL("Kto kogo zjada?", "Who eats whom?"), 8, 67, 78, 10, 8, C(60, 120, 60));
            }
            else if (v == 1)
            {
                Grad(g, new RectangleF(0, 0, 94, 58), C(215, 238, 255), C(250, 252, 255), 90f, false);
                Grad(g, new RectangleF(0, 58, 94, 22), C(150, 105, 60), C(110, 75, 40), 90f, false);
                using (var p = P(C(240, 220, 180), 1.4f))
                {
                    g.DrawLines(p, new[] { SP(47, 58), SP(44, 66), SP(38, 72) });
                    g.DrawLines(p, new[] { SP(47, 58), SP(50, 67), SP(57, 74) });
                    g.DrawLine(p, 47, 58, 47, 76);
                    g.DrawLine(p, 44, 66, 44, 74);
                    g.DrawLine(p, 50, 67, 52, 72);
                }
                SLine(g, C(70, 150, 50), 3f, 47, 58, 47, 16);
                SLeaf(g, 47, 42, -1);
                SLeaf(g, 47, 32, 1);
                for (int i = 0; i < 6; i++)
                {
                    double a = i * Math.PI / 3;
                    SDisc(g, C(240, 110, 160), 47 + (float)Math.Cos(a) * 5.5f, 12 + (float)Math.Sin(a) * 5.5f, 4f);
                }
                SDisc(g, C(255, 210, 50), 47, 12, 3.2f);
                var lc = C(90, 90, 110);
                SLine(g, lc, 0.8f, 54, 10, 64, 8);
                SLine(g, lc, 0.8f, 37, 43, 28, 40);
                SLine(g, lc, 0.8f, 48, 50, 60, 50);
                SLine(g, lc, 0.8f, 44, 68, 33, 68);
                string[] n = L.Pl ? new[] { "kwiat", "liść", "łodyga", "korzeń" } : new[] { "flower", "leaf", "stem", "root" };
                ST(g, n[0], 63, 3, 31, 10, 9, C(210, 60, 130));
                ST(g, n[1], 0, 35, 29, 10, 9, C(50, 140, 50));
                ST(g, n[2], 59, 45, 35, 10, 9, C(60, 130, 40));
                ST(g, n[3], 0, 63, 33, 10, 9, C(255, 240, 200));
            }
            else
            {
                string[] n = L.Pl ? new[] { "jajo", "gąsienica", "poczwarka", "motyl" } : new[] { "egg", "caterpillar", "pupa", "butterfly" };
                // centre: the cycle arrow
                using (var cap = new AdjustableArrowCap(3, 3))
                using (var pen = new Pen(C(230, 120, 40), 2.2f) { CustomEndCap = cap })
                    g.DrawArc(pen, 36, 29, 22, 22, -50, 280);
                // egg (top)
                SFill(g, C(110, 190, 80), SP(34, 13), SP(47, 4), SP(60, 13), SP(47, 16));
                for (int i = 0; i < 3; i++) using (var b = B(C(255, 250, 215))) g.FillEllipse(b, 42 + i * 3.5f, 7 + (i % 2), 3.2f, 4.2f);
                ST(g, n[0], 30, 16, 34, 9, 8.5f, SInk);
                // caterpillar (right)
                for (int i = 0; i < 5; i++) SDisc(g, i == 4 ? C(70, 150, 50) : C(120, 200, 70), 70 + i * 4.5f, 36 + (i % 2 == 0 ? 0 : -1.2f), 3.2f);
                SDisc(g, SInk, 89.5f, 35, 0.8f);
                SLine(g, SInk, 0.7f, 88, 33, 87, 29);
                SLine(g, SInk, 0.7f, 90, 33, 92, 29);
                ST(g, n[1], 63, 42, 31, 9, 8, SInk);
                // pupa (bottom)
                SLine(g, C(120, 80, 40), 2f, 34, 56, 60, 56);
                using (var b = B(C(150, 180, 90))) g.FillEllipse(b, 43, 57, 8, 14);
                using (var p = P(C(100, 130, 60), 0.8f)) { g.DrawArc(p, 43, 60, 8, 4, 0, 180); g.DrawArc(p, 43, 64, 8, 4, 0, 180); }
                ST(g, n[2], 28, 71, 38, 9, 8.5f, SInk);
                // butterfly (left)
                SButterfly(g, 15, 35, 1f);
                ST(g, n[3], 0, 42, 31, 9, 8.5f, SInk);
            }
        }

        static void SGrass(Graphics g, float cx, float by)
        {
            using (var b = B(C(80, 170, 60)))
                for (int i = 0; i < 7; i++)
                {
                    float x = cx - 10 + i * 3.3f, hgt = 12 + (i * 7 % 5) * 2.5f;
                    g.FillPolygon(b, new[] { SP(x - 1.6f, by), SP(x + 1.6f, by), SP(x + (i % 2 == 0 ? 2 : -2), by - hgt) });
                }
            SDisc(g, C(255, 255, 255), cx + 4, by - 16, 2.2f);
            SDisc(g, C(255, 210, 50), cx + 4, by - 16, 1f);
        }

        static void SRabbit(Graphics g, float cx, float by)
        {
            var fur = C(170, 160, 150);
            SDisc(g, Color.White, cx - 10, by - 9, 3.2f);
            using (var b = B(fur))
            {
                g.FillEllipse(b, cx - 11, by - 15, 17, 14);
                g.FillEllipse(b, cx + 1, by - 22, 11, 10);
                g.FillEllipse(b, cx + 2, by - 33, 4, 14);
                g.FillEllipse(b, cx + 6.5f, by - 32, 4, 13);
                g.FillEllipse(b, cx - 9, by - 4, 10, 4);
                g.FillEllipse(b, cx + 3, by - 4, 6, 4);
            }
            using (var b = B(C(240, 170, 180))) { g.FillEllipse(b, cx + 3, by - 31, 2, 10); g.FillEllipse(b, cx + 7.5f, by - 30, 2, 9); }
            SDisc(g, SInk, cx + 8, by - 18, 1.1f);
            SDisc(g, C(230, 110, 130), cx + 11.5f, by - 15.5f, 1.1f);
        }

        static void SFox(Graphics g, float cx, float by)
        {
            var fur = C(230, 120, 40);
            using (var b = B(fur)) g.FillEllipse(b, cx + 3, by - 15, 11, 7);
            SDisc(g, Color.White, cx + 12, by - 11.5f, 2.2f);
            using (var p = P(C(90, 50, 20), 1.6f))
                foreach (var lx in new[] { -4f, -1f, 4f, 7f }) g.DrawLine(p, cx + lx, by - 6, cx + lx, by);
            using (var b = B(fur))
            {
                g.FillEllipse(b, cx - 7, by - 15, 16, 10);
                g.FillEllipse(b, cx - 12, by - 22, 10, 9);
                g.FillPolygon(b, new[] { SP(cx - 11, by - 19), SP(cx - 17, by - 15), SP(cx - 10, by - 14) });
                g.FillPolygon(b, new[] { SP(cx - 11, by - 20), SP(cx - 10, by - 26), SP(cx - 7, by - 21) });
                g.FillPolygon(b, new[] { SP(cx - 6, by - 21), SP(cx - 3, by - 26), SP(cx - 3, by - 19) });
            }
            using (var b = B(Color.White)) g.FillEllipse(b, cx - 8, by - 13, 6, 5);
            SDisc(g, SInk, cx - 8.5f, by - 19, 1f);
            SDisc(g, SInk, cx - 17, by - 15, 1f);
        }

        static void SLeaf(Graphics g, float x, float y, int dir)
        {
            var st = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(dir < 0 ? 200 : -20);
            using (var b = B(C(90, 175, 60))) g.FillEllipse(b, 0, -4, 16, 8);
            using (var p = P(C(50, 120, 40), 0.8f)) g.DrawLine(p, 1, 0, 15, 0);
            g.Restore(st);
        }

        static void SButterfly(Graphics g, float cx, float cy, float s)
        {
            using (var b = B(C(245, 140, 40)))
            {
                g.FillEllipse(b, cx - 11 * s, cy - 9 * s, 11 * s, 10 * s);
                g.FillEllipse(b, cx, cy - 9 * s, 11 * s, 10 * s);
            }
            using (var b = B(C(80, 120, 220)))
            {
                g.FillEllipse(b, cx - 8 * s, cy, 8 * s, 7 * s);
                g.FillEllipse(b, cx, cy, 8 * s, 7 * s);
            }
            SDisc(g, C(255, 240, 160), cx - 6 * s, cy - 5 * s, 1.6f * s);
            SDisc(g, C(255, 240, 160), cx + 6 * s, cy - 5 * s, 1.6f * s);
            using (var b = B(SInk)) g.FillEllipse(b, cx - 1.2f * s, cy - 8 * s, 2.4f * s, 15 * s);
            SLine(g, SInk, 0.7f, cx, cy - 8 * s, cx - 3 * s, cy - 12 * s);
            SLine(g, SInk, 0.7f, cx, cy - 8 * s, cx + 3 * s, cy - 12 * s);
        }

        static PointF[] SProject(double[] ll, Func<double, double, PointF> proj)
        {
            var pts = new PointF[ll.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = proj(ll[i * 2], ll[i * 2 + 1]);
            return pts;
        }

        static readonly double[] SPolandLL =
        {
            14.22, 53.93, 15.5, 54.17, 16.5, 54.55, 17.6, 54.77, 18.4, 54.83, 18.55, 54.6, 18.6, 54.43, 19.0, 54.35, 19.65, 54.45,
            21.0, 54.35, 22.8, 54.36, 23.5, 54.2, 23.6, 53.9, 23.9, 53.2, 23.9, 52.7, 23.2, 52.3, 23.6, 51.7, 23.6, 51.5, 24.1, 50.85,
            23.6, 50.4, 22.7, 49.6, 22.9, 49.1, 22.6, 49.08, 21.6, 49.4, 20.9, 49.3, 20.0, 49.2, 19.4, 49.6, 18.85, 49.5, 18.0, 50.0,
            17.7, 50.3, 16.9, 50.25, 16.6, 50.15, 16.2, 50.55, 16.3, 50.75, 15.0, 51.0, 14.8, 50.85, 14.95, 51.4, 14.7, 52.1,
            14.6, 52.6, 14.15, 52.85, 14.4, 53.3,
        };

        static readonly double[] SVistulaLL = { 19.0, 49.65, 19.4, 50.0, 19.94, 50.06, 21.0, 50.4, 21.75, 50.68, 21.85, 51.56, 21.01, 52.23, 19.7, 52.55, 18.6, 53.01, 18.8, 53.5, 18.95, 54.35 };

        static readonly double[][] SWorldLL =
        {
            new double[] { -165, 66, -140, 70, -95, 72, -75, 70, -60, 56, -65, 45, -80, 32, -81, 25, -97, 22, -88, 16, -80, 8, -92, 14, -106, 22, -117, 32, -125, 42, -127, 52, -142, 60, -165, 60 },
            new double[] { -55, 82, -25, 82, -20, 70, -45, 60, -55, 70 },
            new double[] { -80, 10, -60, 10, -50, 0, -35, -7, -40, -22, -55, -35, -66, -55, -73, -48, -71, -20, -81, -5 },
            new double[] { -10, 36, -9, 43, -2, 44, -5, 48, 4, 51, 8, 57, 5, 62, 15, 70, 30, 71, 40, 67, 50, 68, 60, 68, 60, 45, 50, 45, 40, 42, 28, 41, 25, 36, 15, 38, 10, 44, 3, 43, -5, 36 },
            new double[] { 60, 68, 80, 73, 110, 77, 140, 72, 180, 68, 172, 60, 160, 55, 140, 50, 130, 42, 122, 30, 120, 22, 108, 18, 105, 10, 102, 2, 98, 15, 92, 22, 80, 8, 72, 20, 60, 25, 57, 20, 45, 13, 35, 30, 36, 37, 40, 42, 50, 45, 60, 45 },
            new double[] { -17, 21, -10, 35, 10, 37, 32, 31, 35, 28, 43, 12, 51, 12, 40, -5, 40, -15, 33, -25, 20, -35, 17, -28, 12, -15, 9, 0, -8, 5, -17, 14 },
            new double[] { 113, -22, 130, -12, 142, -11, 146, -19, 153, -27, 150, -37, 140, -38, 130, -32, 115, -34 },
            new double[] { -180, -68, -120, -72, -60, -64, 0, -70, 60, -67, 120, -66, 180, -68, 180, -90, -180, -90 },
        };

        static void SGeo(Graphics g, int v)
        {
            if (v == 0)
            {
                Func<double, double, PointF> proj = (lon, lat) => new PointF(11 + (float)(lon - 14) * 7f, 12 + (float)(54.9 - lat) * 11.35f);
                Grad(g, new RectangleF(0, 0, 94, 22), C(90, 160, 230), C(150, 205, 245), 90f, false);
                ST(g, SL("Morze Bałtyckie", "Baltic Sea"), 2, 1, 56, 9, 8, C(20, 70, 150));
                var land = SProject(SPolandLL, proj);
                using (var br = new LinearGradientBrush(new RectangleF(0, 10, 94, 70), C(170, 220, 130), C(230, 215, 150), 90f)) g.FillPolygon(br, land);
                using (var p = P(C(200, 50, 50), 1.3f)) g.DrawPolygon(p, land);
                using (var p = P(C(50, 120, 220), 1.6f)) g.DrawCurve(p, SProject(SVistulaLL, proj), 0.4f);
                var vi = proj(19.3, 53.55); STO(g, SL("Wisła", "Vistula"), new RectangleF(vi.X + 1, vi.Y - 4, 24, 8), 7, C(30, 90, 200), Color.White);
                // Tatras
                var t = proj(20.0, 49.3);
                using (var b = B(C(130, 130, 140)))
                {
                    g.FillPolygon(b, new[] { SP(t.X - 7, t.Y + 2), SP(t.X - 3, t.Y - 5), SP(t.X + 1, t.Y + 2) });
                    g.FillPolygon(b, new[] { SP(t.X - 2, t.Y + 2), SP(t.X + 2, t.Y - 4), SP(t.X + 6, t.Y + 2) });
                }
                SFill(g, Color.White, SP(t.X - 4.2f, t.Y - 3), SP(t.X - 3, t.Y - 5), SP(t.X - 1.8f, t.Y - 3));
                ST(g, SL("Tatry", "Tatras"), t.X + 6, t.Y - 4, 22, 7, 6, C(90, 90, 100));
                // cities
                var w = proj(21.01, 52.23);
                using (var b = B(C(220, 40, 40))) g.FillPolygon(b, StarPoints(w.X, w.Y, 4, 1.8f));
                STO(g, SL("Warszawa", "Warsaw"), new RectangleF(w.X - 14, w.Y - 10, 30, 7), 6.5f, SInk, Color.White);
                var k = proj(19.94, 50.06);
                SDisc(g, SInk, k.X, k.Y, 1.8f);
                STO(g, "Kraków", new RectangleF(k.X - 21, k.Y - 3.5f, 20, 7), 6.5f, SInk, Color.White);
                var pz = proj(16.93, 52.41);
                SDisc(g, SInk, pz.X, pz.Y, 1.8f);
                STO(g, "Poznań", new RectangleF(pz.X - 10, pz.Y - 10, 22, 7), 6.5f, SInk, Color.White);
                var ko = proj(17.09, 52.25);
                FillRound(g, new RectangleF(ko.X - 10, ko.Y + 4, 24, 8), 3, C(255, 220, 60));
                ST(g, "Kórnik", ko.X - 10, ko.Y + 4.5f, 24, 7, 6.5f, C(200, 40, 40));
                SDisc(g, C(255, 220, 60), ko.X, ko.Y, 2.4f);
                using (var p = P(C(200, 40, 40), 1f)) g.DrawEllipse(p, ko.X - 2.4f, ko.Y - 2.4f, 4.8f, 4.8f);
            }
            else if (v == 1)
            {
                int[] years = { 966, 1025, 1410, 1791, 1918 };
                string[] ev = L.Pl
                    ? new[] { "Chrzest Polski", "Pierwszy król", "Grunwald", "Konstytucja 3 Maja", "Niepodległość" }
                    : new[] { "Baptism", "First king", "Grunwald", "Constitution", "Independence" };
                Color[] c = { C(200, 60, 50), C(210, 150, 20), C(60, 110, 200), C(140, 80, 190), C(220, 40, 60) };
                SLine(g, C(170, 150, 120), 3f, 26, 4, 26, 76);
                for (int i = 0; i < 5; i++)
                {
                    float y = 8 + i * 16;
                    FillRound(g, new RectangleF(30, y - 6.5f, 64, 13), 4, Shade(c[i], 100));
                    SDisc(g, c[i], 26, y, 4);
                    SDisc(g, Color.White, 26, y, 1.6f);
                    ST(g, years[i].ToString(), 0, y - 6, 22, 12, 11, c[i]);
                    ST(g, ev[i], 31, y - 6, 62, 12, 8, SInk);
                }
                // flag stripe at the end of the timeline
                g.FillRectangle(Brushes.White, 22, 76, 8, 2);
                g.FillRectangle(B(C(220, 30, 50)), 22, 78, 8, 2);
            }
            else
            {
                Func<double, double, PointF> proj = (lon, lat) => new PointF((float)(lon + 180) / 360f * 94f, (float)(84 - lat) * 80f / 164f);
                Grad(g, new RectangleF(0, 0, 94, 80), C(120, 185, 240), C(80, 150, 225), 90f, false);
                Color[] lc = { C(240, 170, 70), C(240, 170, 70), C(120, 200, 90), C(230, 110, 100), C(250, 215, 80), C(200, 150, 220), C(240, 130, 60), C(245, 250, 255) };
                for (int i = 0; i < SWorldLL.Length; i++)
                {
                    var pts = SProject(SWorldLL[i], proj);
                    using (var b = B(lc[i])) g.FillClosedCurve(b, pts, FillMode.Alternate, 0.3f);
                    using (var p = P(CA(150, 60, 60, 80), 0.6f)) g.DrawClosedCurve(p, pts, 0.3f, FillMode.Alternate);
                }
                string[] n = L.Pl
                    ? new[] { "Ameryka\nPłn.", "Ameryka\nPłd.", "Europa", "Afryka", "Azja", "Australia", "Antarktyda" }
                    : new[] { "North\nAmerica", "South\nAmerica", "Europe", "Africa", "Asia", "Australia", "Antarctica" };
                double[] pos = { -110, 55, -62, -18, 20, 56, 20, 5, 100, 52, 134, -44, 0, -74 };
                for (int i = 0; i < 7; i++)
                {
                    var p = proj(pos[i * 2], pos[i * 2 + 1]);
                    float x = Math.Min(Math.Max(p.X - 15, 0), 64);
                    STO(g, n[i], new RectangleF(x, p.Y - (i < 2 ? 7 : 4), 30, i < 2 ? 14 : 8), 7, SInk, CA(200, 255, 255, 255));
                }
                var oc = C(235, 248, 255);
                string[] o = L.Pl ? new[] { "Pacyfik", "Atlantyk", "Indyjski" } : new[] { "Pacific", "Atlantic", "Indian" };
                double[] op = { -158, 15, -28, 30, 70, -20 };
                for (int i = 0; i < 3; i++)
                {
                    var p = proj(op[i * 2], op[i * 2 + 1]);
                    float x = Math.Min(Math.Max(p.X - 12, 0), 70);
                    ST(g, o[i], x, p.Y - 3.5f, 24, 7, 6.5f, oc);
                }
            }
        }

        static void SEnglish(Graphics g, int v)
        {
            // For Polish players the big word is English; for English players the big word is Polish.
            if (v == 0)
            {
                string[] en = { "Hello!", "Good morning!", "Goodbye!" };
                string[] pl = { "Cześć!", "Dzień dobry!", "Do widzenia!" };
                string[] big = L.Pl ? en : pl, small = L.Pl ? pl : en;
                SBubble(g, new RectangleF(2, 1, 90, 27), C(90, 150, 235), 20, false);
                ST(g, big[0], 4, 2, 86, 17, 17, Color.White);
                ST(g, small[0], 4, 18, 86, 9, 9, C(20, 50, 120));
                SBubble(g, new RectangleF(2, 33, 60, 20), C(240, 150, 50), 12, false);
                ST(g, big[1], 4, 34, 56, 11, 10, Color.White);
                ST(g, small[1], 4, 45, 56, 8, 8, C(110, 50, 0));
                SBubble(g, new RectangleF(32, 58, 60, 20), C(80, 180, 100), 48, true);
                ST(g, big[2], 34, 59, 56, 11, 10, Color.White);
                ST(g, small[2], 34, 70, 56, 8, 8, C(20, 80, 30));
                // waving smiley
                SDisc(g, C(255, 210, 60), 78, 44, 9);
                SDisc(g, SInk, 75, 42, 1.1f);
                SDisc(g, SInk, 81, 42, 1.1f);
                using (var p = P(SInk, 1.1f)) g.DrawArc(p, 73.5f, 41, 9, 7, 20, 140);
                using (var b = B(C(255, 210, 60))) g.FillEllipse(b, 86, 30, 6, 8);
                using (var p = P(C(220, 160, 40), 1f)) { g.DrawArc(p, 84, 27, 10, 8, 200, 60); }
                // a small British + Polish flag
                using (var b = B(Color.White)) g.FillRectangle(b, 6, 60, 18, 6);
                using (var b = B(C(220, 30, 50))) g.FillRectangle(b, 6, 66, 18, 6);
                using (var p = P(C(150, 150, 160), 0.6f)) g.DrawRectangle(p, 6, 60, 18, 12);
            }
            else if (v == 1)
            {
                string[] cen = { "red", "blue", "green", "yellow" }, cpl = { "czerwony", "niebieski", "zielony", "żółty" };
                string[] nen = { "one", "two", "three", "four" }, npl = { "jeden", "dwa", "trzy", "cztery" };
                Color[] cc = { C(230, 50, 50), C(60, 110, 230), C(60, 170, 60), C(250, 210, 30) };
                for (int i = 0; i < 4; i++)
                {
                    float y = i * 20;
                    SDisc(g, cc[i], 7.5f, y + 10, 6.5f);
                    using (var p = P(CA(120, 0, 0, 0), 0.8f)) g.DrawEllipse(p, 1, y + 3.5f, 13, 13);
                    ST(g, L.Pl ? cen[i] : cpl[i], 16, y + 2, 31, 10, 10, Shade(cc[i], i == 3 ? -70 : -20));
                    ST(g, L.Pl ? cpl[i] : cen[i], 16, y + 11, 31, 8, 6.5f, SInk, false);

                    FillRound(g, new RectangleF(49, y + 3, 14, 14), 4, SRainbow[(i * 2 + 3) % 8]);
                    ST(g, (i + 1).ToString(), 49, y + 3.5f, 14, 13, 12, Color.White);
                    ST(g, L.Pl ? nen[i] : npl[i], 64, y + 2, 30, 10, 10, SInk);
                    ST(g, L.Pl ? npl[i] : nen[i], 64, y + 11, 30, 8, 6.5f, C(110, 110, 120), false);
                }
                SLine(g, C(220, 210, 190), 1f, 47, 2, 47, 78);
            }
            else
            {
                Grad(g, new RectangleF(0, 0, 94, 66), C(150, 200, 245), C(225, 240, 255), 90f, false);
                Grad(g, new RectangleF(0, 64, 94, 16), C(70, 120, 180), C(40, 85, 140), 90f, false);
                // Big Ben
                var stone = C(205, 175, 110);
                using (var b = B(stone)) g.FillRectangle(b, 6, 14, 12, 52);
                SFill(g, Shade(stone, -25), SP(5, 14), SP(12, 0), SP(19, 14));
                using (var b = B(Shade(stone, 15))) g.FillRectangle(b, 5, 14, 14, 4);
                SDisc(g, Color.White, 12, 24, 4.6f);
                using (var p = P(SInk, 0.8f)) { g.DrawEllipse(p, 7.4f, 19.4f, 9.2f, 9.2f); g.DrawLine(p, 12, 24, 12, 20.5f); g.DrawLine(p, 12, 24, 14.5f, 24); }
                using (var p = P(Shade(stone, -40), 0.6f)) for (int i = 0; i < 6; i++) g.DrawLine(p, 8, 34 + i * 5, 16, 34 + i * 5);
                // Tower Bridge
                var tw = C(190, 180, 160);
                foreach (var x in new[] { 56f, 80f })
                {
                    using (var b = B(tw)) g.FillRectangle(b, x, 24, 9, 42);
                    SFill(g, C(90, 110, 140), SP(x - 1, 24), SP(x + 4.5f, 14), SP(x + 10, 24));
                    using (var b = B(C(90, 110, 140))) { g.FillRectangle(b, x + 2, 30, 2, 4); g.FillRectangle(b, x + 5, 30, 2, 4); }
                }
                using (var b = B(C(80, 140, 200))) { g.FillRectangle(b, 65, 28, 15, 3); g.FillRectangle(b, 46, 52, 48, 3); }
                using (var p = P(C(80, 140, 200), 1.2f))
                {
                    g.DrawBezier(p, 46, 50, 50, 40, 54, 30, 56, 25);
                    g.DrawBezier(p, 89, 25, 91, 32, 92, 42, 94, 48);
                }
                // red double-decker bus
                var red = C(220, 35, 35);
                FillRound(g, new RectangleF(22, 40, 30, 23), 3, red);
                using (var b = B(C(210, 235, 250)))
                    for (int i = 0; i < 4; i++) { g.FillRectangle(b, 24.5f + i * 6.8f, 43, 5, 5); g.FillRectangle(b, 24.5f + i * 6.8f, 51, 5, 5); }
                using (var b = B(C(250, 220, 90))) g.FillRectangle(b, 22, 49, 30, 1);
                SDisc(g, SInk, 28, 63, 3);
                SDisc(g, SInk, 46, 63, 3);
                SDisc(g, C(180, 180, 180), 28, 63, 1.2f);
                SDisc(g, C(180, 180, 180), 46, 63, 1.2f);
                ST(g, "Big Ben", 0, 70, 23, 9, 7, Color.White);
                ST(g, L.Pl ? "bus" : "autobus", 24, 70, 25, 9, 7, C(255, 220, 220));
                ST(g, "Tower Bridge", 50, 70, 43, 9, 7, Color.White);
            }
        }

        static void SBubble(Graphics g, RectangleF r, Color c, float tailX, bool tailTop)
        {
            FillRound(g, r, 8, c);
            if (tailTop) SFill(g, c, SP(r.X + tailX - 4, r.Y + 1), SP(r.X + tailX + 4, r.Y + 1), SP(r.X + tailX - 6, r.Y - 5));
            else SFill(g, c, SP(r.X + tailX - 4, r.Bottom - 1), SP(r.X + tailX + 4, r.Bottom - 1), SP(r.X + tailX - 6, r.Bottom + 5));
        }

        /// <summary>A chess piece standing on (cx, by); height about 27*s. kind: 0 king, 1 queen, 2 rook, 3 bishop, 4 knight, 5 pawn.</summary>
        static void SPiece(Graphics g, int kind, float cx, float by, float s, bool white)
        {
            var fill = white ? C(250, 248, 240) : C(35, 35, 45);
            var line = white ? C(40, 40, 50) : C(200, 200, 210);
            var st = g.Save();
            g.TranslateTransform(cx, by);
            g.ScaleTransform(s, s);
            using (var path = new GraphicsPath())
            {
                switch (kind)
                {
                    case 0:
                    case 1:
                        path.AddPolygon(new[] { SP(-3, -14), SP(3, -14), SP(6, -7), SP(-6, -7) });
                        if (kind == 0)
                        {
                            path.AddEllipse(-5.5f, -21, 11, 8);
                            path.AddRectangle(new RectangleF(-1, -27, 2, 7));
                            path.AddRectangle(new RectangleF(-3.5f, -25, 7, 2));
                        }
                        else
                        {
                            path.AddPolygon(new[] { SP(-7, -23), SP(-4, -17), SP(-2, -24), SP(0, -17), SP(2, -24), SP(4, -17), SP(7, -23), SP(5, -13), SP(-5, -13) });
                            foreach (var x in new[] { -7f, -2f, 2f, 7f }) path.AddEllipse(x - 1.4f, -25.5f, 2.8f, 2.8f);
                        }
                        break;
                    case 2:
                        path.AddRectangle(new RectangleF(-5, -17, 10, 10));
                        path.AddRectangle(new RectangleF(-7, -22, 14, 5));
                        path.AddRectangle(new RectangleF(-7, -25, 3, 3));
                        path.AddRectangle(new RectangleF(-1.5f, -25, 3, 3));
                        path.AddRectangle(new RectangleF(4, -25, 3, 3));
                        break;
                    case 3:
                        path.AddPolygon(new[] { SP(-3, -13), SP(3, -13), SP(5, -7), SP(-5, -7) });
                        path.AddEllipse(-5, -23, 10, 12);
                        path.AddEllipse(-1.5f, -26, 3, 3);
                        break;
                    case 4:
                        path.AddPolygon(new[] { SP(6, -7), SP(7, -14), SP(5, -20), SP(2, -24), SP(1, -27), SP(-1, -24), SP(-5, -21), SP(-8, -16), SP(-8, -13), SP(-5, -12.5f), SP(-2, -15), SP(-4, -10), SP(-6, -7) });
                        break;
                    default:
                        path.AddPolygon(new[] { SP(-3, -14), SP(3, -14), SP(6, -7), SP(-6, -7) });
                        path.AddEllipse(-4, -21, 8, 8);
                        break;
                }
                path.AddRectangle(new RectangleF(-7, -8, 14, 3));
                path.AddRectangle(new RectangleF(-9, -5, 18, 5));
                path.FillMode = FillMode.Winding;
                using (var b = B(fill)) g.FillPath(b, path);
                using (var p = P(line, 1.2f)) g.DrawPath(p, path);
            }
            if (kind == 4) SDisc(g, line, -2, -20, 1.1f);
            if (kind == 3) using (var p = P(line, 1f)) g.DrawLine(p, -2, -19, 2, -15);
            g.Restore(st);
        }

        static void SLogic(Graphics g, int v)
        {
            if (v == 0)
            {
                string[] n = L.Pl ? new[] { "król", "hetman", "wieża", "goniec", "skoczek", "pion" } : new[] { "king", "queen", "rook", "bishop", "knight", "pawn" };
                float cw = 94f / 3;
                for (int i = 0; i < 6; i++)
                {
                    float x = (i % 3) * cw, y = (i / 3) * 40;
                    for (int q = 0; q < 4; q++)
                        for (int w = 0; w < 3; w++)
                            using (var b = B((q + w + i) % 2 == 0 ? C(240, 220, 180) : C(200, 160, 110)))
                                g.FillRectangle(b, x + 1 + w * (cw - 2) / 3, y + 1 + q * 7.5f, (cw - 2) / 3 + 0.5f, 7.6f);
                    SPiece(g, i, x + cw / 2, y + 30, 1.0f, i % 2 == 0);
                    ST(g, n[i], x, y + 31, cw, 9, 8.5f, SInk);
                }
            }
            else if (v == 1)
            {
                string[][] rows = { new[] { "2, 4, 8, ", "?" }, new[] { "1, 3, 5, 7, ", "?" }, new[] { "10, 20, 30, ", "?" } };
                Color[] bg = { C(255, 235, 200), C(220, 240, 255), C(225, 245, 220), C(245, 225, 250) };
                for (int i = 0; i < 4; i++)
                {
                    float y = i * 20;
                    FillRound(g, new RectangleF(1, y + 1.5f, 92, 17), 6, bg[i]);
                    if (i < 3) SRuns(g, rows[i], new[] { SInk, C(220, 40, 40) }, new RectangleF(4, y + 2.5f, 86, 15), 13);
                    else
                    {
                        for (int k = 0; k < 4; k++)
                        {
                            float cx = 14 + k * 15, cy = y + 10;
                            if (k % 2 == 0) SFill(g, C(240, 120, 40), SP(cx, cy - 6), SP(cx + 6, cy + 5), SP(cx - 6, cy + 5));
                            else SDisc(g, C(60, 120, 220), cx, cy, 5.5f);
                        }
                        ST(g, "?", 70, y + 2.5f, 16, 15, 14, C(220, 40, 40));
                    }
                }
            }
            else
            {
                float u = 12, ox = 2, oy = 6;
                Func<float, float, PointF> T = (x, y) => SP(ox + x * u, oy + y * u);
                var pieces = new[]
                {
                    new[] { T(0, 0), T(4, 0), T(2, 2) },
                    new[] { T(0, 0), T(2, 2), T(0, 4) },
                    new[] { T(2, 4), T(4, 4), T(4, 2) },
                    new[] { T(4, 0), T(4, 2), T(3, 1) },
                    new[] { T(2, 2), T(3, 3), T(1, 3) },
                    new[] { T(2, 2), T(3, 1), T(4, 2), T(3, 3) },
                    new[] { T(0, 4), T(1, 3), T(3, 3), T(2, 4) },
                };
                for (int i = 0; i < pieces.Length; i++)
                {
                    SFill(g, SRainbow[i], pieces[i]);
                    using (var p = P(Color.White, 1.2f)) g.DrawPolygon(p, pieces[i]);
                }
                // a cat made of the same 7 pieces (silhouette)
                var cat = C(80, 60, 120);
                var catParts = new[]
                {
                    new[] { SP(70, 6), SP(78, 14), SP(70, 22), SP(62, 14) },
                    new[] { SP(62, 14), SP(62, 4), SP(67, 9) },
                    new[] { SP(78, 14), SP(78, 4), SP(73, 9) },
                    new[] { SP(62, 22), SP(78, 22), SP(70, 30) },
                    new[] { SP(58, 30), SP(82, 30), SP(58, 54) },
                    new[] { SP(82, 30), SP(82, 54), SP(58, 54) },
                    new[] { SP(82, 54), SP(88, 48), SP(92, 36), SP(86, 42) },
                };
                foreach (var cp in catParts)
                {
                    SFill(g, cat, cp);
                    using (var p = P(C(150, 130, 190), 0.8f)) g.DrawPolygon(p, cp);
                }
                SDisc(g, C(255, 230, 90), 67, 13, 1.3f);
                SDisc(g, C(255, 230, 90), 73, 13, 1.3f);
                ST(g, SL("7 części", "7 pieces"), 0, 64, 52, 12, 10, SInk);
                ST(g, SL("Ułóż kota!", "Make a cat!"), 50, 64, 44, 12, 10, C(120, 70, 170));
            }
        }

        /// <summary>A clock face with hands for h:m (hour hand moves with the minutes).</summary>
        static void SClock(Graphics g, float cx, float cy, float r, int h, int m, Color rim)
        {
            SDisc(g, rim, cx, cy, r);
            SDisc(g, Color.White, cx, cy, r - 2.2f);
            using (var p = P(SInk, 0.8f))
                for (int i = 0; i < 12; i++)
                {
                    double a = i * Math.PI / 6;
                    float r1 = r - (i % 3 == 0 ? 6.5f : 4.5f), r2 = r - 3;
                    if (i % 3 != 0) g.DrawLine(p, cx + (float)Math.Cos(a) * r1, cy + (float)Math.Sin(a) * r1, cx + (float)Math.Cos(a) * r2, cy + (float)Math.Sin(a) * r2);
                }
            string[] num = { "12", "3", "6", "9" };
            for (int i = 0; i < 4; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 2;
                float rr = r - 6.5f;
                ST(g, num[i], cx + (float)Math.Cos(a) * rr - 5, cy + (float)Math.Sin(a) * rr - 3.5f, 10, 7, 6, SInk);
            }
            double ha = -Math.PI / 2 + ((h % 12) + m / 60.0) * Math.PI / 6, ma = -Math.PI / 2 + m * Math.PI / 30;
            SLine(g, SInk, 2.2f, cx, cy, cx + (float)Math.Cos(ha) * r * 0.45f, cy + (float)Math.Sin(ha) * r * 0.45f);
            SLine(g, C(220, 50, 50), 1.4f, cx, cy, cx + (float)Math.Cos(ma) * r * 0.72f, cy + (float)Math.Sin(ma) * r * 0.72f);
            SDisc(g, SInk, cx, cy, 1.4f);
        }

        static void STime(Graphics g, int v)
        {
            if (v == 0)
            {
                int[] hh = { 3, 7, 12 }, mm = { 0, 30, 15 };
                string[] words = L.Pl ? new[] { "trzecia", "wpół do\nósmej", "kwadrans po\ndwunastej" } : new[] { "three\no'clock", "half past\nseven", "a quarter\npast twelve" };
                Color[] rim = { C(60, 120, 220), C(230, 120, 40), C(60, 170, 80) };
                for (int i = 0; i < 3; i++)
                {
                    float cx = 15.5f + i * 31.5f;
                    SClock(g, cx, 17, 14.5f, hh[i], mm[i], rim[i]);
                    FillRound(g, new RectangleF(cx - 14, 35, 28, 13), 4, Shade(rim[i], 90));
                    ST(g, hh[i] + ":" + mm[i].ToString("00"), cx - 14, 35.5f, 28, 12, 11, Shade(rim[i], -30));
                    ST(g, words[i], cx - 15.5f, 50, 31, 17, 8, SInk);
                }
                ST(g, SL("krótka – godziny, długa – minuty", "short hand: hours, long hand: minutes"), 0, 70, 94, 9, 6.5f, C(120, 100, 80), false);
            }
            else if (v == 1)
            {
                float cx = 47, cy = 40, r = 39;
                Color[] sc = { C(160, 205, 245), C(160, 225, 130), C(255, 215, 90), C(245, 160, 80) };
                string[] sn = L.Pl ? new[] { "zima", "wiosna", "lato", "jesień" } : new[] { "winter", "spring", "summer", "autumn" };
                for (int i = 0; i < 4; i++)
                    using (var b = B(sc[i])) g.FillPie(b, cx - r, cy - r, 2 * r, 2 * r, -135 + i * 90, 90);
                using (var p = P(Color.White, 1.5f))
                {
                    g.DrawEllipse(p, cx - r, cy - r, 2 * r, 2 * r);
                    for (int i = 0; i < 4; i++)
                    {
                        double a = (-135 + i * 90) * Math.PI / 180;
                        g.DrawLine(p, cx, cy, cx + (float)Math.Cos(a) * r, cy + (float)Math.Sin(a) * r);
                    }
                }
                string[] months = L.Pl
                    ? new[] { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII" }
                    : new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
                for (int i = 0; i < 12; i++)
                {
                    double a = (-90 + i * 30) * Math.PI / 180;
                    float x = cx + (float)Math.Cos(a) * 33, y = cy + (float)Math.Sin(a) * 33;
                    ST(g, months[i], x - 9, y - 4, 18, 8, 7.5f, SInk);
                }
                Color[] tc = { C(40, 90, 170), C(40, 130, 40), C(190, 120, 0), C(190, 80, 20) };
                for (int i = 0; i < 4; i++)
                {
                    double a = (-90 + i * 90) * Math.PI / 180;
                    float rr = i % 2 == 0 ? 16 : 13.5f, x = cx + (float)Math.Cos(a) * rr, y = cy + (float)Math.Sin(a) * rr;
                    ST(g, sn[i], x - 11, y - 4.5f, 22, 9, 8, tc[i]);
                }

            }
            else
            {
                string[] n = L.Pl ? new[] { "rano", "południe", "wieczór", "noc" } : new[] { "morning", "noon", "evening", "night" }; string[] hrs = { "7:00", "12:00", "18:00", "22:00" };
                Color[][] sky =
                {
                    new[] { C(255, 190, 200), C(170, 210, 250) },
                    new[] { C(90, 160, 240), C(170, 215, 255) },
                    new[] { C(130, 90, 170), C(255, 150, 80) },
                    new[] { C(15, 20, 60), C(40, 50, 110) },
                };
                for (int i = 0; i < 4; i++)
                {
                    var r = new RectangleF((i % 2) * 47, (i / 2) * 40, 46, 39);
                    Grad(g, new RectangleF(r.X, r.Y, r.Width, 30), sky[i][0], sky[i][1], 90f, false);
                    using (var b = B(i == 3 ? C(30, 60, 40) : C(110, 180, 80))) g.FillRectangle(b, r.X, r.Y + 24, r.Width, 6);
                    switch (i)
                    {
                        case 0: SSun(g, r.X + 12, r.Y + 24, 6, C(255, 170, 60)); break;
                        case 1: SSun(g, r.X + 23, r.Y + 9, 6, C(255, 220, 50)); break;
                        case 2: SSun(g, r.X + 34, r.Y + 23, 6, C(240, 90, 50)); break;
                        default:
                            SDisc(g, C(250, 240, 180), r.X + 30, r.Y + 10, 6);
                            SDisc(g, sky[3][0], r.X + 33, r.Y + 8, 5.5f);
                            using (var b = B(Color.White))
                                foreach (var s in new[] { SP(8, 6), SP(16, 14), SP(40, 18), SP(12, 20), SP(22, 5) })
                                    g.FillPolygon(b, StarPoints(r.X + s.X, r.Y + s.Y, 2.2f, 0.9f));
                            break;
                    }
                    using (var b = B(i == 3 ? C(40, 45, 80) : Color.White)) g.FillRectangle(b, r.X, r.Y + 30, r.Width, 9);
                    ST(g, n[i], r.X + 1, r.Y + 30.5f, r.Width - 2, 8, 8, i == 3 ? Color.White : SInk); ST(g, hrs[i], i % 2 == 0 ? r.Right - 20 : r.X + 1, r.Y + 1, 19, 8, 7, i == 3 ? C(255, 240, 160) : SInk);
                }
            }
        }

        static void SSun(Graphics g, float cx, float cy, float r, Color c)
        {
            using (var p = P(c, 1.2f))
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4;
                    g.DrawLine(p, cx + (float)Math.Cos(a) * (r + 1.5f), cy + (float)Math.Sin(a) * (r + 1.5f), cx + (float)Math.Cos(a) * (r + 4), cy + (float)Math.Sin(a) * (r + 4));
                }
            SDisc(g, c, cx, cy, r);
        }

        static void SMusic(Graphics g, int v)
        {
            if (v == 0)
            {
                float bottom = 46, sp = 7;
                using (var p = P(SInk, 1f))
                    for (int i = 0; i < 5; i++) g.DrawLine(p, 1, bottom - i * sp, 93, bottom - i * sp);
                STrebleClef(g, 7, bottom, sp);
                string[] sol = L.Pl ? new[] { "do", "re", "mi", "fa", "sol", "la", "si", "do" } : new[] { "do", "re", "mi", "fa", "sol", "la", "ti", "do" };
                string[] let = L.Pl ? new[] { "C", "D", "E", "F", "G", "A", "H", "C" } : new[] { "C", "D", "E", "F", "G", "A", "B", "C" };
                Color[] nc = { C(230, 50, 50), C(245, 140, 30), C(220, 180, 0), C(60, 170, 60), C(30, 160, 170), C(60, 100, 220), C(150, 80, 200), C(230, 50, 50) };
                for (int i = 0; i < 8; i++)
                {
                    float x = 22 + i * 9.6f, y = bottom - (i - 2) * sp / 2;
                    if (i == 0) SLine(g, SInk, 1f, x - 5.5f, y, x + 5.5f, y);
                    var st = g.Save();
                    g.TranslateTransform(x, y);
                    g.RotateTransform(-20);
                    using (var b = B(nc[i])) g.FillEllipse(b, -4.2f, -3f, 8.4f, 6f);
                    g.Restore(st);
                    if (i < 6) SLine(g, SInk, 1f, x + 3.6f, y - 1, x + 3.6f, y - 20);
                    else SLine(g, SInk, 1f, x - 3.6f, y + 1, x - 3.6f, y + 20);
                    ST(g, sol[i], x - 5, 60, 10, 9, 8, nc[i]);
                    ST(g, let[i], x - 5, 70, 10, 9, 8, SInk);
                }
                ST(g, "♪", 70, 0, 12, 12, 11, C(200, 80, 160));
                ST(g, "♫", 82, 2, 12, 12, 11, C(80, 120, 220));
            }
            else if (v == 1)
            {
                float cx = 27, cy = 32, r = 26;
                Color[] wc = { C(230, 40, 40), C(245, 140, 30), C(250, 215, 30), C(60, 170, 60), C(50, 100, 220), C(140, 70, 190) };
                for (int i = 0; i < 6; i++)
                    using (var b = B(wc[i])) g.FillPie(b, cx - r, cy - r, 2 * r, 2 * r, -120 + i * 60, 60);
                using (var p = P(Color.White, 1.5f)) g.DrawEllipse(p, cx - r, cy - r, 2 * r, 2 * r);
                SDisc(g, Color.White, cx, cy, 7);
                for (int i = 0; i < 6; i += 2)
                {
                    double a = (-90 + i * 60) * Math.PI / 180;
                    using (var b = B(Color.White)) g.FillPolygon(b, StarPoints(cx + (float)Math.Cos(a) * 19, cy + (float)Math.Sin(a) * 19, 4.5f, 2f));
                }
                ST(g, SL("podstawowe", "primary"), 55, 2, 39, 10, 9, SInk);
                ST(g, SL("pochodne", "secondary"), 55, 30, 39, 10, 9, SInk);
                for (int i = 0; i < 3; i++)
                {
                    SDisc(g, wc[i * 2], 63 + i * 12, 19, 5f);
                    SDisc(g, wc[i * 2 + 1], 63 + i * 12, 47, 5f);
                    using (var b = B(Color.White)) g.FillPolygon(b, StarPoints(63 + i * 12, 19, 2.6f, 1.1f));
                }
                // mixing: red + yellow = orange, yellow + blue = green, blue + red = purple
                int[][] mix = { new[] { 0, 2, 1 }, new[] { 2, 4, 3 }, new[] { 4, 0, 5 } };
                FillRound(g, new RectangleF(0, 64, 94, 16), 6, C(245, 242, 235));
                for (int i = 0; i < 3; i++)
                {
                    float x = 2 + i * 31.3f;
                    SDisc(g, wc[mix[i][0]], x + 4, 72, 3.6f);
                    ST(g, "+", x + 7.5f, 67, 6, 10, 8, SInk);
                    SDisc(g, wc[mix[i][1]], x + 17, 72, 3.6f);
                    ST(g, "=", x + 20.5f, 67, 6, 10, 8, SInk);
                    SDisc(g, wc[mix[i][2]], x + 30 - 3.6f, 72, 3.6f);
                }
            }
            else
            {
                string[] fam = L.Pl ? new[] { "strunowe", "dęte", "perkusyjne" } : new[] { "strings", "wind", "percussion" };
                string[] ins = L.Pl ? new[] { "skrzypce", "trąbka", "bęben" } : new[] { "violin", "trumpet", "drum" };
                Color[] bg = { C(255, 235, 210), C(255, 245, 200), C(250, 225, 230) };
                float cw = 94f / 3;
                for (int i = 0; i < 3; i++)
                {
                    float x = i * cw, cx = x + cw / 2;
                    FillRound(g, new RectangleF(x + 1, 1, cw - 2, 78), 6, bg[i]);
                    if (i == 0)
                    {
                        var wood = C(185, 95, 40);
                        using (var b = B(wood)) { g.FillEllipse(b, cx - 7, 18, 14, 13); g.FillEllipse(b, cx - 8.5f, 28, 17, 16); }
                        using (var b = B(C(40, 30, 25))) { g.FillRectangle(b, cx - 1.5f, 4, 3, 22); g.FillEllipse(b, cx - 2.5f, 1, 5, 5); }
                        using (var p = P(C(230, 220, 200), 0.5f)) for (int k = -1; k <= 1; k++) g.DrawLine(p, cx + k, 6, cx + k, 38);
                        using (var b = B(C(40, 30, 25))) g.FillRectangle(b, cx - 3.5f, 36, 7, 2);
                        using (var p = P(C(60, 40, 20), 0.8f)) { g.DrawArc(p, cx - 6, 28, 3, 7, 90, 180); g.DrawArc(p, cx + 3, 28, 3, 7, 270, 180); }
                        SLine(g, C(150, 110, 70), 1.2f, cx - 13, 44, cx + 13, 8);
                    }
                    else if (i == 1)
                    {
                        var gold = C(235, 185, 50);
                        using (var p = P(gold, 2.4f)) { g.DrawLine(p, cx - 13, 24, cx + 6, 24); }
                        using (var p = P(gold, 1.6f)) g.DrawArc(p, cx - 9, 24, 14, 9, 0, 180);
                        SFill(g, gold, SP(cx + 5, 22.5f), SP(cx + 13, 15), SP(cx + 13, 33), SP(cx + 5, 25.5f));
                        using (var b = B(Shade(gold, -30))) { for (int k = 0; k < 3; k++) g.FillRectangle(b, cx - 6 + k * 3.5f, 17, 2, 6); }
                        SDisc(g, Shade(gold, -40), cx - 13.5f, 24, 1.8f);
                        SLine(g, C(255, 240, 180), 0.8f, cx - 11, 23, cx + 4, 23);
                    }
                    else
                    {
                        var red = C(210, 50, 60);
                        using (var b = B(red)) g.FillRectangle(b, cx - 12, 22, 24, 18);
                        using (var b = B(red)) g.FillEllipse(b, cx - 12, 36, 24, 8);
                        using (var p = P(Color.White, 0.9f))
                            g.DrawLines(p, new[] { SP(cx - 12, 24), SP(cx - 6, 38), SP(cx, 24), SP(cx + 6, 38), SP(cx + 12, 24) });
                        using (var b = B(C(250, 240, 220))) g.FillEllipse(b, cx - 12, 18, 24, 8);
                        using (var p = P(C(170, 150, 130), 0.8f)) g.DrawEllipse(p, cx - 12, 18, 24, 8);
                        SLine(g, C(150, 100, 60), 1.6f, cx - 11, 6, cx - 1, 20);
                        SLine(g, C(150, 100, 60), 1.6f, cx + 11, 6, cx + 1, 20);
                        SDisc(g, C(240, 230, 210), cx - 11, 6, 1.6f);
                        SDisc(g, C(240, 230, 210), cx + 11, 6, 1.6f);
                    }
                    ST(g, fam[i], x, 50, cw, 11, 8.5f, SInk);
                    ST(g, ins[i], x + 1, 62, cw - 2, 9, 7, C(120, 90, 70), false);
                }
            }
        }

        static void STrebleClef(Graphics g, float x, float bottom, float sp)
        {
            float gLine = bottom - sp; // the G line
            using (var p = P(SInk, 1.6f))
            {
                g.DrawBezier(p, x - 1, bottom + 9, x + 2, bottom - 8, x + 3, bottom - 4 * sp - 2, x, bottom - 4 * sp - 6);
                g.DrawBezier(p, x, bottom - 4 * sp - 6, x - 4, bottom - 4 * sp, x - 5, gLine - 6, x + 1, gLine - 9);
                g.DrawBezier(p, x + 1, gLine - 9, x + 7, gLine - 6, x + 6, gLine + 4, x, gLine + 3);
                g.DrawBezier(p, x, gLine + 3, x - 4, gLine + 2, x - 3, gLine - 4, x + 1, gLine - 3);
            }
            SDisc(g, SInk, x - 2, bottom + 8, 1.8f);
        }

        static void SHealth(Graphics g, int v)
        {
            if (v == 0)
            {
                float cx = 36, cy = 40;
                SDisc(g, C(200, 200, 205), cx + 1, cy + 1.5f, 37);
                SDisc(g, Color.White, cx, cy, 37);
                float r = 32;
                Color[] sc = { C(150, 215, 120), C(255, 170, 160), C(240, 210, 150), C(230, 175, 205) };
                float[] start = { 180, 288, 0, 90 }, sweep = { 108, 72, 90, 90 };
                for (int i = 0; i < 4; i++) using (var b = B(sc[i])) g.FillPie(b, cx - r, cy - r, 2 * r, 2 * r, start[i], sweep[i]);
                using (var p = P(Color.White, 1.5f))
                    foreach (var a0 in new[] { 0f, 90f, 180f, 288f })
                        g.DrawLine(p, cx, cy, cx + (float)Math.Cos(a0 * Math.PI / 180) * r, cy + (float)Math.Sin(a0 * Math.PI / 180) * r);
                string[] n = L.Pl ? new[] { "warzywa", "owoce", "zboża", "białko" } : new[] { "vegetables", "fruit", "grains", "protein" };
                for (int i = 0; i < 4; i++)
                {
                    double mid = (start[i] + sweep[i] / 2) * Math.PI / 180;
                    float ix = cx + (float)Math.Cos(mid) * 18, iy = cy + (float)Math.Sin(mid) * 18 - 3;
                    switch (i)
                    {
                        case 0:
                            SLine(g, C(80, 140, 50), 2f, ix, iy + 4, ix, iy);
                            foreach (var d in new[] { SP(-3, -1), SP(3, -1), SP(0, -4), SP(-1.5f, 1), SP(1.5f, 1) }) SDisc(g, C(60, 150, 50), ix + d.X, iy + d.Y - 1, 2.6f);
                            SFill(g, C(245, 140, 40), SP(ix + 6, iy - 2), SP(ix + 10, iy - 3), SP(ix + 8, iy + 6));
                            break;
                        case 1:
                            SDisc(g, C(220, 40, 40), ix - 1.8f, iy, 3.6f);
                            SDisc(g, C(220, 40, 40), ix + 1.8f, iy, 3.6f);
                            SLine(g, C(110, 70, 30), 1f, ix, iy - 3, ix + 1, iy - 6);
                            break;
                        case 2:
                            FillRound(g, new RectangleF(ix - 5, iy - 5, 10, 9), 3, C(200, 140, 70));
                            FillRound(g, new RectangleF(ix - 3.5f, iy - 3.5f, 7, 6.5f), 2, C(250, 225, 170));
                            break;
                        default:
                            using (var b = B(Color.White)) g.FillEllipse(b, ix - 5, iy - 4, 10, 8);
                            SDisc(g, C(255, 200, 40), ix, iy, 2.3f);
                            break;
                    }
                    STO(g, n[i], new RectangleF(ix - 16, iy + 5, 32, 9), 8, SInk, CA(180, 255, 255, 255));
                }
                // glass of water
                SFill(g, C(215, 235, 250), SP(77, 26), SP(92, 26), SP(90, 58), SP(79, 58));
                SFill(g, C(90, 170, 240), SP(78, 34), SP(91, 34), SP(90, 58), SP(79, 58));
                using (var p = P(C(120, 150, 180), 0.9f)) g.DrawPolygon(p, new[] { SP(77, 26), SP(92, 26), SP(90, 58), SP(79, 58) });
                SLine(g, Color.White, 1f, 80, 37, 80.6f, 54);
                ST(g, SL("woda", "water"), 72, 60, 22, 9, 8, C(40, 110, 200));
            }
            else if (v == 1)
            {
                string[] n = L.Pl ? new[] { "piłka nożna", "pływanie", "kolarstwo", "koszykówka" } : new[] { "football", "swimming", "cycling", "basketball" };
                Color[] c = { C(60, 160, 70), C(50, 130, 220), C(240, 140, 30), C(200, 60, 80) };
                for (int i = 0; i < 4; i++)
                {
                    float x = (i % 2) * 47, y = (i / 2) * 40, cx = x + 23.5f, cy = y + 16;
                    FillRound(g, new RectangleF(cx - 15, cy - 15, 30, 30), 6, c[i]);
                    SSport(g, i, cx, cy);
                    ST(g, n[i], x, y + 31, 47, 9, 8, Shade(c[i], -30));
                }
            }
            else
            {
                FillRound(g, new RectangleF(1, 2, 92, 36), 8, C(255, 165, 60));
                SDisc(g, Color.White, 18, 20, 13);
                {
                    var st = g.Save();
                    g.TranslateTransform(18, 20);
                    using (var p = P(C(240, 120, 30), 2.6f))
                    {
                        g.FillEllipse(B(C(240, 120, 30)), 1, -10, 5, 5);
                        g.DrawLine(p, 2.5f, -5, 0, 2);
                        g.DrawLines(p, new[] { SP(-6, -3), SP(1, -4), SP(6, 0) });
                        g.DrawLines(p, new[] { SP(0, 2), SP(5, 5), SP(4, 10) });
                        g.DrawLines(p, new[] { SP(0, 2), SP(-4, 7), SP(-8, 6) });
                    }
                    g.Restore(st);
                }
                ST(g, "60 min", 34, 4, 58, 18, 17, Color.White);
                ST(g, SL("ruchu dziennie", "of activity a day"), 34, 22, 58, 12, 9, C(110, 50, 0));

                FillRound(g, new RectangleF(1, 42, 92, 36), 8, C(50, 60, 130));
                SDisc(g, C(250, 235, 150), 18, 60, 10);
                SDisc(g, C(50, 60, 130), 22, 57, 9);
                ST(g, "z", 22, 46, 8, 8, 7, Color.White);
                ST(g, "z", 27, 41.5f, 8, 8, 8, Color.White);
                ST(g, "9–10 h", 34, 44, 58, 18, 17, Color.White);
                ST(g, SL("snu każdej nocy", "of sleep a night"), 34, 62, 58, 12, 9, C(200, 210, 255));
            }
        }

        /// <summary>White sports pictogram centered at (cx,cy): 0 football, 1 swimming, 2 cycling, 3 basketball.</summary>
        static void SSport(Graphics g, int kind, float cx, float cy)
        {
            var st = g.Save();
            g.TranslateTransform(cx, cy);
            using (var p = P(Color.White, 2.4f))
            {
                switch (kind)
                {
                    case 0:
                        SDisc(g, Color.White, 1, -9, 2.6f);
                        g.DrawLine(p, 0.5f, -6, -1, 2);
                        g.DrawLines(p, new[] { SP(-6, -2), SP(0, -4.5f), SP(5, -6) });
                        g.DrawLines(p, new[] { SP(-1, 2), SP(-3, 6), SP(-6, 11) });
                        g.DrawLines(p, new[] { SP(-1, 2), SP(4, 5), SP(6, 9) });
                        SDisc(g, Color.White, 10, 9, 2.6f);
                        break;
                    case 1:
                        SDisc(g, Color.White, -5, -1, 2.6f);
                        g.DrawLine(p, -2, 1, 9, 2);
                        g.DrawLines(p, new[] { SP(-1, 0), SP(3, -7), SP(9, -4) });
                        using (var w = P(Color.White, 1.4f))
                            for (int k = 0; k < 2; k++)
                                g.DrawBezier(w, -12, 6 + k * 4, -6, 3 + k * 4, 0, 9 + k * 4, 12, 6 + k * 4);
                        break;
                    case 2:
                        using (var w = P(Color.White, 1.6f)) { g.DrawEllipse(w, -12, 1, 10, 10); g.DrawEllipse(w, 2, 1, 10, 10); }
                        using (var w = P(Color.White, 1.4f)) g.DrawLines(w, new[] { SP(-7, 6), SP(-2, -1), SP(5, -1), SP(7, 6) });
                        SDisc(g, Color.White, 2, -10, 2.6f);
                        g.DrawLines(p, new[] { SP(1, -7), SP(-3, -1), SP(0, 5) });
                        g.DrawLine(p, 0.5f, -6, 5, -3);
                        break;
                    default:
                        using (var w = P(Color.White, 1.4f)) { g.DrawLine(w, 11, -13, 11, -4); g.DrawLine(w, 5, -7, 11, -7); g.DrawLine(w, 6, -7, 8, -3); g.DrawLine(w, 10, -7, 8, -3); }
                        SDisc(g, Color.White, 3, -11, 2.6f);
                        SDisc(g, Color.White, -5, -5, 2.6f);
                        g.DrawLine(p, -5, -2, -5, 4);
                        g.DrawLine(p, -5, -1, 0, -7);
                        g.DrawLine(p, -5, -1, -9, 1);
                        g.DrawLines(p, new[] { SP(-5, 4), SP(-8, 8), SP(-8, 12) });
                        g.DrawLines(p, new[] { SP(-5, 4), SP(-2, 8), SP(-1, 12) });
                        break;
                }
            }
            g.Restore(st);
        }

        static void SScience(Graphics g, int v)
        {
            if (v == 0)
            {
                Grad(g, new RectangleF(0, 0, 94, 80), C(15, 20, 55), C(35, 30, 80), 90f, false);
                var rnd = new Random(9);
                for (int i = 0; i < 30; i++) using (var b = B(CA(150 + rnd.Next(100), 255, 255, 255))) g.FillRectangle(b, F(rnd) * 94, F(rnd) * 80, 1, 1);
                using (var br = new LinearGradientBrush(new RectangleF(-40, 14, 52, 52), C(255, 240, 120), C(250, 140, 30), 0f)) g.FillEllipse(br, -40, 14, 52, 52);
                string[] n = L.Pl ? new[] { "Merkury", "Wenus", "Ziemia", "Mars", "Jowisz", "Saturn", "Uran", "Neptun" }
                                  : new[] { "Mercury", "Venus", "Earth", "Mars", "Jupiter", "Saturn", "Uranus", "Neptune" };
                float[] x = { 17, 24, 32, 40, 51, 67, 80, 89 }, r = { 1.7f, 2.7f, 2.9f, 2.2f, 7f, 5.5f, 3.8f, 3.6f };
                Color[] c = { C(180, 175, 170), C(240, 210, 140), C(70, 140, 230), C(220, 90, 60), C(225, 185, 140), C(235, 210, 150), C(150, 220, 230), C(70, 110, 230) };
                float[] ly = { 2, 60, 11, 69, 2, 60, 11, 69 };
                using (var p = new Pen(CA(110, 255, 255, 255), 0.6f) { DashStyle = DashStyle.Dot })
                    for (int i = 0; i < 8; i++)
                    {
                        float y0 = ly[i] < 40 ? ly[i] + 8 : ly[i];
                        g.DrawLine(p, x[i], y0, x[i], ly[i] < 40 ? 40 - r[i] : 40 + r[i]);
                    }
                for (int i = 0; i < 8; i++)
                {
                    if (i == 5) using (var p = P(C(200, 180, 130), 1.4f)) g.DrawEllipse(p, x[i] - 10, 40 - 2.6f, 20, 5.2f);
                    SDisc(g, c[i], x[i], 40, r[i]);
                    if (i == 2) SDisc(g, C(80, 180, 80), x[i] + 0.8f, 39.5f, 1.2f);
                    if (i == 4) using (var p = P(C(190, 130, 90), 1f)) { g.DrawLine(p, x[i] - 6, 38, x[i] + 6, 38); g.DrawLine(p, x[i] - 6.5f, 42, x[i] + 6.5f, 42); }
                    if (i == 5) using (var p = P(C(200, 180, 130), 1.4f)) g.DrawArc(p, x[i] - 10, 40 - 2.6f, 20, 5.2f, 0, 180);
                    float lx = Math.Min(Math.Max(x[i] - 15, 0), 64);
                    ST(g, n[i], lx, ly[i], 30, 8, 7, Color.White);
                }
                ST(g, SL("Słońce", "Sun"), 0, 69, 18, 8, 6.5f, C(255, 220, 120));
            }
            else if (v == 1)
            {
                Grad(g, new RectangleF(0, 0, 94, 60), C(140, 195, 245), C(220, 240, 255), 90f, false);
                // land with a mountain and a river
                SFill(g, C(120, 180, 80), SP(40, 80), SP(52, 60), SP(74, 34), SP(84, 44), SP(94, 40), SP(94, 80));
                SFill(g, Color.White, SP(70, 39), SP(74, 34), SP(78, 39), SP(75, 38), SP(73, 40));
                using (var p = P(C(70, 140, 220), 2f)) g.DrawBezier(p, 76, 44, 70, 56, 62, 60, 46, 66);
                // sea
                Grad(g, new RectangleF(0, 60, 56, 20), C(60, 130, 220), C(30, 90, 180), 90f, false);
                SFill(g, C(60, 130, 220), SP(0, 62), SP(48, 62), SP(56, 80), SP(0, 80));
                using (var p = P(C(160, 210, 250), 0.9f))
                    for (int i = 0; i < 3; i++) g.DrawBezier(p, 4 + i * 14, 70, 7 + i * 14, 67, 10 + i * 14, 73, 14 + i * 14, 70);
                SSun(g, 11, 11, 6.5f, C(255, 200, 40));
                // evaporation
                var up = C(240, 120, 50);
                for (int i = 0; i < 3; i++)
                    using (var cap = new AdjustableArrowCap(3, 3))
                    using (var p = new Pen(up, 1.4f) { CustomEndCap = cap, DashStyle = DashStyle.Dash })
                        g.DrawBezier(p, 14 + i * 9, 60, 11 + i * 9, 52, 17 + i * 9, 44, 14 + i * 9, 36);
                // cloud and rain
                KCloud(g, 52, 6, 38, 255);
                using (var b = B(C(200, 210, 225))) g.FillEllipse(b, 56, 16, 30, 7);
                using (var p = P(C(40, 110, 220), 1.4f))
                    for (int i = 0; i < 4; i++) g.DrawLine(p, 58 + i * 4.5f, 26 + (i % 2) * 3, 56 + i * 4.5f, 33 + (i % 2) * 3);
                using (var cap = new AdjustableArrowCap(3, 3))
                using (var p = new Pen(up, 1.4f) { CustomEndCap = cap })
                    g.DrawBezier(p, 30, 28, 36, 16, 44, 12, 52, 14);
                STO(g, SL("parowanie", "evaporation"), new RectangleF(32, 40, 30, 9), 8, C(200, 80, 20), CA(200, 255, 255, 255));
                ST(g, SL("chmury", "clouds"), 54, 0, 40, 8, 7, SInk);
                ST(g, SL("deszcz", "rain"), 73, 25, 21, 9, 8, C(30, 80, 190));
                ST(g, SL("morze", "sea"), 2, 72, 30, 8, 7, Color.White);
                ST(g, SL("rzeka", "river"), 60, 60, 26, 8, 6.5f, Color.White);
            }
            else
            {
                // horseshoe magnet
                using (var p = new Pen(C(220, 40, 40), 7f)) g.DrawArc(p, 6, 3, 24, 24, 180, 90);
                using (var p = new Pen(C(50, 90, 220), 7f)) g.DrawArc(p, 6, 3, 24, 24, 270, 90);
                using (var b = B(C(220, 40, 40))) g.FillRectangle(b, 2.5f, 15, 7, 10);
                using (var b = B(C(50, 90, 220))) g.FillRectangle(b, 26.5f, 15, 7, 10);
                using (var b = B(C(200, 200, 210))) { g.FillRectangle(b, 2.5f, 25, 7, 4); g.FillRectangle(b, 26.5f, 25, 7, 4); }
                ST(g, "N", 2.5f, 15, 7, 9, 7, Color.White);
                ST(g, "S", 26.5f, 15, 7, 9, 7, Color.White);
                using (var p = P(C(130, 130, 140), 0.9f))
                {
                    g.DrawRectangle(p, 4, 31, 4, 7);
                    g.DrawRectangle(p, 28, 31, 4, 7);
                }
                using (var p = new Pen(C(230, 160, 40), 0.8f) { DashStyle = DashStyle.Dot })
                    g.DrawArc(p, 6, 22, 24, 14, 0, 180);
                ST(g, SL("magnes", "magnet"), 36, 2, 58, 12, 11, C(200, 40, 40));
                ST(g, SL("N + S przyciągają", "N + S attract"), 36, 15, 58, 9, 8, SInk);
                ST(g, SL("N + N odpychają", "N + N repel"), 36, 24, 58, 9, 8, C(110, 110, 120));
                // circuit
                using (var p = P(C(60, 60, 70), 1.8f))
                {
                    g.DrawLine(p, 10, 46, 41, 46);
                    g.DrawLine(p, 53, 46, 86, 46);
                    g.DrawLine(p, 86, 46, 86, 76);
                    g.DrawLine(p, 86, 76, 54, 76);
                    g.DrawLine(p, 42, 76, 10, 76);
                    g.DrawLine(p, 10, 76, 10, 70);
                    g.DrawLine(p, 10, 52, 10, 46);
                }
                // battery
                using (var b = B(C(60, 60, 70))) g.FillRectangle(b, 5, 52, 10, 18);
                using (var b = B(C(240, 180, 40))) g.FillRectangle(b, 5, 52, 10, 7);
                using (var b = B(C(160, 160, 170))) g.FillRectangle(b, 8, 50, 4, 2);
                ST(g, "+", 5, 52, 10, 7, 7, SInk);
                // bulb
                SDisc(g, CA(90, 255, 230, 80), 47, 40, 11);
                SDisc(g, C(255, 235, 110), 47, 40, 5.5f);
                using (var p = P(C(160, 120, 30), 0.8f)) g.DrawArc(p, 45, 38, 4, 4, 180, 180);
                using (var b = B(C(150, 150, 160))) g.FillRectangle(b, 43.5f, 44.5f, 7, 4);
                // switch (closed)
                SDisc(g, C(60, 60, 70), 42, 76, 1.8f);
                SDisc(g, C(60, 60, 70), 54, 76, 1.8f);
                SLine(g, C(220, 60, 60), 2f, 42, 76, 54, 74.5f);
                ST(g, SL("bateria", "battery"), 15, 53, 30, 9, 8, SInk);
                ST(g, SL("żarówka", "bulb"), 54, 49, 31, 9, 8, SInk);
                ST(g, SL("włącznik", "switch"), 30, 64, 36, 9, 8, SInk);
            }
        }

        // ------------------------------------------------------------------ captions

        /// <summary>One or two friendly sentences explaining a subject poster (shown when the player walks up to it).</summary>
        public static string SubjectPosterCaption(int subject, int variant)
        {
            int s = ((subject % 10) + 10) % 10, v = ((variant % 3) + 3) % 3;
            switch (s * 3 + v)
            {
                case 0: return SL("Tabliczka mnożenia: tam, gdzie spotyka się wiersz z kolumną, jest iloczyn, np. 7 × 8 = 56. Żółte pola to kwadraty liczb: 1, 4, 9, 16…",
                                  "Times table: where a row meets a column you find the product, e.g. 7 × 8 = 56. The yellow squares are square numbers: 1, 4, 9, 16…");
                case 1: return SL("Trójkąt ma 3 boki, a kwadrat i prostokąt mają po 4 boki i 4 kąty proste (90°). Koło nie ma żadnych kątów!",
                                  "A triangle has 3 sides, while a square and a rectangle have 4 sides and 4 right angles (90°). A circle has no corners at all!");
                case 2: return SL("Pizza podzielona na 2 równe części to połówki, na 4 – ćwiartki, a na 8 – ósemki. 1/2 to tyle samo co 2/4 i 4/8!",
                                  "A pizza cut into 2 equal parts gives halves, into 4 – quarters, into 8 – eighths. 1/2 is the same as 2/4 and 4/8!");
                case 3: return SL("Polski alfabet ma 32 litery, w tym 9 liter z ogonkami i kreskami: Ą, Ć, Ę, Ł, Ń, Ó, Ś, Ź, Ż.",
                                  "The English alphabet has 26 letters: 5 vowels (A, E, I, O, U) and 21 consonants.");
                case 4: return SL("Rzeczownik odpowiada na pytania kto? co?, czasownik – co robi?, a przymiotnik – jaki? jaka? jakie?",
                                  "A noun names a thing (cat), a verb tells what someone does (run) and an adjective describes something (happy).");
                case 5: return SL("Gdy ó wymienia się na o (stół – stoły), piszemy ó. Zapamiętaj też: góra, rzeka, żaba, chleb, herbata!",
                                  "Remember: i before e, except after c – friend and believe, but receive and ceiling. Many words ending in y change to -ies: baby → babies.");
                case 6: return SL("Łańcuch pokarmowy: trawę zjada zając, a zająca – lis. Strzałki pokazują, dokąd płynie energia.",
                                  "A food chain: grass is eaten by the rabbit, and the rabbit by the fox. The arrows show where the energy goes.");
                case 7: return SL("Korzeń pobiera wodę z ziemi, łodyga ją przewodzi, liście wytwarzają pokarm dzięki światłu, a z kwiatu powstają nasiona.",
                                  "The root takes water from the soil, the stem carries it, the leaves make food from sunlight and the flower makes seeds.");
                case 8: return SL("Z jaja wykluwa się gąsienica, która zamienia się w poczwarkę, a z poczwarki wychodzi motyl. To przeobrażenie zupełne!",
                                  "An egg hatches into a caterpillar, which turns into a pupa, and a butterfly comes out of the pupa. This change is called metamorphosis!");
                case 9: return SL("Polska leży nad Morzem Bałtyckim, a jej najdłuższa rzeka to Wisła. Stolicą jest Warszawa, a Kórnik leży niedaleko Poznania.",
                                  "Poland lies on the Baltic Sea and its longest river is the Vistula. The capital is Warsaw, and Kórnik is near Poznań.");
                case 10: return SL("W 966 r. Mieszko I przyjął chrzest, w 1025 r. Bolesław Chrobry został pierwszym królem, a w 1410 r. Polacy wygrali bitwę pod Grunwaldem. Konstytucję 3 Maja uchwalono w 1791 r., a niepodległość odzyskaliśmy w 1918 r.",
                                   "In 966 Mieszko I was baptised, in 1025 Bolesław the Brave became the first king and in 1410 the Poles won the Battle of Grunwald. The Constitution of 3 May was passed in 1791 and Poland regained independence in 1918.");
                case 11: return SL("Na Ziemi jest 7 kontynentów: Azja, Afryka, Ameryka Północna, Ameryka Południowa, Antarktyda, Europa i Australia. Największy ocean to Ocean Spokojny.",
                                   "There are 7 continents: Asia, Africa, North America, South America, Antarctica, Europe and Australia. The largest ocean is the Pacific.");
                case 12: return SL("Hello! znaczy Cześć!, Good morning! – Dzień dobry!, a Goodbye! – Do widzenia!",
                                   "Cześć! means Hello!, Dzień dobry! – Good morning! and Do widzenia! – Goodbye!");
                case 13: return SL("Red to czerwony, blue – niebieski, green – zielony, a yellow – żółty. Policz po angielsku: one, two, three, four!",
                                   "Czerwony is red, niebieski – blue, zielony – green and żółty – yellow. Count in Polish: jeden, dwa, trzy, cztery!");
                case 14: return SL("Big Ben to wielki dzwon w słynnej wieży zegarowej w Londynie. Po ulicach jeżdżą czerwone piętrowe autobusy, a nad Tamizą stoi Tower Bridge.",
                                   "Big Ben is the great bell in London's famous clock tower. Red double-decker buses drive through the streets and Tower Bridge crosses the River Thames.");
                case 15: return SL("W szachach są: król, hetman, wieże, gońce, skoczki i piony. Partia kończy się, gdy król dostanie mata!",
                                   "Chess has a king, a queen, rooks, bishops, knights and pawns. The game ends when a king is checkmated!");
                case 16: return SL("Szukaj reguły: 2, 4, 8, 16 – każda liczba jest dwa razy większa od poprzedniej. Co będzie dalej w pozostałych rzędach?",
                                   "Find the rule: 2, 4, 8, 16 – each number is double the one before. What comes next in the other rows?");
                case 17: return SL("Tangram to chińska układanka z 7 części: 5 trójkątów, kwadratu i równoległoboku. Ułożysz z nich kota, domek albo łódkę!",
                                   "A tangram is a Chinese puzzle of 7 pieces: 5 triangles, a square and a parallelogram. You can make a cat, a house or a boat!");
                case 18: return SL("Krótka wskazówka pokazuje godziny, a długa – minuty. 7:30 to wpół do ósmej, a 12:15 to kwadrans po dwunastej.",
                                   "The short hand shows the hours and the long hand the minutes. 7:30 is half past seven and 12:15 is a quarter past twelve.");
                case 19: return SL("Rok ma 12 miesięcy i 4 pory roku: wiosnę, lato, jesień i zimę. Kalendarzowa wiosna zaczyna się 21 marca.",
                                   "A year has 12 months and 4 seasons: spring, summer, autumn and winter. In Poland spring starts around 21 March.");
                case 20: return SL("Rano słońce wschodzi, w południe stoi najwyżej na niebie, wieczorem zachodzi, a w nocy świeci księżyc.",
                                   "In the morning the Sun rises, at noon it is highest in the sky, in the evening it sets and at night the Moon shines.");
                case 21: return SL("Gama C-dur to: do, re, mi, fa, sol, la, si, do (po polsku C, D, E, F, G, A, H, C). Nuty zapisujemy na pięciolinii z kluczem wiolinowym.",
                                   "The C major scale is do, re, mi, fa, sol, la, ti, do – or C, D, E, F, G, A, B, C. Notes are written on a five-line staff with a treble clef.");
                case 22: return SL("Barwy podstawowe to czerwony, żółty i niebieski. Mieszając je, dostajemy barwy pochodne: pomarańczowy, zielony i fioletowy.",
                                   "The primary colours are red, yellow and blue. Mixing them gives the secondary colours: orange, green and purple.");
                case 23: return SL("Skrzypce to instrument strunowy, trąbka – dęty, a bęben – perkusyjny. Każda rodzina wydaje dźwięk w inny sposób!",
                                   "The violin is a string instrument, the trumpet a wind instrument and the drum a percussion instrument. Each family makes sound in a different way!");
                case 24: return SL("Połowę talerza niech zajmą warzywa i owoce, a resztę – produkty zbożowe i białko. Do picia najlepsza jest woda!",
                                   "Fill half of your plate with vegetables and fruit, and the rest with grains and protein. Water is the best drink!");
                case 25: return SL("Piłka nożna, pływanie, jazda na rowerze i koszykówka to świetne sposoby na ruch. Który sport lubisz najbardziej?",
                                   "Football, swimming, cycling and basketball are great ways to keep moving. Which sport do you like best?");
                case 26: return SL("Dzieci potrzebują co najmniej 60 minut ruchu dziennie i około 9–10 godzin snu każdej nocy. Wtedy rosną zdrowo i lepiej się uczą!",
                                   "Children need at least 60 minutes of activity a day and about 9–10 hours of sleep every night. Then they grow healthy and learn better!");
                case 27: return SL("Wokół Słońca krąży 8 planet: Merkury, Wenus, Ziemia, Mars, Jowisz, Saturn, Uran i Neptun. Jowisz jest największy, a Saturn ma piękne pierścienie.",
                                   "8 planets orbit the Sun: Mercury, Venus, Earth, Mars, Jupiter, Saturn, Uranus and Neptune. Jupiter is the biggest and Saturn has beautiful rings.");
                case 28: return SL("Słońce ogrzewa wodę, która paruje i tworzy chmury. Z chmur pada deszcz, a rzeki niosą wodę z powrotem do morza.",
                                   "The Sun warms the water, which evaporates and forms clouds. Rain falls from the clouds and rivers carry the water back to the sea.");
                default: return SL("Różne bieguny magnesu (N i S) się przyciągają, a jednakowe odpychają. Gdy włącznik jest zamknięty, prąd z baterii płynie i żarówka świeci.",
                                   "Opposite poles of a magnet (N and S) attract and like poles repel. When the switch is closed, current flows from the battery and the bulb lights up.");
            }
        }

        // ------------------------------------------------------------------ teacher portraits

        /// <summary>
        /// The subject's old turtle teacher (front view, 170x210, transparent) with a dedicated outfit and
        /// a held item; even subjects are ladies, odd are gentlemen. talk = open mouth.
        /// </summary>
        public static Bitmap TeacherPortrait(int subject, bool talk)
        {
            int v = ((subject % 10) + 10) % 10;
            return Canvas(170, 210, g =>
            {
                const float s = 0.92f;
                g.TranslateTransform(85 * (1 - s), 210 * (1 - s));
                g.ScaleTransform(s, s);
                STeacher(g, v, talk);
            });
        }

        static void STeacher(Graphics g, int v, bool talk)
        {
            bool lady = v % 2 == 0;
            var skin = C(130, 180, 105);
            var skinD = C(85, 135, 70);
            using (var outline = P(C(50, 80, 40), 2.5f))
            {
                // wild white hair behind the head (science)
                if (v == 9)
                    using (var b = B(C(245, 245, 250)))
                        foreach (var r in new[] { new RectangleF(18, 26, 30, 22), new RectangleF(122, 26, 30, 22), new RectangleF(16, 44, 26, 20), new RectangleF(128, 44, 26, 20), new RectangleF(26, 12, 24, 20), new RectangleF(120, 12, 24, 20) })
                            g.FillEllipse(b, r);
                // pencil behind the ear (maths)
                if (v == 0)
                {
                    SLine(g, C(250, 200, 50), 6f, 22, 50, 50, 4);
                    SLine(g, C(240, 150, 160), 6f, 48, 7, 50, 4);
                    SFill(g, C(240, 210, 160), SP(19.5f, 48), SP(24.5f, 51), SP(17, 57));
                }
                // shell behind the body
                g.FillEllipse(B(C(110, 120, 60)), 22, 76, 126, 116);
                g.DrawEllipse(outline, 22, 76, 126, 116);
                // feet
                g.FillEllipse(B(skinD), 46, 188, 34, 20);
                g.FillEllipse(B(skinD), 90, 188, 34, 20);
                if (v == 8)
                {
                    FillRound(g, new RectangleF(46, 190, 34, 16), 7, Color.White);
                    FillRound(g, new RectangleF(90, 190, 34, 16), 7, Color.White);
                    SLine(g, C(220, 40, 40), 2f, 52, 197, 74, 197);
                    SLine(g, C(220, 40, 40), 2f, 96, 197, 118, 197);
                }
                // belly plate
                g.FillEllipse(B(C(240, 225, 170)), 58, 96, 54, 96);
                using (var pen = P(C(200, 180, 120), 1.5f))
                    for (int i = 0; i < 4; i++) g.DrawLine(pen, 64, 120 + i * 18, 106, 120 + i * 18);

                SOutfit(g, v);

                // arms (sleeves in the outfit colour) and hands
                Color sleeve = SSleeve(v, skin);
                bool leftUp = v == 1 || v == 7;
                if (leftUp) FillRoundGrad(g, new RectangleF(16, 104, 26, 50), 12, Shade(sleeve, 15), Shade(sleeve, -20), 0f);
                else FillRoundGrad(g, new RectangleF(18, 104, 24, 60), 12, Shade(sleeve, 15), Shade(sleeve, -20), 0f);
                FillRoundGrad(g, new RectangleF(128, 104, 24, 54), 12, Shade(sleeve, 15), Shade(sleeve, -20), 0f);
                if (v == 8)
                    using (var p = P(Color.White, 2f))
                    {
                        g.DrawLine(p, 26, 108, 26, 158);
                        g.DrawLine(p, 32, 108, 32, 160);
                        g.DrawLine(p, 138, 108, 138, 152);
                        g.DrawLine(p, 144, 108, 144, 152);
                    }
                var leftHand = leftUp ? new RectangleF(18, 142, 22, 16) : new RectangleF(19, 152, 20, 16);
                var rightHand = new RectangleF(130, 150, 20, 16);
                bool handFirst = v == 5 || v == 6 || v == 8;
                if (handFirst) g.FillEllipse(B(skin), rightHand);
                SHeld(g, v);
                if (!handFirst) g.FillEllipse(B(skin), rightHand);
                if (v == 7) SPalette(g);
                g.FillEllipse(B(skin), leftHand);
                if (v == 1) SQuill(g);

                // neck and head
                g.FillRectangle(B(skin), 68, 80, 34, 20);
                var head = new RectangleF(34, 8, 102, 84);
                using (var br = new LinearGradientBrush(head, Shade(skin, 15), skinD, 90f)) g.FillEllipse(br, head);
                g.DrawEllipse(outline, head);
                using (var pen = P(CA(140, 70, 110, 55), 1.8f))
                    for (int i = 0; i < 3; i++) g.DrawArc(pen, 62, 14 + i * 5, 46, 14, 200, 140);

                // hair bun for ladies without a hat
                if (lady && v != 2)
                {
                    g.FillEllipse(B(C(205, 205, 210)), 68, -2, 34, 26);
                    g.FillEllipse(B(C(225, 225, 230)), 74, 2, 14, 10);
                    g.FillEllipse(B(v == 8 ? C(220, 40, 40) : SSleeve(v, skin)), 64, 16, 10, 8);
                }
                SHeadwear(g, v, head);

                // eyes and glasses
                Eye(g, 66, 46, 9);
                Eye(g, 104, 46, 9);
                using (var pen = P(C(60, 45, 30), 3))
                {
                    g.DrawEllipse(pen, 52, 32, 28, 28);
                    g.DrawEllipse(pen, 90, 32, 28, 28);
                    g.DrawLine(pen, 80, 44, 90, 44);
                    g.DrawLine(pen, 52, 42, 40, 38);
                    g.DrawLine(pen, 118, 42, 130, 38);
                }
                using (var pen = P(Color.White, 5))
                {
                    g.DrawArc(pen, 50, 22, 30, 14, 200, 120);
                    g.DrawArc(pen, 90, 22, 30, 14, 220, 120);
                }
                g.FillEllipse(B(CA(110, 255, 130, 140)), 44, 58, 16, 10);
                g.FillEllipse(B(CA(110, 255, 130, 140)), 110, 58, 16, 10);
                g.FillEllipse(B(skinD), 80, 58, 4, 4);
                g.FillEllipse(B(skinD), 88, 58, 4, 4);
                if (talk)
                {
                    g.FillEllipse(B(C(120, 40, 40)), 74, 66, 22, 16);
                    g.FillEllipse(B(C(230, 110, 120)), 79, 74, 12, 7);
                }
                else using (var pen = P(C(60, 40, 30), 3)) g.DrawArc(pen, 70, 58, 30, 18, 20, 140);

                if (lady)
                {
                    if (v == 0 || v == 2 || v == 6)
                        for (int i = 0; i < 7; i++) g.FillEllipse(Brushes.White, 64 + i * 6, 94 + (float)Math.Sin(i / 6.0 * Math.PI) * 5, 6, 6);
                }
                else
                {
                    g.FillEllipse(B(Color.White), 70, 60, 16, 8);
                    g.FillEllipse(B(Color.White), 84, 60, 16, 8);
                    if (talk) g.FillRectangle(B(skin), 80, 66, 10, 2);
                }
                SNeckwear(g, v);
            }
        }

        static Color SSleeve(int v, Color skin)
        {
            switch (v)
            {
                case 0: return C(70, 120, 210);
                case 3: return C(170, 155, 100);
                case 4: return C(140, 80, 190);
                case 5: return C(240, 140, 40);
                case 6: return C(130, 85, 55);
                case 7: return C(200, 50, 150);
                case 8: return C(210, 40, 40);
                case 9: return C(245, 245, 248);
                default: return skin;
            }
        }

        static void SCardigan(Graphics g, Color coat, float w)
        {
            FillRoundGrad(g, new RectangleF(36, 92, w, 100), 12, Shade(coat, 25), Shade(coat, -30), 0f);
            FillRoundGrad(g, new RectangleF(134 - w, 92, w, 100), 12, Shade(coat, 25), Shade(coat, -30), 0f);
        }

        static void SOutfit(Graphics g, int v)
        {
            switch (v)
            {
                case 0: // blue cardigan
                    {
                        var coat = C(70, 120, 210);
                        SCardigan(g, coat, 30);
                        for (int i = 0; i < 3; i++) g.FillEllipse(B(C(250, 240, 210)), 58, 112 + i * 22, 6, 6);
                        break;
                    }
                case 1: // red waistcoat
                    {
                        var red = C(190, 40, 45);
                        var l = new[] { SP(38, 96), SP(64, 92), SP(84, 128), SP(84, 186), SP(46, 186), SP(38, 160) };
                        var r = new[] { SP(132, 96), SP(106, 92), SP(86, 128), SP(86, 186), SP(124, 186), SP(132, 160) };
                        using (var br = new LinearGradientBrush(new RectangleF(30, 90, 110, 100), Shade(red, 30), Shade(red, -30), 0f))
                        {
                            g.FillPolygon(br, l);
                            g.FillPolygon(br, r);
                        }
                        using (var p = P(C(120, 20, 25), 1.5f)) { g.DrawPolygon(p, l); g.DrawPolygon(p, r); g.DrawLine(p, 48, 160, 64, 160); g.DrawLine(p, 106, 160, 122, 160); }
                        for (int i = 0; i < 3; i++) SDisc(g, C(240, 200, 80), 80, 140 + i * 14, 3);
                        break;
                    }
                case 2: // green vest with a leaf brooch
                    {
                        var vest = C(80, 150, 70);
                        SCardigan(g, vest, 30);
                        var st = g.Save();
                        g.TranslateTransform(51, 118);
                        g.RotateTransform(-35);
                        g.FillEllipse(B(C(230, 200, 70)), -7, -4, 14, 8);
                        using (var p = P(C(160, 120, 20), 1f)) g.DrawLine(p, -7, 0, 7, 0);
                        g.Restore(st);
                        break;
                    }
                case 3: // khaki explorer jacket
                    {
                        var k = C(170, 155, 100);
                        SCardigan(g, k, 34);
                        foreach (var x in new[] { 40f, 112f })
                        {
                            FillRound(g, new RectangleF(x, 112, 18, 16), 2, Shade(k, -15));
                            FillRound(g, new RectangleF(x - 1, 110, 20, 6), 2, Shade(k, -35));
                            SDisc(g, C(90, 70, 40), x + 9, 114, 1.6f);
                        }
                        using (var b = B(C(110, 80, 45))) { g.FillRectangle(b, 36, 156, 34, 6); g.FillRectangle(b, 100, 156, 34, 6); }
                        break;
                    }
                case 4: // purple cardigan with a bus badge
                    {
                        var coat = C(140, 80, 190);
                        SCardigan(g, coat, 30);
                        for (int i = 0; i < 3; i++) g.FillEllipse(B(C(250, 240, 210)), 58, 112 + i * 22, 6, 6);
                        FillRound(g, new RectangleF(108, 116, 20, 12), 2, C(220, 35, 35));
                        using (var b = B(C(210, 235, 250))) for (int i = 0; i < 3; i++) { g.FillRectangle(b, 110 + i * 6, 118, 4, 3); g.FillRectangle(b, 110 + i * 6, 122.5f, 4, 2.5f); }
                        SDisc(g, SInk, 112, 128, 1.8f);
                        SDisc(g, SInk, 124, 128, 1.8f);
                        break;
                    }
                case 5: // orange sweater with a checkered vest
                    {
                        var o = C(240, 140, 40);
                        FillRoundGrad(g, new RectangleF(36, 90, 98, 100), 16, Shade(o, 20), Shade(o, -25), 0f);
                        var vest = new[] { SP(56, 96), SP(85, 120), SP(114, 96), SP(116, 186), SP(54, 186) };
                        var st = g.Save();
                        using (var path = new GraphicsPath())
                        {
                            path.AddPolygon(vest);
                            g.SetClip(path, CombineMode.Intersect);
                            g.FillRectangle(Brushes.White, 50, 90, 70, 100);
                            using (var b = B(C(30, 30, 35)))
                                for (int y = 0; y < 13; y++)
                                    for (int x = 0; x < 9; x++)
                                        if ((x + y) % 2 == 0) g.FillRectangle(b, 50 + x * 8, 94 + y * 8, 8, 8);
                        }
                        g.Restore(st);
                        using (var p = P(C(30, 30, 35), 1.5f)) g.DrawPolygon(p, vest);
                        break;
                    }
                case 6: // brown dress with an hourglass brooch
                    {
                        var d = C(130, 85, 55);
                        using (var path = new GraphicsPath())
                        {
                            path.AddPolygon(new[] { SP(56, 92), SP(114, 92), SP(128, 140), SP(140, 196), SP(30, 196), SP(42, 140) });
                            using (var br = new LinearGradientBrush(new RectangleF(30, 90, 110, 106), Shade(d, 25), Shade(d, -25), 0f)) g.FillPath(br, path);
                        }
                        using (var p = P(Shade(d, -40), 1.2f)) for (int i = 0; i < 4; i++) g.DrawLine(p, 60 + i * 16, 150, 52 + i * 22, 194);
                        SFill(g, C(245, 235, 215), SP(66, 92), SP(85, 104), SP(104, 92), SP(96, 92), SP(85, 98), SP(74, 92));
                        // hourglass brooch
                        SFill(g, C(240, 200, 80), SP(78, 112), SP(92, 112), SP(85, 120));
                        SFill(g, C(240, 200, 80), SP(78, 128), SP(92, 128), SP(85, 120));
                        SFill(g, C(250, 230, 160), SP(81, 128), SP(89, 128), SP(85, 123));
                        using (var p = P(C(140, 90, 30), 1.6f)) { g.DrawLine(p, 76, 111, 94, 111); g.DrawLine(p, 76, 129, 94, 129); }
                        break;
                    }
                case 7: // magenta artist smock with paint spots
                    {
                        var m = C(200, 50, 150);
                        using (var path = new GraphicsPath())
                        {
                            path.AddPolygon(new[] { SP(46, 90), SP(124, 90), SP(146, 196), SP(24, 196) });
                            using (var br = new LinearGradientBrush(new RectangleF(24, 90, 122, 106), Shade(m, 25), Shade(m, -25), 0f)) g.FillPath(br, path);
                        }
                        using (var p = P(Shade(m, -40), 1.2f)) { g.DrawLine(p, 70, 120, 62, 194); g.DrawLine(p, 100, 120, 108, 194); }
                        SDisc(g, C(255, 220, 50), 60, 150, 4);
                        SDisc(g, C(60, 140, 230), 108, 170, 3.5f);
                        SDisc(g, C(80, 200, 90), 92, 132, 3);
                        SDisc(g, Color.White, 74, 178, 2.5f);
                        break;
                    }
                case 8: // red tracksuit with white stripes
                    {
                        var r = C(210, 40, 40);
                        FillRoundGrad(g, new RectangleF(36, 90, 98, 100), 16, Shade(r, 20), Shade(r, -25), 0f);
                        using (var p = P(Color.White, 2.4f)) { g.DrawLine(p, 46, 100, 46, 186); g.DrawLine(p, 52, 100, 52, 186); g.DrawLine(p, 118, 100, 118, 186); g.DrawLine(p, 124, 100, 124, 186); }
                        using (var p = P(C(200, 200, 210), 1.6f)) g.DrawLine(p, 85, 100, 85, 188);
                        SFill(g, Color.White, SP(70, 92), SP(85, 104), SP(100, 92), SP(94, 92), SP(85, 98), SP(76, 92));
                        break;
                    }
                default: // white lab coat
                    {
                        var w = C(245, 245, 248);
                        var l = new[] { SP(30, 94), SP(72, 92), SP(76, 196), SP(26, 196) };
                        var rr = new[] { SP(140, 94), SP(98, 92), SP(94, 196), SP(144, 196) };
                        SFill(g, w, l);
                        SFill(g, w, rr);
                        using (var p = P(C(190, 190, 200), 1.4f)) { g.DrawPolygon(p, l); g.DrawPolygon(p, rr); }
                        SFill(g, C(225, 225, 232), SP(72, 92), SP(62, 104), SP(70, 116));
                        SFill(g, C(225, 225, 232), SP(98, 92), SP(108, 104), SP(100, 116));
                        FillRound(g, new RectangleF(40, 124, 22, 16), 2, C(232, 232, 238));
                        SLine(g, C(60, 110, 220), 2.4f, 46, 116, 46, 128);
                        SLine(g, C(220, 50, 50), 2.4f, 52, 118, 52, 128);
                        break;
                    }
            }
        }

        static void SHeld(Graphics g, int v)
        {
            switch (v)
            {
                case 0: // wooden set-square
                    {
                        using (var path = new GraphicsPath())
                        {
                            path.AddPolygon(new[] { SP(100, 196), SP(160, 196), SP(160, 114) });
                            path.AddPolygon(new[] { SP(124, 188), SP(152, 188), SP(152, 150) });
                            path.FillMode = FillMode.Alternate;
                            using (var br = new LinearGradientBrush(new RectangleF(100, 114, 60, 82), C(235, 195, 120), C(190, 140, 70), 45f)) g.FillPath(br, path);
                            using (var p = P(C(130, 85, 40), 1.6f)) g.DrawPath(p, path);
                        }
                        using (var p = P(C(120, 80, 40), 1f))
                            for (int i = 0; i < 9; i++) g.DrawLine(p, 106 + i * 6, 196, 106 + i * 6, i % 2 == 0 ? 191 : 193);
                        break;
                    }
                case 1: // big open book
                    {
                        SFill(g, C(150, 30, 35), SP(102, 144), SP(132, 149), SP(162, 144), SP(162, 176), SP(132, 180), SP(102, 176));
                        SFill(g, C(252, 250, 240), SP(105, 146), SP(132, 150), SP(132, 176), SP(105, 172));
                        SFill(g, C(245, 242, 230), SP(132, 150), SP(159, 146), SP(159, 172), SP(132, 176));
                        using (var p = P(C(150, 150, 160), 1f))
                            for (int i = 0; i < 4; i++) { g.DrawLine(p, 109, 153 + i * 5, 128, 156 + i * 5); g.DrawLine(p, 136, 156 + i * 5, 155, 153 + i * 5); }
                        SLine(g, C(120, 100, 80), 1.2f, 132, 150, 132, 176);
                        break;
                    }
                case 2: // magnifying glass
                    SLine(g, C(120, 70, 30), 6f, 146, 140, 136, 172);
                    SDisc(g, CA(120, 190, 230, 255), 150, 124, 15);
                    using (var p = P(C(190, 150, 50), 4f)) g.DrawEllipse(p, 135, 109, 30, 30);
                    using (var p = P(CA(200, 255, 255, 255), 2f)) g.DrawArc(p, 140, 114, 20, 20, 200, 60);
                    break;
                case 3: // rolled map
                    using (var p = new Pen(C(235, 220, 170), 15f)) g.DrawLine(p, 112, 178, 160, 128);
                    using (var p = P(C(170, 150, 100), 1.2f))
                    {
                        g.DrawLine(p, 107, 173, 155, 123);
                        g.DrawLine(p, 117, 183, 165, 133);
                    }
                    g.FillEllipse(B(C(225, 205, 150)), 154, 121, 13, 13);
                    using (var p = P(C(150, 120, 70), 1f)) { g.DrawEllipse(p, 154, 121, 13, 13); g.DrawEllipse(p, 157, 124, 7, 7); }
                    using (var p = new Pen(C(200, 40, 40), 3f)) g.DrawLine(p, 127, 152, 137, 162);
                    break;
                case 4: // red dictionary "ABC"
                    FillRound(g, new RectangleF(116, 134, 46, 36), 4, C(200, 35, 40));
                    FillRound(g, new RectangleF(116, 134, 8, 36), 3, C(150, 20, 25));
                    g.FillRectangle(B(C(250, 245, 225)), 158, 137, 3, 30);
                    TextIn(g, "ABC", new RectangleF(124, 138, 34, 18), 15, Color.White);
                    using (var p = P(C(255, 220, 100), 1.2f)) g.DrawLine(p, 128, 160, 154, 160);
                    break;
                case 5: // chess knight
                    SPiece(g, 4, 142, 162, 1.8f, false);
                    break;
                case 6: // gold pocket watch on a chain
                    using (var p = new Pen(C(230, 190, 60), 1.8f) { DashStyle = DashStyle.Dot })
                        g.DrawBezier(p, 84, 132, 100, 156, 120, 150, 136, 140);
                    SDisc(g, C(230, 190, 60), 146, 134, 3);
                    using (var br = new LinearGradientBrush(new RectangleF(130, 136, 32, 32), C(255, 225, 110), C(190, 140, 30), 45f)) g.FillEllipse(br, 130, 136, 32, 32);
                    SDisc(g, C(255, 252, 240), 146, 152, 12);
                    using (var p = P(SInk, 1f))
                        for (int i = 0; i < 12; i++)
                        {
                            double a = i * Math.PI / 6;
                            g.DrawLine(p, 146 + (float)Math.Cos(a) * 9.5f, 152 + (float)Math.Sin(a) * 9.5f, 146 + (float)Math.Cos(a) * 11, 152 + (float)Math.Sin(a) * 11);
                        }
                    SLine(g, SInk, 2f, 146, 152, 146, 145);
                    SLine(g, SInk, 1.4f, 146, 152, 153, 152);
                    break;
                case 7: // paint brush
                    SLine(g, C(150, 90, 40), 4f, 128, 178, 154, 126);
                    SLine(g, C(190, 190, 200), 5f, 154, 126, 158, 118);
                    SFill(g, C(60, 120, 220), SP(155.5f, 119), SP(160.5f, 121), SP(163, 108));
                    break;
                case 8: // ball
                    KBall(g, 146, 146, 16);
                    break;
                default: // flask with green liquid
                    {
                        var flask = new[] { SP(138, 112), SP(150, 112), SP(150, 132), SP(164, 168), SP(124, 168), SP(138, 132) };
                        SFill(g, CA(120, 220, 240, 255), flask);
                        SFill(g, C(110, 220, 80), SP(142.6f, 144), SP(145.4f, 144), SP(164, 168), SP(124, 168));
                        SFill(g, C(110, 220, 80), SP(131.5f, 150), SP(156.5f, 150), SP(164, 168), SP(124, 168));
                        using (var p = P(C(120, 150, 170), 1.6f)) g.DrawPolygon(p, flask);
                        SDisc(g, C(200, 255, 170), 144, 158, 2.5f);
                        SDisc(g, C(200, 255, 170), 150, 154, 1.6f);
                        SDisc(g, CA(200, 200, 255, 170), 145, 104, 2.4f);
                        SDisc(g, CA(160, 200, 255, 170), 150, 98, 1.8f);
                        using (var p = P(CA(200, 255, 255, 255), 1.6f)) g.DrawLine(p, 141, 116, 141, 132);
                        break;
                    }
            }
        }

        static void SPalette(Graphics g)
        {
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(0, 128, 46, 34);
                path.AddEllipse(8, 150, 9, 7);
                path.FillMode = FillMode.Alternate;
                using (var b = B(C(225, 190, 130))) g.FillPath(b, path);
                using (var p = P(C(150, 110, 60), 1.4f)) g.DrawPath(p, path);
            }
            Color[] paints = { C(230, 40, 40), C(250, 210, 40), C(60, 120, 230), C(70, 180, 70), Color.White };
            float[][] pos = { new[] { 10f, 136f }, new[] { 22f, 132f }, new[] { 34f, 136f }, new[] { 38f, 148f }, new[] { 26f, 154f } };
            for (int i = 0; i < paints.Length; i++) SDisc(g, paints[i], pos[i][0], pos[i][1], 4);
        }

        static void SQuill(Graphics g)
        {
            var st = g.Save();
            g.TranslateTransform(22, 120);
            g.RotateTransform(-20);
            using (var b = B(C(250, 250, 252))) g.FillEllipse(b, -6, -28, 12, 38);
            using (var p = P(C(170, 170, 185), 1.2f))
            {
                g.DrawEllipse(p, -6, -28, 12, 38);
                g.DrawLine(p, 0, -26, 0, 26);
                for (int i = 0; i < 5; i++) { g.DrawLine(p, 0, -20 + i * 6, -4, -24 + i * 6); g.DrawLine(p, 0, -20 + i * 6, 4, -24 + i * 6); }
            }
            SLine(g, C(60, 50, 40), 1.6f, 0, 18, 0, 28);
            g.Restore(st);
        }

        static void SHeadwear(Graphics g, int v, RectangleF head)
        {
            switch (v)
            {
                case 1:
                case 7: // beret
                    {
                        var c = v == 1 ? C(55, 45, 75) : C(25, 25, 30);
                        var st = g.Save();
                        g.TranslateTransform(85, 10);
                        g.RotateTransform(-8);
                        using (var br = new LinearGradientBrush(new RectangleF(-50, -16, 100, 32), Shade(c, 40), c, 90f)) g.FillEllipse(br, -50, -16, 100, 30);
                        using (var p = P(Shade(c, 60), 1.2f)) g.DrawArc(p, -46, -12, 92, 24, 20, 140);
                        g.FillEllipse(B(c), -3, -20, 6, 7);
                        g.Restore(st);
                        break;
                    }
                case 2: // straw sun hat with a flower
                    {
                        var straw = C(235, 205, 130);
                        g.FillEllipse(B(Shade(straw, -10)), 8, 2, 154, 24);
                        using (var br = new LinearGradientBrush(new RectangleF(46, -16, 78, 32), Shade(straw, 20), Shade(straw, -15), 90f))
                        using (var path = new GraphicsPath())
                        {
                            path.AddArc(48, -16, 74, 50, 180, 180);
                            path.CloseFigure();
                            g.FillPath(br, path);
                        }
                        using (var b = B(C(80, 160, 80))) g.FillRectangle(b, 48, 4, 74, 6);
                        using (var p = P(Shade(straw, -40), 1f)) g.DrawEllipse(p, 8, 2, 154, 24);
                        for (int i = 0; i < 5; i++)
                        {
                            double a = i * Math.PI * 2 / 5;
                            SDisc(g, C(240, 110, 160), 116 + (float)Math.Cos(a) * 5, 6 + (float)Math.Sin(a) * 5, 4);
                        }
                        SDisc(g, C(255, 220, 60), 116, 6, 3);
                        break;
                    }
                case 3: // pith helmet
                    {
                        var c = C(225, 205, 160);
                        g.FillEllipse(B(Shade(c, -15)), 22, 8, 126, 16);
                        using (var br = new LinearGradientBrush(new RectangleF(40, -20, 90, 40), Shade(c, 20), Shade(c, -20), 90f))
                        using (var path = new GraphicsPath())
                        {
                            path.AddArc(40, -20, 90, 68, 180, 180);
                            path.CloseFigure();
                            g.FillPath(br, path);
                        }
                        using (var b = B(C(120, 95, 60))) g.FillRectangle(b, 40, 6, 90, 6);
                        using (var p = P(Shade(c, -50), 1.2f)) g.DrawEllipse(p, 22, 8, 126, 16);
                        SDisc(g, Shade(c, -10), 85, -18, 3);
                        break;
                    }
                case 8: // white headband
                    {
                        var st = g.Save();
                        using (var path = new GraphicsPath())
                        {
                            path.AddEllipse(head);
                            g.SetClip(path, CombineMode.Intersect);
                            g.FillRectangle(Brushes.White, head.X, 14, head.Width, 9);
                            using (var b = B(C(220, 40, 40))) g.FillRectangle(b, head.X, 17.5f, head.Width, 2);
                        }
                        g.Restore(st);
                        break;
                    }
                case 9: // wild hair on top and safety goggles on the forehead
                    {
                        using (var b = B(C(245, 245, 250)))
                            foreach (var r in new[] { new RectangleF(56, -2, 16, 16), new RectangleF(72, -6, 18, 18), new RectangleF(90, -2, 16, 16) })
                                g.FillEllipse(b, r);
                        var st = g.Save();
                        using (var path = new GraphicsPath())
                        {
                            path.AddEllipse(head);
                            g.SetClip(path, CombineMode.Intersect);
                            g.FillRectangle(B(C(60, 60, 70)), head.X, 13, head.Width, 5);
                        }
                        g.Restore(st);
                        foreach (var x in new[] { 60f, 88f })
                        {
                            FillRound(g, new RectangleF(x, 6, 22, 16), 7, C(70, 150, 200));
                            FillRound(g, new RectangleF(x + 2.5f, 8.5f, 17, 11), 5, CA(200, 190, 235, 255));
                            g.FillEllipse(B(CA(200, 255, 255, 255)), x + 5, 10, 6, 3);
                        }
                        break;
                    }
            }
        }

        static void SNeckwear(Graphics g, int v)
        {
            switch (v)
            {
                case 1: // dark bow tie
                case 5: // checkered bow tie
                    {
                        var c = v == 1 ? C(30, 30, 50) : C(30, 30, 35);
                        SFill(g, c, SP(85, 98), SP(70, 90), SP(70, 106));
                        SFill(g, c, SP(85, 98), SP(100, 90), SP(100, 106));
                        if (v == 5)
                            using (var b = B(Color.White))
                            {
                                g.FillRectangle(b, 73, 93, 4, 4); g.FillRectangle(b, 77, 98, 4, 4);
                                g.FillRectangle(b, 93, 93, 4, 4); g.FillRectangle(b, 89, 98, 4, 4);
                            }
                        g.FillEllipse(B(v == 1 ? C(70, 70, 100) : Color.White), 81, 94, 8, 8);
                        break;
                    }
                case 3: // compass on a cord
                    using (var p = P(C(110, 80, 45), 1.4f)) { g.DrawLine(p, 66, 92, 85, 114); g.DrawLine(p, 104, 92, 85, 114); }
                    SDisc(g, C(200, 160, 60), 85, 122, 9);
                    SDisc(g, C(250, 248, 235), 85, 122, 6.5f);
                    SFill(g, C(220, 40, 40), SP(83.5f, 122), SP(85, 116.5f), SP(86.5f, 122));
                    SFill(g, C(60, 60, 80), SP(83.5f, 122), SP(85, 127.5f), SP(86.5f, 122));
                    break;
                case 4: // red scarf
                    FillRound(g, new RectangleF(60, 86, 50, 14), 7, C(210, 40, 45));
                    SFill(g, C(190, 30, 35), SP(88, 94), SP(100, 94), SP(104, 130), SP(92, 132));
                    SFill(g, C(220, 50, 55), SP(96, 94), SP(108, 96), SP(114, 124), SP(103, 126));
                    using (var p = P(C(255, 200, 200), 1f)) { g.DrawLine(p, 92, 128, 103, 126); g.DrawLine(p, 103, 122, 113, 120); }
                    break;
                case 7: // floppy black artist bow
                    SFill(g, C(25, 25, 30), SP(85, 98), SP(66, 88), SP(64, 108));
                    SFill(g, C(25, 25, 30), SP(85, 98), SP(104, 88), SP(106, 108));
                    SFill(g, C(25, 25, 30), SP(82, 100), SP(76, 122), SP(84, 118));
                    SFill(g, C(25, 25, 30), SP(88, 100), SP(94, 124), SP(86, 118));
                    SDisc(g, C(60, 60, 70), 85, 98, 4);
                    break;
                case 8: // whistle on a lanyard
                    using (var p = P(C(40, 90, 200), 1.6f)) { g.DrawLine(p, 66, 92, 85, 120); g.DrawLine(p, 104, 92, 85, 120); }
                    FillRoundGrad(g, new RectangleF(78, 119, 18, 10), 4, C(240, 240, 245), C(160, 160, 170), 90f);
                    g.FillRectangle(B(C(160, 160, 170)), 94, 120, 6, 5);
                    SDisc(g, C(90, 90, 100), 84, 124, 2);
                    break;
                case 9: // tie under the lab coat
                    SFill(g, C(50, 70, 150), SP(81, 96), SP(89, 96), SP(87, 100), SP(91, 126), SP(85, 132), SP(79, 126), SP(83, 100));
                    break;
            }
        }

        // ------------------------------------------------------------------ floor blackboards

        /// <summary>
        /// A blackboard spanning two wall cells (like Blackboard) with chalk content for subject that changes
        /// with the floor (0..7), suitable for 8–11-year-olds.
        /// </summary>
        public static Tex[] FloorBlackboard(FloorTheme th, int subject, string title, int floor)
        {
            int s = ((subject % 10) + 10) % 10, f = ((floor % 8) + 8) % 8;
            using (var bmp = Canvas(TW * 2, TH, g =>
            {
                WallBase(g, th, TW * 2, 20 + s);
                var board = new RectangleF(12, 14, 232, 100);
                g.FillRectangle(B(C(130, 85, 45)), board.X - 6, board.Y - 6, board.Width + 12, board.Height + 12);
                Grad(g, board, C(40, 85, 60), C(30, 70, 50), 90f, false);
                var r = new Random(s * 8 + f);
                for (int i = 0; i < 60; i++) g.FillEllipse(B(CA(25, 255, 255, 255)), board.X + F(r) * board.Width, board.Y + F(r) * board.Height, 8, 3);
                Grad(g, new RectangleF(board.X - 4, board.Bottom + 4, board.Width + 8, 6), C(150, 100, 55), C(100, 65, 35), 90f, false);
                g.FillRectangle(Brushes.White, 60 + f * 6, board.Bottom + 1, 12, 4);
                g.FillRectangle(B(C(255, 220, 120)), 84 + f * 4, board.Bottom + 1, 10, 4);
                FillRound(g, new RectangleF(180, board.Bottom + 1, 20, 5), 2, C(90, 70, 160));

                var chalk = CA(235, 250, 250, 245);
                TextIn(g, title, new RectangleF(board.X + 4, board.Y + 2, board.Width - 8, 22), 19, chalk);
                using (var p = P(CA(120, 250, 250, 245), 1f)) g.DrawLine(p, board.X + 60, board.Y + 24, board.Right - 60, board.Y + 24);
                var a = new RectangleF(board.X + 8, board.Y + 28, board.Width - 16, board.Height - 31);
                int sketch;
                var lines = SBoardLines(s, f, out sketch);
                var text = a;
                if (sketch == 1 || sketch == 6) // full-width sketches
                {
                    SChalkSketch(g, sketch, a, chalk);
                    text = RectangleF.Empty;
                }
                else if (sketch > 0)
                {
                    SChalkSketch(g, sketch, new RectangleF(a.X, a.Y, 78, a.Height), chalk);
                    text = new RectangleF(a.X + 82, a.Y, a.Width - 82, a.Height);
                }
                if (lines != null && text.Width > 0)
                {
                    Color[] cols = { chalk, CA(235, 255, 230, 120), CA(235, 255, 165, 195) };
                    float lh = text.Height / lines.Length;
                    for (int i = 0; i < lines.Length; i++)
                        TextIn(g, lines[i], new RectangleF(text.X, text.Y + i * lh, text.Width, lh), lines.Length > 2 ? 17 : 21, cols[i % 3]);
                }
            }))
                return Tex.Split(bmp, 2);
        }

        /// <summary>Chalk lines for a subject board on a floor; sketch: 0 none, 1 food chain (full width), 2 tree, 3 clock, 4 staff, 5 planets, 6 shapes, 7 compass rose, 8 knight move, 9 rectangle.</summary>
        static string[] SBoardLines(int s, int f, out int sketch)
        {
            sketch = 0;
            bool pl = L.Pl;
            switch (s)
            {
                case 0:
                    switch (f)
                    {
                        case 0: return new[] { "7 × 8 = 56", "6 × 9 = 54" };
                        case 1: return new[] { "3/4 > 1/2", "1/2 = 2/4" };
                        case 2: sketch = 9; return pl ? new[] { "P = a × b", "Obw. = 2a + 2b" } : new[] { "A = a × b", "P = 2a + 2b" };
                        case 3: return new[] { "125 + 375 = 500", "1000 − 1 = 999" };
                        case 4: return new[] { pl ? "72 : 8 = 9" : "72 ÷ 8 = 9", pl ? "100 : 4 = 25" : "100 ÷ 4 = 25" };
                        case 5: return new[] { "1 m = 100 cm", "1 kg = 1000 g" };
                        case 6: return new[] { pl ? "0,5 = 1/2" : "0.5 = 1/2", "25% = 1/4" };
                        default: return new[] { "2 + 3 × 4 = 14", "(2 + 3) × 4 = 20" };
                    }
                case 1:
                    if (pl)
                        switch (f)
                        {
                            case 0: return new[] { "rzeczownik – kto? co?", "czasownik – co robi?" };
                            case 1: return new[] { "przymiotnik – jaki?", "duży, wesoły, zielony" };
                            case 2: return new[] { "ó – góra, stół", "u – ul, mucha" };
                            case 3: return new[] { "rz – rzeka, morze", "ż – żaba, może" };
                            case 4: return new[] { "ch – chleb, ucho", "h – herbata, hak" };
                            case 5: return new[] { "Wielka litera:", "Kórnik, Polska, Ola" };
                            case 6: return new[] { "samogłoski:", "a ą e ę i o u y" };
                            default: return new[] { "Zdanie kończy się", "kropką . ! ?" };
                        }
                    switch (f)
                    {
                        case 0: return new[] { "noun – a thing: cat", "verb – an action: run" };
                        case 1: return new[] { "adjective – describes:", "big, happy, green" };
                        case 2: return new[] { "i before e:", "friend, believe" };
                        case 3: return new[] { "except after c:", "receive, ceiling" };
                        case 4: return new[] { "Capital letters:", "London, Monday, Tom" };
                        case 5: return new[] { "a cat, a dog", "an apple, an egg" };
                        case 6: return new[] { "vowels:", "a  e  i  o  u" };
                        default: return new[] { "one mouse – two mice", "one child – two children" };
                    }
                case 2:
                    switch (f)
                    {
                        case 0: sketch = 1; return null;
                        case 1: return pl ? new[] { "korzeń, łodyga,", "liść, kwiat" } : new[] { "root, stem,", "leaf, flower" };
                        case 2: return pl ? new[] { "jajo → gąsienica →", "poczwarka → motyl" } : new[] { "egg → caterpillar →", "pupa → butterfly" };
                        case 3: return pl ? new[] { "Rośliny potrzebują:", "wody, światła", "i powietrza" } : new[] { "Plants need:", "water, light, air" };
                        case 4: return pl ? new[] { "ssaki:", "pies, kot, wieloryb" } : new[] { "mammals:", "dog, cat, whale" };
                        case 5: return pl ? new[] { "owad – 6 nóg", "pająk – 8 nóg" } : new[] { "insect – 6 legs", "spider – 8 legs" };
                        case 6: sketch = 2; return pl ? new[] { "dąb, buk,", "sosna, lipa" } : new[] { "oak, beech,", "pine, lime" };
                        default: return pl ? new[] { "kijanka → żaba" } : new[] { "tadpole → frog" };
                    }
                case 3:
                    switch (f)
                    {
                        case 0: return pl ? new[] { "966 – chrzest", "1410 – Grunwald", "1918 – niepodległość" } : new[] { "966 – baptism", "1410 – Grunwald", "1918 – independence" };
                        case 1: sketch = 7; return pl ? new[] { "N – północ", "S – południe" } : new[] { "N – north", "S – south" };
                        case 2: return pl ? new[] { "Stolica Polski:", "Warszawa" } : new[] { "Capital of Poland:", "Warsaw" };
                        case 3: return pl ? new[] { "Najdłuższa rzeka:", "Wisła" } : new[] { "Longest river:", "the Vistula" };
                        case 4: return pl ? new[] { "Rysy – 2499 m n.p.m.", "najwyższy szczyt Polski" } : new[] { "Rysy – 2499 m", "Poland's highest peak" };
                        case 5: return pl ? new[] { "1791", "Konstytucja 3 Maja" } : new[] { "1791", "Constitution of 3 May" };
                        case 6: return pl ? new[] { "Kórnik:", "zamek i arboretum" } : new[] { "Kórnik:", "castle and arboretum" };
                        default: return pl ? new[] { "7 kontynentów", "5 oceanów" } : new[] { "7 continents", "5 oceans" };
                    }
                case 4:
                    {
                        string[][] en =
                        {
                            new[] { "I am, you are,", "he is, she is" },
                            new[] { "Monday, Tuesday,", "Wednesday" },
                            new[] { "one, two, three,", "four, five" },
                            new[] { "red, blue,", "green, yellow" },
                            new[] { "dog = pies", "cat = kot" },
                            new[] { "How are you?", "I'm fine, thank you!" },
                            new[] { "apple, banana,", "orange" },
                            new[] { "Good morning!", "Good night!" },
                        };
                        string[][] plw =
                        {
                            new[] { "ja jestem, ty jesteś,", "on jest, ona jest" },
                            new[] { "poniedziałek, wtorek,", "środa" },
                            new[] { "jeden, dwa, trzy,", "cztery, pięć" },
                            new[] { "czerwony, niebieski,", "zielony, żółty" },
                            new[] { "pies = dog", "kot = cat" },
                            new[] { "Jak się masz?", "Dobrze, dziękuję!" },
                            new[] { "jabłko, banan,", "pomarańcza" },
                            new[] { "Dzień dobry!", "Dobranoc!" },
                        };
                        return (pl ? en : plw)[f];
                    }
                case 5:
                    switch (f)
                    {
                        case 0: return new[] { "1, 1, 2, 3, 5, 8, ?" };
                        case 1: return new[] { "2, 4, 8, 16, ?" };
                        case 2: return new[] { "1, 4, 9, 16, ?" };
                        case 3: return new[] { "A, C, E, G, ?" };
                        case 4: sketch = 6; return null;
                        case 5: return pl ? new[] { "A > B  i  B > C", "więc  A > C" } : new[] { "A > B  and  B > C", "so  A > C" };
                        case 6: return new[] { "10, 7, 4, ?" };
                        default: sketch = 8; return pl ? new[] { "Skoczek", "chodzi jak L" } : new[] { "The knight", "moves in an L" };
                    }
                case 6:
                    switch (f)
                    {
                        case 0: return new[] { "1 h = 60 min", "= 3600 s" };
                        case 1: return pl ? new[] { "1 doba = 24 h" } : new[] { "1 day = 24 h" };
                        case 2: return pl ? new[] { "1 rok = 12 miesięcy", "= 365 dni" } : new[] { "1 year = 12 months", "= 365 days" };
                        case 3: return pl ? new[] { "1 tydzień = 7 dni" } : new[] { "1 week = 7 days" };
                        case 4: sketch = 3; return pl ? new[] { "15:30", "wpół do czwartej" } : new[] { "15:30", "half past three" };
                        case 5: return pl ? new[] { "rok przestępny:", "366 dni" } : new[] { "leap year:", "366 days" };
                        case 6: return pl ? new[] { "kwadrans = 15 min" } : new[] { "a quarter of an hour", "= 15 min" };
                        default: return pl ? new[] { "wiek = 100 lat" } : new[] { "a century = 100 years" };
                    }
                case 7:
                    switch (f)
                    {
                        case 0: sketch = 4; return pl ? new[] { "do re mi", "fa sol la si" } : new[] { "do re mi", "fa sol la ti" };
                        case 1: return pl ? new[] { "cała nuta =", "4 ćwierćnuty" } : new[] { "1 whole note =", "4 quarter notes" };
                        case 2: return pl ? new[] { "barwy podstawowe:", "czerwony, żółty,", "niebieski" } : new[] { "primary colours:", "red, yellow, blue" };
                        case 3: return pl ? new[] { "żółty + niebieski", "= zielony" } : new[] { "yellow + blue", "= green" };
                        case 4: return pl ? new[] { "forte = głośno", "piano = cicho" } : new[] { "forte = loud", "piano = quiet" };
                        case 5: return pl ? new[] { "Fryderyk Chopin", "ur. 1810", "w Żelazowej Woli" } : new[] { "Frédéric Chopin", "born 1810", "in Poland" };
                        case 6: return pl ? new[] { "czerwony + żółty", "= pomarańczowy" } : new[] { "red + yellow", "= orange" };
                        default: sketch = 4; return new[] { "C D E F G A " + (pl ? "H" : "B") };
                    }
                case 8:
                    switch (f)
                    {
                        case 0: return pl ? new[] { "60 min ruchu", "dziennie!" } : new[] { "60 min of activity", "every day!" };
                        case 1: return pl ? new[] { "9–10 h snu" } : new[] { "9–10 h of sleep" };
                        case 2: return pl ? new[] { "Pij wodę!" } : new[] { "Drink water!" };
                        case 3: return pl ? new[] { "5 porcji", "warzyw i owoców" } : new[] { "5 portions", "of fruit & veg" };
                        case 4: return pl ? new[] { "Piłka nożna:", "11 zawodników" } : new[] { "Football:", "11 players" };
                        case 5: return pl ? new[] { "Koszykówka:", "5 zawodników" } : new[] { "Basketball:", "5 players" };
                        case 6: return pl ? new[] { "Myj ręce!" } : new[] { "Wash your hands!" };
                        default: return pl ? new[] { "Rozgrzewka", "przed sportem!" } : new[] { "Warm up", "before sport!" };
                    }
                default:
                    switch (f)
                    {
                        case 0: return pl ? new[] { "H2O = woda", "wrze: 100 °C", "zamarza: 0 °C" } : new[] { "H2O = water", "boils: 100 °C", "freezes: 0 °C" };
                        case 1: return pl ? new[] { "Ziemia krąży", "wokół Słońca" } : new[] { "The Earth orbits", "the Sun" };
                        case 2: sketch = 5; return pl ? new[] { "8 planet" } : new[] { "8 planets" };
                        case 3: return pl ? new[] { "Magnes:", "N przyciąga S" } : new[] { "Magnet:", "N attracts S" };
                        case 4: return pl ? new[] { "Księżyc okrąża Ziemię", "w ok. 27 dni" } : new[] { "The Moon orbits the Earth", "in about 27 days" };
                        case 5: return pl ? new[] { "Światło jest szybsze", "niż dźwięk" } : new[] { "Light is faster", "than sound" };
                        case 6: return pl ? new[] { "ciało stałe,", "ciecz, gaz" } : new[] { "solid, liquid,", "gas" };
                        default: return pl ? new[] { "Ziemia obraca się", "raz na dobę (24 h)" } : new[] { "The Earth spins", "once a day (24 h)" };
                    }
            }
        }

        static void SChalkSketch(Graphics g, int kind, RectangleF a, Color chalk)
        {
            var yellow = CA(235, 255, 230, 120);
            var pink = CA(235, 255, 165, 195);
            float cx = a.X + a.Width / 2, cy = a.Y + a.Height / 2;
            using (var pen = P(chalk, 2.2f))
            using (var yp = P(yellow, 2.2f))
            using (var pp = P(pink, 2.2f))
            {
                switch (kind)
                {
                    case 1: // food chain: grass -> rabbit -> fox
                        {
                            float y = a.Y + 36;
                            float gx = a.X + 30, rx = cx, fx = a.Right - 34;
                            using (var gp = P(CA(235, 150, 230, 130), 2.2f))
                                for (int i = 0; i < 6; i++) g.DrawLine(gp, gx - 12 + i * 5, y, gx - 13 + i * 5 + (i % 2) * 4, y - 14 - (i % 3) * 4);
                            g.DrawEllipse(pen, rx - 12, y - 14, 18, 14);
                            g.DrawEllipse(pen, rx + 3, y - 22, 11, 10);
                            g.DrawEllipse(pen, rx + 4, y - 36, 4, 15);
                            g.DrawEllipse(pen, rx + 9, y - 35, 4, 14);
                            using (var op = P(CA(235, 255, 180, 110), 2.2f))
                            {
                                g.DrawEllipse(op, fx - 6, y - 14, 18, 11);
                                g.DrawPolygon(op, new[] { SP(fx - 6, y - 12), SP(fx - 16, y - 16), SP(fx - 9, y - 24), SP(fx - 6, y - 18) });
                                g.DrawBezier(op, fx + 12, y - 9, fx + 22, y - 10, fx + 24, y - 20, fx + 18, y - 24);
                                g.DrawLine(op, fx - 2, y - 4, fx - 2, y);
                                g.DrawLine(op, fx + 8, y - 4, fx + 8, y);
                            }
                            SArrow(g, yellow, 2.2f, gx + 18, y - 10, rx - 20, y - 10);
                            SArrow(g, yellow, 2.2f, rx + 22, y - 10, fx - 22, y - 10);
                            string[] n = L.Pl ? new[] { "trawa", "zając", "lis" } : new[] { "grass", "rabbit", "fox" };
                            TextIn(g, n[0], new RectangleF(gx - 30, y + 2, 60, 22), 18, chalk);
                            TextIn(g, n[1], new RectangleF(rx - 30, y + 2, 60, 22), 18, chalk);
                            TextIn(g, n[2], new RectangleF(fx - 30, y + 2, 60, 22), 18, chalk);
                            break;
                        }
                    case 2: // tree
                        g.DrawLine(pen, cx - 4, a.Bottom - 2, cx - 4, cy);
                        g.DrawLine(pen, cx + 4, a.Bottom - 2, cx + 4, cy);
                        using (var gp = P(CA(235, 150, 230, 130), 2.2f))
                        {
                            g.DrawEllipse(gp, cx - 26, a.Y + 2, 30, 26);
                            g.DrawEllipse(gp, cx - 6, a.Y, 32, 28);
                            g.DrawEllipse(gp, cx - 18, a.Y + 14, 36, 24);
                        }
                        break;
                    case 3: // clock 3:30
                        {
                            float r = Math.Min(a.Height / 2 - 2, 30);
                            g.DrawEllipse(pen, cx - r, cy - r, 2 * r, 2 * r);
                            for (int i = 0; i < 12; i++)
                            {
                                double ang = i * Math.PI / 6;
                                g.DrawLine(pen, cx + (float)Math.Cos(ang) * (r - 5), cy + (float)Math.Sin(ang) * (r - 5), cx + (float)Math.Cos(ang) * (r - 2), cy + (float)Math.Sin(ang) * (r - 2));
                            }
                            double ha = -Math.PI / 2 + 3.5 * Math.PI / 6;
                            g.DrawLine(yp, cx, cy, cx + (float)Math.Cos(ha) * r * 0.5f, cy + (float)Math.Sin(ha) * r * 0.5f);
                            g.DrawLine(pp, cx, cy, cx, cy + r * 0.78f);
                            break;
                        }
                    case 4: // staff with notes
                        {
                            for (int i = 0; i < 5; i++) g.DrawLine(pen, a.X, a.Y + 10 + i * 8, a.Right, a.Y + 10 + i * 8);
                            float bottom = a.Y + 42;
                            for (int i = 0; i < 5; i++)
                            {
                                float x = a.X + 12 + i * 14, y = bottom + 8 - i * 4;
                                using (var b = B(i % 2 == 0 ? chalk : yellow)) g.FillEllipse(b, x - 5, y - 3.5f, 10, 7);
                                g.DrawLine(pen, x + 4.5f, y, x + 4.5f, y - 22);
                                if (i == 0) g.DrawLine(pen, x - 8, y, x + 8, y);
                            }
                            break;
                        }
                    case 5: // sun and planets
                        g.DrawArc(yp, a.X - 30, cy - 30, 60, 60, -70, 140);
                        g.DrawEllipse(pen, a.X + 34, cy - 4, 8, 8);
                        g.DrawEllipse(pp, a.X + 48, cy - 7, 14, 14);
                        g.DrawEllipse(pen, a.X + 66, cy - 6, 12, 12);
                        g.DrawEllipse(yp, a.X + 60, cy - 3, 24, 6);
                        foreach (var sp in new[] { SP(a.X + 40, a.Y + 6), SP(a.X + 70, a.Bottom - 8), SP(a.X + 20, a.Bottom - 6) })
                            g.DrawPolygon(pen, StarPoints(sp.X, sp.Y, 5, 2));
                        break;
                    case 6: // shape pattern: triangle, circle, square, triangle, circle, square, ?
                        for (int i = 0; i < 6; i++)
                        {
                            float x = a.X + 22 + i * 28;
                            if (i % 3 == 0) g.DrawPolygon(yp, new[] { SP(x, cy - 12), SP(x + 12, cy + 10), SP(x - 12, cy + 10) });
                            else if (i % 3 == 1) g.DrawEllipse(pp, x - 11, cy - 11, 22, 22);
                            else g.DrawRectangle(pen, x - 10, cy - 10, 20, 20);
                        }
                        TextIn(g, "?", new RectangleF(a.Right - 34, cy - 18, 30, 36), 30, chalk);
                        break;
                    case 7: // compass rose
                        g.DrawLine(pen, cx, a.Y + 2, cx, a.Bottom - 2);
                        g.DrawLine(pen, cx - 30, cy, cx + 30, cy);
                        g.DrawPolygon(yp, new[] { SP(cx, a.Y + 2), SP(cx + 6, cy), SP(cx, cy - 4), SP(cx - 6, cy) });
                        TextIn(g, "N", new RectangleF(cx + 6, a.Y - 2, 18, 16), 14, yellow);
                        TextIn(g, "E", new RectangleF(cx + 28, cy - 16, 16, 16), 13, chalk);
                        TextIn(g, "W", new RectangleF(cx - 46, cy - 16, 16, 16), 13, chalk);
                        TextIn(g, "S", new RectangleF(cx + 6, a.Bottom - 16, 16, 16), 13, chalk);
                        break;
                    case 8: // knight L move on a small grid
                        {
                            float sz = 14, ox = cx - sz * 2, oy = a.Y + 6;
                            for (int i = 0; i <= 3; i++) g.DrawLine(pen, ox, oy + i * sz, ox + sz * 4, oy + i * sz);
                            for (int i = 0; i <= 4; i++) g.DrawLine(pen, ox + i * sz, oy, ox + i * sz, oy + sz * 3);
                            g.DrawLines(pp, new[] { SP(ox + sz * 0.5f, oy + sz * 2.5f), SP(ox + sz * 2.5f, oy + sz * 2.5f), SP(ox + sz * 2.5f, oy + sz * 1.5f) });
                            using (var b = B(yellow)) g.FillEllipse(b, ox + sz * 0.5f - 4, oy + sz * 2.5f - 4, 8, 8);
                            using (var b = B(pink)) g.FillEllipse(b, ox + sz * 2.5f - 4, oy + sz * 1.5f - 4, 8, 8);
                            break;
                        }
                    case 9: // rectangle with sides a and b
                        g.DrawRectangle(pen, a.X + 6, a.Y + 6, 54, 40);
                        TextIn(g, "a", new RectangleF(a.X + 24, a.Y + 46, 18, 20), 17, yellow);
                        TextIn(g, "b", new RectangleF(a.X + 61, a.Y + 16, 16, 20), 17, pink);
                        break;
                }
            }
        }
    }
}
