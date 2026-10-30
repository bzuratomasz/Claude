using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace LazyTurtle
{
    /// <summary>
    /// Everything drawn over the 3D view: gift bar, wisdom orb, compass, mini map, quiz dialogs,
    /// painting captions, menus, floor screens and celebrations (confetti, "Super!" banners).
    /// Also remembers clickable areas for the mouse.
    /// GDI+ objects are cached (text outlines, rounded rectangles, fonts) or reused (one brush and
    /// one pen whose color is set before each use), so a frame allocates almost nothing.
    /// </summary>
    sealed class Hud : IDisposable
    {
        const int Bits = 96;              // confetti pieces in the shared template
        const double CheerLength = 1.8;   // seconds a celebration stays on screen

        // warm, bright palette
        static readonly Color Cream = Color.FromArgb(240, 255, 245, 215);
        static readonly Color Gold = Color.FromArgb(255, 225, 60);
        static readonly Color Ink = Color.FromArgb(60, 45, 35);
        static readonly Color[] Party =
        {
            Color.FromArgb(255, 90, 90), Color.FromArgb(255, 170, 40), Color.FromArgb(255, 230, 60), Color.FromArgb(90, 205, 90),
            Color.FromArgb(70, 165, 250), Color.FromArgb(190, 115, 245), Color.FromArgb(255, 120, 200),
        };
        static readonly Color[] TitleColors =
        {
            Color.FromArgb(255, 225, 60), Color.FromArgb(255, 160, 50), Color.FromArgb(255, 115, 125),
            Color.FromArgb(130, 225, 90), Color.FromArgb(95, 195, 255), Color.FromArgb(205, 145, 255),
        };
        static readonly Color[] MenuColors =
        {
            Color.FromArgb(90, 190, 80), Color.FromArgb(70, 155, 235), Color.FromArgb(240, 140, 50), Color.FromArgb(150, 110, 225),
            Color.FromArgb(40, 175, 170), Color.FromArgb(225, 90, 150), Color.FromArgb(225, 85, 75),
        };
        static readonly Color[] AnswerColors =
        {
            Color.FromArgb(240, 95, 95), Color.FromArgb(70, 150, 240), Color.FromArgb(245, 170, 30), Color.FromArgb(75, 185, 90),
        };

        readonly Game game;
        readonly FontFamily family;
        readonly ImageAttributes faded = new ImageAttributes();
        readonly List<KeyValuePair<RectangleF, int>> hits = new List<KeyValuePair<RectangleF, int>>();
        float u;
        int W, H;

        // reused GDI+ objects and caches (cleared when the window scale changes)
        readonly SolidBrush brush = new SolidBrush(Color.White);
        readonly Pen pen = new Pen(Color.White) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        readonly StringFormat[] formats = new StringFormat[6];
        readonly StringFormat typo;
        readonly Dictionary<string, GraphicsPath> textPaths = new Dictionary<string, GraphicsPath>();
        readonly Dictionary<string, KeyValuePair<GraphicsPath, float>> boxPaths = new Dictionary<string, KeyValuePair<GraphicsPath, float>>();
        readonly Dictionary<long, GraphicsPath> roundPaths = new Dictionary<long, GraphicsPath>();
        readonly Dictionary<int, Font> fonts = new Dictionary<int, Font>();
        readonly Dictionary<string, float> fitSizes = new Dictionary<string, float>();
        readonly Dictionary<string, SizeF> measures = new Dictionary<string, SizeF>();
        readonly LinearGradientBrush[] cardBrushes = new LinearGradientBrush[School.Floors];
        readonly PointF[] poly4 = new PointF[4], poly3 = new PointF[3], star10 = new PointF[10], spark8 = new PointF[8];
        LinearGradientBrush titleBg, orbFill;
        GraphicsPath orbPath;
        GraphicsPath[] titlePaths;
        float[] titleX;
        float titleWidth, cacheU, orbR;
        string titleFor;
        int bgW, bgH;

        // confetti and title sparkle templates (fixed random numbers, drawn deterministically)
        readonly float[] bitA = new float[Bits], bitS = new float[Bits], bitSpin = new float[Bits], bitX = new float[Bits], bitY = new float[Bits];
        readonly float[] sparkX = new float[16], sparkY = new float[16], sparkP = new float[16];

        // small animations driven by changes the HUD notices
        readonly bool[] giftSeen = new bool[School.Classes];
        readonly double[] giftAt = new double[School.Classes];
        World giftWorld;
        float giftFrac;
        int lastCoins = -1;
        double coinAt = -99;
        float cheerX, cheerY;

        // mouse (for hover / pressed looks only; clicks still go through HitTest)
        Point mouse;
        bool mouseIn, mouseDown;

        public Hud(Game game)
        {
            this.game = game;
            try { family = new FontFamily("Comic Sans MS"); }
            catch (ArgumentException) { family = FontFamily.GenericSansSerif; }
            faded.SetColorMatrix(new ColorMatrix(new[]
            {
                new[] { 0.3f, 0.3f, 0.3f, 0, 0 },
                new[] { 0.59f, 0.59f, 0.59f, 0, 0 },
                new[] { 0.11f, 0.11f, 0.11f, 0, 0 },
                new[] { 0f, 0, 0, 0.4f, 0 },
                new[] { 0f, 0, 0, 0, 1 },
            }));
            for (int i = 0; i < 6; i++)
                formats[i] = new StringFormat { Alignment = (StringAlignment)(i % 3), LineAlignment = i < 3 ? StringAlignment.Near : StringAlignment.Center };
            typo = new StringFormat(StringFormat.GenericTypographic);
            typo.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
            var r = new Random(11);
            for (int i = 0; i < Bits; i++)
            {
                bitA[i] = (float)r.NextDouble();
                bitS[i] = (float)r.NextDouble();
                bitSpin[i] = (float)(r.NextDouble() * 2 - 1);
                bitX[i] = (float)r.NextDouble();
                bitY[i] = (float)r.NextDouble();
            }
            for (int i = 0; i < sparkX.Length; i++)
            {
                sparkX[i] = (float)(r.NextDouble() * 2 - 1);
                sparkY[i] = (float)r.NextDouble();
                sparkP[i] = (float)(r.NextDouble() * Math.PI * 2);
            }
        }

        public void Dispose()
        {
            ClearCaches();
            faded.Dispose();
            brush.Dispose();
            pen.Dispose();
            typo.Dispose();
            foreach (var f in formats) f.Dispose();
            titleBg?.Dispose();
        }

        /// <summary>Action of the clickable area under the mouse, or -1.</summary>
        public int HitTest(Point p)
        {
            for (int i = hits.Count - 1; i >= 0; i--)
                if (hits[i].Key.Contains(p)) return hits[i].Value;
            return -1;
        }

        void Hit(RectangleF r, int action) => hits.Add(new KeyValuePair<RectangleF, int>(r, action));

        bool Hover(RectangleF r) => mouseIn && r.Contains(mouse);

        public void Draw(Graphics g, Rectangle rc, Renderer r)
        {
            W = rc.Width;
            H = rc.Height;
            u = Math.Min(H / 720f, W / 1100f);
            if (u != cacheU) { ClearCaches(); cacheU = u; }
            hits.Clear();
            ReadMouse(rc);
            if (r != null) r.Blit(g, rc);     // software renderer; with OpenGL the HUD is drawn on a transparent overlay
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            cheerX = W / 2f;
            cheerY = H * 0.32f;
            if (game.Save.Coins != lastCoins)
            {
                if (lastCoins >= 0 && game.Save.Coins > lastCoins) coinAt = game.Time;
                lastCoins = game.Save.Coins;
            }

            switch (game.Screen)
            {
                case GameScreen.Title: TitleScreen(g); break;
                case GameScreen.Floors: FloorSelect(g); break;
                case GameScreen.Help: HelpScreen(g); break;
                case GameScreen.Intro:
                    PlayHud(g);
                    IntroBanner(g);
                    break;
                case GameScreen.Playing: PlayHud(g); break;
                case GameScreen.Dialog: DialogScreen(g); break;
                case GameScreen.Paused:
                    PlayHud(g);
                    Dim(g, 150);
                    Text(g, L.T("paused"), 70, W / 2f, 120 * u, Gold);
                    MenuList(g, game.PauseItems, 260 * u);
                    break;
                case GameScreen.FloorDone: FloorDone(g); break;
                case GameScreen.Victory: Victory(g); break;
            }
            if (game.CheerTime < CheerLength && (game.Screen == GameScreen.Playing || game.Screen == GameScreen.Dialog))
                Celebration(g);
        }

        void ReadMouse(Rectangle rc)
        {
            mouseIn = mouseDown = false;
            var form = System.Windows.Forms.Form.ActiveForm;
            if (form == null) return;
            mouse = form.PointToClient(System.Windows.Forms.Control.MousePosition);
            mouseIn = rc.Contains(mouse);
            mouseDown = (System.Windows.Forms.Control.MouseButtons & System.Windows.Forms.MouseButtons.Left) != 0;
        }

        // ------------------------------------------------------------------ caches

        void ClearCaches()
        {
            foreach (var p in textPaths.Values) p.Dispose();
            textPaths.Clear();
            foreach (var p in boxPaths.Values) p.Key.Dispose();
            boxPaths.Clear();
            foreach (var p in roundPaths.Values) p.Dispose();
            roundPaths.Clear();
            foreach (var f in fonts.Values) f.Dispose();
            fonts.Clear();
            fitSizes.Clear();
            measures.Clear();
            for (int i = 0; i < cardBrushes.Length; i++)
            {
                cardBrushes[i]?.Dispose();
                cardBrushes[i] = null;
            }
            orbFill?.Dispose();
            orbFill = null;
            orbPath?.Dispose();
            orbPath = null;
            if (titlePaths != null) foreach (var p in titlePaths) p.Dispose();
            titlePaths = null;
            titleFor = null;
        }

        StringFormat Sf(StringAlignment align, bool vcenter) => formats[(int)align + (vcenter ? 3 : 0)];

        Font FontPx(float size)
        {
            int key = (int)(size * u * 8);
            Font f;
            if (fonts.TryGetValue(key, out f)) return f;
            if (fonts.Count > 160)
            {
                foreach (var old in fonts.Values) old.Dispose();
                fonts.Clear();
            }
            f = new Font(family, size * u, FontStyle.Bold, GraphicsUnit.Pixel);
            fonts[key] = f;
            return f;
        }

        /// <summary>Outline of a one-line text, built once at the origin and moved with a transform.</summary>
        GraphicsPath TextPath(string s, float size, StringAlignment align)
        {
            string key = s + "\u0001" + (int)(size * u * 8) + "\u0001" + (int)align;
            GraphicsPath p;
            if (textPaths.TryGetValue(key, out p)) return p;
            if (textPaths.Count > 600)
            {
                foreach (var old in textPaths.Values) old.Dispose();
                textPaths.Clear();
            }
            p = new GraphicsPath();
            p.AddString(s, family, (int)FontStyle.Bold, size * u, PointF.Empty, Sf(align, false));
            textPaths[key] = p;
            return p;
        }

        /// <summary>Rounded rectangle of the given size at the origin.</summary>
        GraphicsPath RoundPath(float w, float h, float rad)
        {
            int wi = Math.Max(1, (int)Math.Round(w)), hi = Math.Max(1, (int)Math.Round(h)), ri = (int)Math.Round(rad);
            long key = ((long)wi << 40) | ((long)hi << 20) | (long)ri;
            GraphicsPath p;
            if (roundPaths.TryGetValue(key, out p)) return p;
            if (roundPaths.Count > 400)
            {
                foreach (var old in roundPaths.Values) old.Dispose();
                roundPaths.Clear();
            }
            p = RoundRect(new RectangleF(0, 0, wi, hi), ri);
            roundPaths[key] = p;
            return p;
        }

        // ------------------------------------------------------------------ drawing helpers

        void Text(Graphics g, string s, float size, float x, float y, Color fill, StringAlignment align = StringAlignment.Center, int alpha = 255)
        {
            if (string.IsNullOrEmpty(s)) return;
            var path = TextPath(s, size, align);
            g.TranslateTransform(x, y);
            Outline(g, path, size, fill, alpha);
            g.TranslateTransform(-x, -y);
        }

        /// <summary>Text scaled and turned around (x, vertical middle) - for popping words and counters.</summary>
        void TextPop(Graphics g, string s, float size, float x, float cy, Color fill, float scale, float degrees = 0, int alpha = 255, StringAlignment align = StringAlignment.Center)
        {
            if (string.IsNullOrEmpty(s) || scale <= 0.01f) return;
            var path = TextPath(s, size, align);
            var st = g.Save();
            g.TranslateTransform(x, cy);
            if (degrees != 0) g.RotateTransform(degrees);
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(0, -size * u * 0.68f);
            Outline(g, path, size, fill, alpha);
            g.Restore(st);
        }

        /// <summary>Wrapped text inside a box (for questions and captions).</summary>
        void TextBox(Graphics g, string s, float size, RectangleF box, Color fill, StringAlignment align = StringAlignment.Center)
        {
            if (string.IsNullOrEmpty(s)) return;
            string key = s + "\u0002" + (int)box.Width + "x" + (int)box.Height + "\u0002" + (int)(size * u * 8) + "\u0002" + (int)align;
            KeyValuePair<GraphicsPath, float> entry;
            if (!boxPaths.TryGetValue(key, out entry))
            {
                if (boxPaths.Count > 300)
                {
                    foreach (var old in boxPaths.Values) old.Key.Dispose();
                    boxPaths.Clear();
                }
                float fit = FitSize(g, s, size, box.Width, box.Height, align);
                var p = new GraphicsPath();
                p.AddString(s, family, (int)FontStyle.Bold, fit * u, new RectangleF(0, 0, box.Width, box.Height), Sf(align, true));
                entry = new KeyValuePair<GraphicsPath, float>(p, fit);
                boxPaths[key] = entry;
            }
            g.TranslateTransform(box.X, box.Y);
            Outline(g, entry.Key, entry.Value, fill, 255);
            g.TranslateTransform(-box.X, -box.Y);
        }

        /// <summary>Largest size (down from size, step 2) at which the text fits the box.</summary>
        float FitSize(Graphics g, string s, float size, float w, float h, StringAlignment align)
        {
            for (; size > 10; size -= 2)
                if (g.MeasureString(s, FontPx(size), (int)w, Sf(align, true)).Height <= h) break;
            return size;
        }

        void Outline(Graphics g, GraphicsPath path, float size, Color fill, int alpha)
        {
            pen.Color = Color.FromArgb(alpha * fill.A / 255, 50, 30, 20);
            pen.Width = Math.Max(2f, size * u * 0.15f);
            g.DrawPath(pen, path);
            brush.Color = Color.FromArgb(alpha * fill.A / 255, fill);
            g.FillPath(brush, path);
        }

        SizeF Measure(Graphics g, string s, float size)
        {
            string key = s + "\u0001" + (int)(size * u * 8);
            SizeF m;
            if (measures.TryGetValue(key, out m)) return m;
            if (measures.Count > 300) measures.Clear();
            m = g.MeasureString(s, FontPx(size));
            measures[key] = m;
            return m;
        }

        void Fill(Graphics g, Color c)
        {
            brush.Color = c;
            g.FillRectangle(brush, g.VisibleClipBounds);
        }

        void Dim(Graphics g, int alpha) => Fill(g, Color.FromArgb(alpha, 15, 15, 35));

        static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d < 1) d = 1;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        void FillRound(Graphics g, RectangleF r, float rad, Color c)
        {
            if (c.A == 0 || r.Width < 1 || r.Height < 1) return;
            brush.Color = c;
            g.TranslateTransform(r.X, r.Y);
            g.FillPath(brush, RoundPath(r.Width, r.Height, rad));
            g.TranslateTransform(-r.X, -r.Y);
        }

        void DrawRound(Graphics g, RectangleF r, float rad, Color c, float width)
        {
            if (c.A == 0 || r.Width < 1 || r.Height < 1) return;
            pen.Color = c;
            pen.Width = width;
            g.TranslateTransform(r.X, r.Y);
            g.DrawPath(pen, RoundPath(r.Width, r.Height, rad));
            g.TranslateTransform(-r.X, -r.Y);
        }

        static RectangleF Offset(RectangleF r, float dx, float dy) => new RectangleF(r.X + dx, r.Y + dy, r.Width, r.Height);

        static Color Shade(Color c, float k) => Color.FromArgb(c.A, (int)(c.R * k), (int)(c.G * k), (int)(c.B * k));

        static Color Tint(Color c, float k) => Color.FromArgb(c.A, c.R + (int)((255 - c.R) * k), c.G + (int)((255 - c.G) * k), c.B + (int)((255 - c.B) * k));

        static Color Alpha(Color c, int a) => Color.FromArgb(Math.Max(0, Math.Min(255, a)), c);

        static float EaseOutBack(float t)
        {
            t = Math.Max(0, Math.Min(1, t)) - 1;
            return 1 + 2.70158f * t * t * t + 1.70158f * t * t;
        }

        /// <summary>Rounded panel with a soft shadow, a glossy top and a light border.</summary>
        void Panel(Graphics g, RectangleF r, Color fill, Color? border = null, float rad = 22, bool gloss = true)
        {
            rad *= u;
            int a = fill.A;
            FillRound(g, Offset(r, 0, 7 * u), rad, Color.FromArgb(a * 40 / 255, 40, 20, 10));
            FillRound(g, Offset(r, 0, 3 * u), rad, Color.FromArgb(a * 60 / 255, 40, 20, 10));
            FillRound(g, r, rad, fill);
            if (gloss)
                FillRound(g, new RectangleF(r.X + 7 * u, r.Y + 4 * u, r.Width - 14 * u, Math.Min(r.Height * 0.42f, 56 * u)), rad * 0.8f, Color.FromArgb(a * 45 / 255, 255, 255, 255));
            DrawRound(g, r, rad, border ?? Cream, 4 * u);
        }

        /// <summary>Chunky button with a darker "side", gloss, and hover / pressed looks. Returns the top face.</summary>
        RectangleF Button(Graphics g, RectangleF r, Color face, Color border, float borderWidth, bool hover)
        {
            float rad = Math.Min(r.Height / 2, 24 * u), depth = 6 * u;
            bool pressed = hover && mouseDown;
            var top = pressed ? Offset(r, 0, depth * 0.7f) : r;
            FillRound(g, Offset(r, 0, depth + 4 * u), rad, Color.FromArgb(face.A * 60 / 255, 40, 20, 10));
            FillRound(g, Offset(r, 0, depth), rad, Shade(face, 0.68f));
            FillRound(g, top, rad, hover ? Tint(face, 0.2f) : face);
            FillRound(g, new RectangleF(top.X + 9 * u, top.Y + 4 * u, top.Width - 18 * u, top.Height * 0.42f), rad * 0.7f, Color.FromArgb(face.A * 75 / 255, 255, 255, 255));
            DrawRound(g, top, rad, border, borderWidth);
            return top;
        }

        void StarShape(Graphics g, float cx, float cy, float r, bool filled)
        {
            StarPoints(star10, cx, cy, r, r * 0.45f, 5);
            brush.Color = filled ? Color.FromArgb(255, 220, 40) : Color.FromArgb(140, 60, 60, 70);
            g.FillPolygon(brush, star10);
            if (filled)
            {
                StarPoints(star10, cx - r * 0.08f, cy - r * 0.12f, r * 0.55f, r * 0.25f, 5);
                brush.Color = Color.FromArgb(110, 255, 255, 220);
                g.FillPolygon(brush, star10);
                StarPoints(star10, cx, cy, r, r * 0.45f, 5);
            }
            pen.Color = filled ? Color.FromArgb(200, 120, 0) : Color.FromArgb(180, 30, 30, 40);
            pen.Width = 3 * u;
            g.DrawPolygon(pen, star10);
        }

        static void StarPoints(PointF[] pts, float cx, float cy, float outer, float inner, int tips)
        {
            for (int i = 0; i < tips * 2; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / tips;
                float rr = i % 2 == 0 ? outer : inner;
                pts[i] = new PointF(cx + (float)Math.Cos(a) * rr, cy + (float)Math.Sin(a) * rr);
            }
        }

        /// <summary>A four-pointed twinkle.</summary>
        void Twinkle(Graphics g, float cx, float cy, float r, Color c)
        {
            if (r < 0.8f) return;
            StarPoints(spark8, cx, cy, r, r * 0.28f, 4);
            brush.Color = c;
            g.FillPolygon(brush, spark8);
        }

        /// <summary>Rotating rays of light behind something (gift, stars, cheer words).</summary>
        void Rays(Graphics g, float cx, float cy, float len, int count, double turn, double hueShift, int alpha)
        {
            float half = (float)(Math.PI / count * 0.45);
            for (int i = 0; i < count; i++)
            {
                double a = turn + i * Math.PI * 2 / count;
                poly3[0] = new PointF(cx, cy);
                poly3[1] = new PointF(cx + (float)Math.Cos(a - half) * len, cy + (float)Math.Sin(a - half) * len);
                poly3[2] = new PointF(cx + (float)Math.Cos(a + half) * len, cy + (float)Math.Sin(a + half) * len);
                brush.Color = hueShift >= 0 ? Hue(i / (double)count + hueShift, alpha) : Color.FromArgb(alpha, 255, 240, 160);
                g.FillPolygon(brush, poly3);
            }
        }

        /// <summary>One paper confetti piece, turned and "flipping" (its width shrinks and grows).</summary>
        void Piece(Graphics g, float x, float y, float w, float h, double angle, double flip, Color c, int alpha)
        {
            float cs = (float)Math.Cos(angle), sn = (float)Math.Sin(angle);
            float hw = w / 2 * (float)Math.Abs(Math.Cos(flip)) + 0.5f, hh = h / 2;
            poly4[0] = new PointF(x - hw * cs + hh * sn, y - hw * sn - hh * cs);
            poly4[1] = new PointF(x + hw * cs + hh * sn, y + hw * sn - hh * cs);
            poly4[2] = new PointF(x + hw * cs - hh * sn, y + hw * sn + hh * cs);
            poly4[3] = new PointF(x - hw * cs - hh * sn, y - hw * sn + hh * cs);
            brush.Color = Alpha(c, alpha);
            g.FillPolygon(brush, poly4);
        }

        /// <summary>Confetti bursting out of a point (t = seconds since the burst).</summary>
        void Burst(Graphics g, float cx, float cy, double t, int seed, int count)
        {
            int alpha = t < CheerLength - 0.5 ? 255 : (int)(255 * Math.Max(0, (CheerLength - t) / 0.5));
            double drag = (1 - Math.Exp(-2.4 * t)) / 2.4;
            for (int i = 0; i < count; i++)
            {
                int k = (i + seed) % Bits;
                double a = -Math.PI / 2 + (bitA[k] - 0.5) * Math.PI * 1.5;
                double sp = (380 + bitS[k] * 560) * u;
                float x = cx + (float)(Math.Cos(a) * sp * drag);
                float y = cy + (float)(Math.Sin(a) * sp * drag + 300 * u * t * t);
                Piece(g, x, y, 14 * u, 8 * u, bitSpin[k] * 8 * t, t * (6 + bitS[k] * 6), Party[k % Party.Length], alpha);
            }
        }

        /// <summary>Confetti gently falling over the whole screen (floor done, diploma).</summary>
        void Rain(Graphics g, double time, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int k = i % Bits;
                float speed = 60 + bitS[k] * 120;
                float x = bitX[k] * W + (float)Math.Sin(time * 2 + i) * 20 * u;
                float y = (bitY[k] * H + (float)time * speed * u) % (H + 20 * u) - 10 * u;
                Piece(g, x, y, 13 * u, 7 * u, time * 3 * bitSpin[k] + i * 0.65, time * 4 + i, Party[i % Party.Length], 255);
            }
        }

        static Color Hue(double h, int alpha = 255)
        {
            h = (h % 1 + 1) % 1 * 6;
            int i = (int)h;
            double f = h - i;
            int q = (int)(255 * (1 - f)), t = (int)(255 * f);
            switch (i)
            {
                case 0: return Color.FromArgb(alpha, 255, t, 0);
                case 1: return Color.FromArgb(alpha, q, 255, 0);
                case 2: return Color.FromArgb(alpha, 0, 255, t);
                case 3: return Color.FromArgb(alpha, 0, q, 255);
                case 4: return Color.FromArgb(alpha, t, 0, 255);
                default: return Color.FromArgb(alpha, 255, 0, q);
            }
        }

        /// <summary>Big chunky menu buttons, one friendly color each; the selected one is golden and bounces.</summary>
        void MenuList(Graphics g, string[] items, float top)
        {
            float step = 58 * u, widest = 0;
            foreach (var s in items) widest = Math.Max(widest, Measure(g, s, 34).Width);
            float half = Math.Max(widest / 2 + 50 * u, 190 * u);
            for (int i = 0; i < items.Length; i++)
            {
                bool sel = i == game.Menu;
                float y = top + i * step;
                var rect = new RectangleF(W / 2f - half, y - 4 * u, half * 2, 48 * u);
                Hit(rect, 100 + i);
                bool hover = Hover(rect);
                if (sel) rect.Inflate(8 * u, 2 * u);
                var face = Button(g, rect, sel ? Color.FromArgb(255, 200, 45) : Alpha(MenuColors[i % MenuColors.Length], 235),
                                  sel ? Color.White : Color.FromArgb(200, 255, 255, 255), (sel ? 4 : 2.5f) * u, hover);
                if (sel)
                {
                    float bounce = (float)Math.Sin(game.Time * 6) * 6 * u;
                    Arrow(g, face.X - 16 * u - bounce, face.Y + face.Height / 2, 12 * u, false);
                    Arrow(g, face.Right + 16 * u + bounce, face.Y + face.Height / 2, 12 * u, true);
                }
                TextPop(g, items[i], sel ? 34 : 31, W / 2f, face.Y + face.Height / 2, Color.White, sel ? 1 + 0.03f * (float)Math.Sin(game.Time * 6) : 1);
            }
        }

        void Arrow(Graphics g, float x, float y, float s, bool pointLeft)
        {
            float d = pointLeft ? -1 : 1;
            poly3[0] = new PointF(x - s * d, y - s);
            poly3[1] = new PointF(x + s * d, y);
            poly3[2] = new PointF(x - s * d, y + s);
            pen.Color = Color.FromArgb(60, 35, 10);
            pen.Width = 4 * u;
            g.DrawPolygon(pen, poly3);
            brush.Color = Gold;
            g.FillPolygon(brush, poly3);
        }

        /// <summary>A coin (or star) counter whose number pops when it grows.</summary>
        void CoinCounter(Graphics g, float x, float y, float size)
        {
            double k = game.Time - coinAt;
            float pop = k < 0.45 ? 1 + 0.45f * (float)Math.Sin(k / 0.45 * Math.PI) : 1;
            float s = size * pop;
            g.DrawImage(game.Art.CoinIcon, x + size / 2 - s / 2, y + size / 2 - s / 2, s, s);
            if (k < 0.6) Twinkle(g, x + size * 0.85f, y + size * 0.1f, 10 * u * (float)(1 - k / 0.6), Color.White);
            TextPop(g, game.Save.Coins.ToString(), 26, x + size + 6 * u, y + size / 2, Gold, pop, 0, 255, StringAlignment.Near);
        }

        // ------------------------------------------------------------------ menus

        void TitleScreen(Graphics g)
        {
            if (titleBg == null || bgW != W || bgH != H)
            {
                titleBg?.Dispose();
                bgW = W;
                bgH = H;
                titleBg = new LinearGradientBrush(new Rectangle(0, 0, W, H + 1), Color.FromArgb(160, 255, 140, 60), Color.FromArgb(40, 120, 60, 170), 90f);
            }
            g.FillRectangle(titleBg, 0, 0, W, H);

            // title: every letter in its own color, bouncing and wobbling
            string title = L.T("title");
            LayoutTitle(g, title);
            float lh = 92 * u, tx0 = W / 2f + 40 * u - titleWidth / 2, ty = 16 * u;
            float bob = (float)Math.Sin(game.Time * 2) * 6 * u;
            g.DrawImage(game.Art.TurtleIcon, tx0 - 130 * u, 20 * u + bob, 110 * u, 105 * u);
            for (int i = 0; i < title.Length; i++)
            {
                if (char.IsWhiteSpace(title[i])) continue;
                float cw = (i + 1 < title.Length ? titleX[i + 1] : titleWidth) - titleX[i];
                float bounce = (float)Math.Sin(game.Time * 3 - i * 0.5) * 7 * u;
                var st = g.Save();
                g.TranslateTransform(tx0 + titleX[i] + cw / 2, ty + lh * 0.6f + bounce);
                g.RotateTransform((float)Math.Sin(game.Time * 2.2 + i * 0.9) * 6);
                g.TranslateTransform(-cw / 2, -lh * 0.6f);
                g.TranslateTransform(4 * u, 6 * u);
                brush.Color = Color.FromArgb(90, 40, 20, 10);
                g.FillPath(brush, titlePaths[i]);
                g.TranslateTransform(-4 * u, -6 * u);
                pen.Color = Color.FromArgb(70, 35, 20);
                pen.Width = 12 * u;
                g.DrawPath(pen, titlePaths[i]);
                brush.Color = TitleColors[i % TitleColors.Length];
                g.FillPath(brush, titlePaths[i]);
                g.Restore(st);
            }
            for (int i = 0; i < sparkX.Length; i++)
            {
                float r = 11 * u * (float)Math.Max(0, Math.Sin(game.Time * 2.4 + sparkP[i]));
                Twinkle(g, W / 2f + sparkX[i] * 470 * u, 12 * u + sparkY[i] * 220 * u, r, i % 3 == 0 ? Color.FromArgb(230, 255, 240, 140) : Color.FromArgb(220, 255, 255, 255));
            }

            Text(g, L.T("subtitle"), 34, W / 2f, 122 * u, Color.FromArgb(255, 255, 225));
            Text(g, L.T("school_name"), 22, W / 2f, 172 * u, Color.FromArgb(215, 240, 255));
            Text(g, L.T("school_patron"), 18, W / 2f, 202 * u, Color.FromArgb(215, 240, 255));
            MenuList(g, game.TitleItems, 252 * u);

            // coins and stars collected so far
            int stars = 0;
            foreach (var s in game.Save.Stars) stars += s;
            var pill = new RectangleF(12 * u, H - 62 * u, 250 * u, 52 * u);
            Panel(g, pill, Color.FromArgb(200, 120, 60, 30), null, 26);
            CoinCounter(g, pill.X + 12 * u, pill.Y + 7 * u, 38 * u);
            StarShape(g, pill.X + 150 * u, pill.Y + 26 * u, 17 * u, true);
            Text(g, stars.ToString(), 26, pill.X + 174 * u, pill.Y + 6 * u, Gold, StringAlignment.Near);
            Text(g, "v4.0", 18, W - 20 * u, H - 40 * u, Color.FromArgb(220, 255, 255, 255), StringAlignment.Far);
        }

        /// <summary>Per-letter outlines and positions of the title (rebuilt when the language or size changes).</summary>
        void LayoutTitle(Graphics g, string s)
        {
            if (titleFor == s && titlePaths != null) return;
            if (titlePaths != null) foreach (var p in titlePaths) p.Dispose();
            titleFor = s;
            titlePaths = new GraphicsPath[s.Length];
            titleX = new float[s.Length];
            var f = FontPx(92);
            float x = 0;
            for (int i = 0; i < s.Length; i++)
            {
                string ch = s[i].ToString();
                titleX[i] = x;
                titlePaths[i] = new GraphicsPath();
                titlePaths[i].AddString(ch, family, (int)FontStyle.Bold, 92 * u, PointF.Empty, typo);
                x += g.MeasureString(ch, f, PointF.Empty, typo).Width;
            }
            titleWidth = x;
        }

        void HelpScreen(Graphics g)
        {
            Dim(g, 120);
            var panel = new RectangleF(W / 2f - 540 * u, 30 * u, 1080 * u, 660 * u);
            Panel(g, panel, Color.FromArgb(230, 55, 95, 175));
            Hit(panel, 1);
            Text(g, L.T("help"), 52, W / 2f, 44 * u, Gold);
            TextBox(g, L.T("help_goal"), 24, new RectangleF(panel.X + 30 * u, 116 * u, panel.Width - 60 * u, 60 * u), Color.White);
            float y = 190 * u;
            for (int i = 1; i <= 8; i++)
            {
                FillRound(g, new RectangleF(panel.X + 40 * u, y - 2 * u, panel.Width - 80 * u, 40 * u), 14 * u, Color.FromArgb(i % 2 == 0 ? 40 : 22, 255, 255, 255));
                Text(g, L.T("help" + i), 25, W / 2f - 20 * u, y, Gold, StringAlignment.Far);
                Text(g, L.T("help" + i + "b"), 25, W / 2f + 10 * u, y, Color.White, StringAlignment.Near);
                y += 44 * u;
            }
            TextBox(g, L.T("help_tip"), 21, new RectangleF(panel.X + 30 * u, y + 4 * u, panel.Width - 60 * u, 56 * u), Color.FromArgb(210, 255, 200));
            Text(g, L.T("back"), 21, W / 2f, panel.Bottom - 44 * u, Color.FromArgb(230, 230, 240));
        }

        void FloorSelect(Graphics g)
        {
            Dim(g, 120);
            Text(g, L.T("floors"), 60, W / 2f, 50 * u, Gold);
            int n = School.Floors, perRow = (n + 1) / 2;
            float cw = 230 * u, ch = 240 * u, gap = 20 * u;
            float total = perRow * cw + (perRow - 1) * gap;
            float x0 = (W - total) / 2, y0 = 130 * u;
            for (int i = 0; i < n; i++)
            {
                bool sel = i == game.Menu, locked = i >= game.Save.Unlocked;
                float lift = sel ? (float)Math.Sin(game.Time * 5) * 5 * u - 8 * u : 0;
                var rect = new RectangleF(x0 + (i % perRow) * (cw + gap), y0 + (i / perRow) * (ch + gap) + lift, cw, ch);
                Hit(rect, 200 + i);
                bool hover = !locked && Hover(rect);
                if (hover && !sel) rect.Y -= 4 * u;
                var th = School.Themes[i];
                float rad = 20 * u;
                FillRound(g, Offset(rect, 0, 8 * u), rad, Color.FromArgb(90, 30, 15, 5));
                if (cardBrushes[i] == null) cardBrushes[i] = new LinearGradientBrush(new RectangleF(0, -1, cw, ch + 2), th.Paint, th.Accent, 90f);
                g.TranslateTransform(rect.X, rect.Y);
                g.FillPath(cardBrushes[i], RoundPath(cw, ch, rad));
                g.TranslateTransform(-rect.X, -rect.Y);
                FillRound(g, new RectangleF(rect.X + 8 * u, rect.Y + 5 * u, cw - 16 * u, 60 * u), rad * 0.8f, Color.FromArgb(55, 255, 255, 255));
                DrawRound(g, rect, rad, sel ? Gold : hover ? Color.White : Color.FromArgb(200, 255, 255, 255), (sel ? 7 : hover ? 5 : 3) * u);
                Text(g, (i + 1).ToString(), 56, rect.X + cw / 2, rect.Y + 6 * u, Color.White);
                TextBox(g, th.Name, 20, new RectangleF(rect.X + 10 * u, rect.Y + 78 * u, cw - 20 * u, 58 * u), Color.White);
                int done = 0;
                for (int b = 0; b < School.Classes; b++) if ((game.Save.Classes[i] & (1 << b)) != 0) done++;
                g.DrawImage(game.Art.GiftBoxIcon, rect.X + cw / 2 - 64 * u, rect.Y + 140 * u, 36 * u, 36 * u);
                Text(g, done + "/10", 24, rect.X + cw / 2 + 20 * u, rect.Y + 144 * u, Color.White);
                for (int s = 0; s < 3; s++)
                    StarShape(g, rect.X + cw / 2 + (s - 1) * 44 * u, rect.Y + 208 * u, 17 * u, s < game.Save.Stars[i]);
                if (locked)
                {
                    FillRound(g, rect, rad, Color.FromArgb(170, 20, 20, 30));
                    Padlock(g, rect.X + cw / 2, rect.Y + ch / 2, 36 * u);
                }
            }
            float bottom = y0 + 2 * ch + gap;
            Text(g, L.T("select_hint"), 24, W / 2f, bottom + 14 * u, Color.White);
            Text(g, L.T("back"), 20, W / 2f, bottom + 52 * u, Color.FromArgb(220, 220, 220));
        }

        void Padlock(Graphics g, float cx, float cy, float s)
        {
            pen.Color = Color.FromArgb(220, 220, 220);
            pen.Width = s * 0.25f;
            g.DrawArc(pen, cx - s * 0.6f, cy - s * 1.3f, s * 1.2f, s * 1.4f, 180, 180);
            FillRound(g, new RectangleF(cx - s, cy - s * 0.6f, s * 2, s * 1.6f), s * 0.3f, Color.FromArgb(255, 200, 60));
            brush.Color = Color.FromArgb(60, 40, 20);
            g.FillEllipse(brush, cx - s * 0.15f, cy - s * 0.1f, s * 0.3f, s * 0.4f);
        }

        // ------------------------------------------------------------------ in-game HUD

        void PlayHud(Graphics g)
        {
            var w = game.World;

            // floor badge and coins
            var badge = new RectangleF(14 * u, 12 * u, 330 * u, 86 * u);
            Panel(g, badge, Color.FromArgb(215, 235, 125, 45));
            Text(g, game.InYard ? L.T("yard") : L.T("floor") + " " + (game.FloorIndex + 1), 26, badge.X + 16 * u, badge.Y + 6 * u, Color.FromArgb(255, 245, 150), StringAlignment.Near);
            TextBox(g, game.InYard ? L.T("school_name") : game.Theme.Name, 17, new RectangleF(badge.X + 14 * u, badge.Y + 40 * u, 210 * u, 40 * u), Color.White, StringAlignment.Near);
            CoinCounter(g, badge.Right - 108 * u, badge.Y + 12 * u, 34 * u);

            if (game.InYard) Scoreboard(g);
            else GiftBar(g);
            Compass(g, W / 2f, 132 * u, 30 * u);

            if (game.BigMap)
            {
                float s = Math.Min(W * 0.8f, H * 0.7f);
                float mw = s * 2.4f > W * 0.92f ? W * 0.92f : s * 2.4f;
                MiniMap(g, new RectangleF(W / 2f - mw / 2, H / 2f - s / 2 + 30 * u, mw, s), 225);
            }
            else MiniMap(g, new RectangleF(W - 300 * u, 12 * u, 286 * u, 118 * u), 215);

            // wisdom orb (Diablo style): classrooms finished on this floor
            if (!game.InYard)
            {
                float R = 62 * u;
                Orb(g, 22 * u + R, H - 22 * u - R, R, w.GiftsDone / (double)School.Classes);
                Text(g, w.GiftsDone + "/10", 30, 22 * u + R, H - 22 * u - R - 18 * u, Color.White);
                Text(g, L.T("wisdom"), 16, 22 * u + R, H - 22 * u - R + 18 * u, Color.FromArgb(255, 245, 200));
            }

            if (game.Screen == GameScreen.Playing)
            {
                if (game.CanTalk) TalkPrompt(g);
                else if (game.NearPainting != null)
                {
                    int k = game.NearPainting.Kind;
                    Caption(g, k >= School.SubjectCaption ? Art.SubjectPosterCaption((k - School.SubjectCaption) / 3, (k - School.SubjectCaption) % 3)
                             : k >= School.KornikCaption ? Art.KornikPosterCaption(k - School.KornikCaption) : L.T("paint" + k));
                }
                else if (game.InYard && game.Match != null && game.Match.Ball != null &&
                         Math.Abs(game.Match.Ball.X - game.P.X) + Math.Abs(game.Match.Ball.Y - game.P.Y) < 1.2)
                    TextPop(g, L.T("kick"), 28, W / 2f, H - 74 * u, Color.White, 1 + 0.06f * (float)Math.Sin(game.Time * 6));
            }

            if (game.MessageTime > 0 && game.Message != null)
            {
                int a = (int)(Math.Min(1, game.MessageTime) * 255);
                var box = new RectangleF(W / 2f - 420 * u, 176 * u, 840 * u, 80 * u);
                FillRound(g, Offset(box, 0, 5 * u), 20 * u, Color.FromArgb(a * 60 / 255, 30, 10, 30));
                FillRound(g, box, 20 * u, Color.FromArgb(a * 215 / 255, 105, 60, 165));
                DrawRound(g, box, 20 * u, Color.FromArgb(a * 230 / 255, 255, 235, 170), 4 * u);
                TextBox(g, game.Message, 28, new RectangleF(box.X + 14 * u, box.Y + 6 * u, box.Width - 28 * u, box.Height - 12 * u), Color.FromArgb(a, 255, 248, 170));
            }

            foreach (var p in game.Popups)
            {
                int a = (int)(255 * Math.Max(0, 1 - p.Time / 1.5));
                float pop = p.Time < 0.25 ? EaseOutBack((float)(p.Time / 0.25)) : 1;
                TextPop(g, p.Text, 38, W * p.X, H * 0.4f + 22 * u - (float)p.Time * 80 * u, p.Color, pop, 0, a);
            }
        }

        /// <summary>The ten gifts of the floor: each one pops in when won, plus a progress bar underneath.</summary>
        void GiftBar(Graphics g)
        {
            var w = game.World;
            int doneCount = 0;
            bool fresh = w != giftWorld;
            giftWorld = w;
            for (int i = 0; i < School.Classes; i++)
            {
                bool done = false;
                foreach (var r in w.Rooms) if (r.Index == i) done = r.Done;
                if (done) doneCount++;
                if (done && !giftSeen[i] && !fresh) giftAt[i] = game.Time;
                else if (fresh) giftAt[i] = -99;
                giftSeen[i] = done;
            }
            float target = doneCount / (float)School.Classes;
            giftFrac = fresh ? target : giftFrac + (target - giftFrac) * 0.08f;

            float s = 44 * u, gap = 8 * u;
            float total = 10 * s + 9 * gap;
            float x0 = (W - total) / 2, y = 14 * u;
            var bg = new RectangleF(x0 - 14 * u, y - 8 * u, total + 28 * u, s + 32 * u);
            Panel(g, bg, Color.FromArgb(205, 60, 140, 215), null, 18);
            for (int i = 0; i < 10; i++)
            {
                float cx = x0 + i * (s + gap) + s / 2, cy = y + s / 2;
                brush.Color = Color.FromArgb(60, 255, 255, 255);
                g.FillEllipse(brush, cx - s * 0.55f, cy - s * 0.55f, s * 1.1f, s * 1.1f);
                if (giftSeen[i])
                {
                    double k = game.Time - giftAt[i];
                    float pop = k < 0.5 ? EaseOutBack((float)(k / 0.5)) * (1 + 0.35f * (float)Math.Sin(k / 0.5 * Math.PI)) : 1;
                    if (k < 0.8)
                        for (int t = 0; t < 6; t++)
                        {
                            double a = t * Math.PI / 3 + k * 2;
                            float rr = s * (0.4f + (float)k);
                            Twinkle(g, cx + (float)Math.Cos(a) * rr, cy + (float)Math.Sin(a) * rr, 8 * u * (float)(1 - k / 0.8), Gold);
                        }
                    float sz = s * pop;
                    g.DrawImage(game.Art.GiftIcons[i], cx - sz / 2, cy - sz / 2, sz, sz);
                }
                else
                {
                    var dest = new Rectangle((int)(cx - s / 2), (int)(cy - s / 2), (int)s, (int)s);
                    g.DrawImage(game.Art.GiftBoxIcon, dest, 0, 0, game.Art.GiftBoxIcon.Width, game.Art.GiftBoxIcon.Height, GraphicsUnit.Pixel, faded);
                }
            }
            var track = new RectangleF(x0, y + s + 7 * u, total, 10 * u);
            FillRound(g, track, 5 * u, Color.FromArgb(130, 20, 30, 70));
            if (giftFrac > 0.01f)
            {
                var bar = new RectangleF(track.X, track.Y, Math.Max(track.Height, track.Width * giftFrac), track.Height);
                FillRound(g, bar, 5 * u, Color.FromArgb(255, 210, 50));
                FillRound(g, new RectangleF(bar.X + 3 * u, bar.Y + 1.5f * u, bar.Width - 6 * u, 3 * u), 1.5f * u, Color.FromArgb(150, 255, 255, 220));
            }
        }

        /// <summary>A pulsing "Enter" button near a teacher or a classmate.</summary>
        void TalkPrompt(Graphics g)
        {
            bool kid = game.NearKid != null && game.NearRoom == null && !game.NearMaster;
            string label = kid ? L.F("talk_to_btn", game.NearKid.Name) : L.T("talk_btn");
            float keyW = Measure(g, "Enter", 22).Width + 20 * u;
            float labelW = Measure(g, label, 32).Width;
            float width = Math.Max(300 * u, keyW + labelW + 60 * u);
            float bounce = (float)Math.Abs(Math.Sin(game.Time * 4)) * 8 * u;
            var box = new RectangleF(W / 2f - width / 2, H - 124 * u - bounce, width, 64 * u);
            Hit(box, 20);
            float glow = (float)(0.5 + 0.5 * Math.Sin(game.Time * 5));
            var halo = box;
            halo.Inflate((6 + 6 * glow) * u, (6 + 6 * glow) * u);
            FillRound(g, halo, 34 * u, Color.FromArgb((int)(40 + 50 * glow), 255, 235, 120));
            var face = Button(g, box, Color.FromArgb(240, 80, 185, 80), Color.White, 3 * u, Hover(box));
            var key = new RectangleF(face.X + 20 * u, face.Y + 12 * u, keyW, face.Height - 24 * u);
            FillRound(g, Offset(key, 0, 3 * u), 9 * u, Color.FromArgb(150, 30, 70, 30));
            FillRound(g, key, 9 * u, Color.FromArgb(255, 255, 250, 235));
            TextPop(g, "Enter", 22, key.X + key.Width / 2, key.Y + key.Height / 2, Color.FromArgb(255, 220, 90), 1 + 0.06f * glow);
            float textX = key.Right + (face.Right - key.Right) / 2 - 8 * u;
            TextPop(g, label, 32, textX, face.Y + face.Height / 2, Color.White, 1);
        }

        /// <summary>Football score and clock (yard), or a hint to talk to the captain.</summary>
        void Scoreboard(Graphics g)
        {
            var m = game.Match;
            var box = new RectangleF(W / 2f - 300 * u, 10 * u, 600 * u, 66 * u);
            Panel(g, box, Color.FromArgb(205, 50, 150, 70), Color.FromArgb(235, 255, 255, 255));
            if (m != null && m.Active)
            {
                TextBox(g, m.ScoreText(), 26, new RectangleF(box.X + 10 * u, box.Y + 4 * u, box.Width - 110 * u, box.Height - 8 * u), Color.White);
                Text(g, m.TimeText(), 26, box.Right - 52 * u, box.Y + 16 * u, Gold);
            }
            else TextBox(g, L.T("yard_hint"), 22, new RectangleF(box.X + 10 * u, box.Y + 4 * u, box.Width - 20 * u, box.Height - 8 * u), Color.White);
            if (m != null && m.EventTime > 0 && !string.IsNullOrEmpty(m.LastEvent))
            {
                float pop = (float)(1 + 0.3 * Math.Max(0, m.EventTime - 1));
                Text(g, m.LastEvent, 70 * pop, W / 2f, H * 0.3f, Gold);
            }
        }

        void Caption(Graphics g, string text)
        {
            var box = new RectangleF(W / 2f - 440 * u, H - 150 * u, 880 * u, 120 * u);
            Panel(g, box, Color.FromArgb(225, 120, 60, 150), Color.FromArgb(235, 255, 220, 120));
            TextBox(g, text, 24, new RectangleF(box.X + 20 * u, box.Y + 10 * u, box.Width - 40 * u, box.Height - 20 * u), Color.White);
        }

        void Orb(Graphics g, float cx, float cy, float R, double frac)
        {
            if (orbFill == null || orbR != R)
            {
                orbFill?.Dispose();
                orbPath?.Dispose();
                orbR = R;
                orbFill = new LinearGradientBrush(new RectangleF(-R, -R - 1, 2 * R, 2 * R + 2), Color.FromArgb(255, 230, 90), Color.FromArgb(220, 120, 0), 90f);
                orbPath = new GraphicsPath();
                orbPath.AddEllipse(-R, -R, 2 * R, 2 * R);
            }
            var st = g.Save();
            g.TranslateTransform(cx, cy);
            brush.Color = Color.FromArgb(220, 35, 20, 30);
            g.FillPath(brush, orbPath);
            float level = R - (float)(Math.Max(0, Math.Min(1, frac)) * 2 * R);
            var inner = g.Save();
            g.SetClip(orbPath);
            g.IntersectClip(new RectangleF(-R, level, 2 * R, R - level + 1));
            g.FillPath(orbFill, orbPath);
            float wave = (float)Math.Sin(game.Time * 3) * 3 * u;
            brush.Color = Color.FromArgb(100, 255, 255, 255);
            g.FillEllipse(brush, -R * 1.2f, level - 5 * u + wave, R * 2.8f, 10 * u);
            g.Restore(inner);
            brush.Color = Color.FromArgb(70, 255, 255, 255);
            g.FillEllipse(brush, -R * 0.6f, -R * 0.85f, R * 0.9f, R * 0.55f);
            pen.Color = Color.FromArgb(70, 40, 15);
            pen.Width = 9 * u;
            g.DrawPath(pen, orbPath);
            pen.Color = Color.FromArgb(240, 190, 70);
            pen.Width = 4 * u;
            g.DrawPath(pen, orbPath);
            g.Restore(st);
        }

        void Compass(Graphics g, float cx, float cy, float r)
        {
            double tx, ty;
            bool master;
            if (!game.CompassTarget(out tx, out ty, out master)) return;
            double rel = Math.Atan2(ty - game.P.Y, tx - game.P.X) - game.CamAngle;
            brush.Color = Color.FromArgb(190, 50, 90, 160);
            g.FillEllipse(brush, cx - r, cy - r, 2 * r, 2 * r);
            pen.Color = Color.FromArgb(240, 190, 70);
            pen.Width = 4 * u;
            g.DrawEllipse(pen, cx - r, cy - r, 2 * r, 2 * r);
            float cs = (float)Math.Cos(rel), sn = (float)Math.Sin(rel);
            // arrow pointing up, turned by rel (same as the old rotated drawing)
            SetRotated(0, cx, cy, 0, -r * 0.8f, cs, sn);
            SetRotated(1, cx, cy, r * 0.45f, r * 0.35f, cs, sn);
            SetRotated(2, cx, cy, 0, r * 0.12f, cs, sn);
            SetRotated(3, cx, cy, -r * 0.45f, r * 0.35f, cs, sn);
            brush.Color = master ? Color.FromArgb(200, 120, 255) : Color.FromArgb(255, 200, 40);
            g.FillPolygon(brush, poly4);
            pen.Color = Color.White;
            pen.Width = 2 * u;
            g.DrawPolygon(pen, poly4);
        }

        void SetRotated(int i, float cx, float cy, float x, float y, float cs, float sn) => poly4[i] = new PointF(cx + x * cs - y * sn, cy + x * sn + y * cs);

        void MiniMap(Graphics g, RectangleF area, int alpha)
        {
            var w = game.World;
            float cs = Math.Min(area.Width / w.Wd, area.Height / w.Ht);
            float ox = area.X + (area.Width - cs * w.Wd) / 2, oy = area.Y + (area.Height - cs * w.Ht) / 2;
            var frame = new RectangleF(ox - 5 * u, oy - 5 * u, cs * w.Wd + 10 * u, cs * w.Ht + 10 * u);
            FillRound(g, Offset(frame, 0, 4 * u), 9 * u, Color.FromArgb(alpha * 70 / 255, 30, 15, 5));
            FillRound(g, frame, 9 * u, Color.FromArgb(alpha, 30, 35, 55));
            DrawRound(g, frame, 9 * u, Color.FromArgb(alpha, 240, 190, 70), 3 * u);
            var smooth = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;
            Color wall = Color.FromArgb(alpha, 120, 100, 90), floor = Color.FromArgb(alpha, 215, 205, 170);
            Color done = Color.FromArgb(alpha, 130, 210, 110), hall = Color.FromArgb(alpha, 180, 100, 200);
            for (int y = 0; y < w.Ht; y++)
                for (int x = 0; x < w.Wd; x++)
                {
                    int i = y * w.Wd + x;
                    if (!w.Explored[i]) continue;
                    Color c = w.Wall[i] != 0 ? wall : floor;
                    if (w.Wall[i] == 0)
                    {
                        if (x > 41 && !game.InYard) c = hall;
                        foreach (var r in w.Rooms) if (r.Done && r.Contains(x + 0.5, y + 0.5)) c = done;
                    }
                    if (brush.Color != c) brush.Color = c;
                    g.FillRectangle(brush, ox + x * cs, oy + y * cs, cs + 0.5f, cs + 0.5f);
                }
            g.SmoothingMode = smooth;

            float dot = Math.Max(6 * u, cs * 1.3f);
            brush.Color = Color.FromArgb(255, 255, 215, 40);
            foreach (var r in w.Rooms)
            {
                if (r.Done) continue;
                g.FillEllipse(brush, ox + (float)r.Teacher.X * cs - dot / 2, oy + (float)r.Teacher.Y * cs - dot / 2, dot, dot);
            }
            if (w.Master != null)
            {
                brush.Color = (int)(game.Time * 3) % 2 == 0 ? Color.FromArgb(255, 220, 140, 255) : Color.FromArgb(255, 160, 80, 220);
                g.FillEllipse(brush, ox + (float)w.Master.X * cs - dot / 2, oy + (float)w.Master.Y * cs - dot / 2, dot, dot);
            }
            if (game.InYard && game.Match != null && game.Match.Ball != null)
                g.FillEllipse(Brushes.White, ox + (float)game.Match.Ball.X * cs - dot / 2, oy + (float)game.Match.Ball.Y * cs - dot / 2, dot, dot);

            float px = ox + (float)game.P.X * cs, py = oy + (float)game.P.Y * cs;
            float a = Math.Max(7 * u, cs * 1.5f), ca = (float)Math.Cos(game.P.Angle), sa = (float)Math.Sin(game.P.Angle);
            poly3[0] = new PointF(px + a * ca, py + a * sa);
            poly3[1] = new PointF(px - a * 0.7f * ca - a * 0.65f * sa, py - a * 0.7f * sa + a * 0.65f * ca);
            poly3[2] = new PointF(px - a * 0.7f * ca + a * 0.65f * sa, py - a * 0.7f * sa - a * 0.65f * ca);
            brush.Color = Color.FromArgb(80, 200, 80);
            g.FillPolygon(brush, poly3);
            pen.Color = Color.White;
            pen.Width = 1.5f * u;
            g.DrawPolygon(pen, poly3);
        }

        // ------------------------------------------------------------------ quiz dialog

        void DialogScreen(Graphics g)
        {
            var d = game.Dialog;
            Fill(g, Color.FromArgb(110, 15, 15, 35));

            // portrait of the teacher (mouth moves while speaking)
            bool talking = d.Time < 2.2 && (int)(d.Time * 8) % 2 == 0;
            Bitmap img;
            if (d.Kid != null) img = game.Art.KidPortrait[d.Kid.Variant % 3];
            else img = (d.IsMaster ? game.Art.MasterPortrait : game.Art.TeacherPortraits[d.Room.Subject])[talking ? 1 : 0];
            var accent = d.Kid != null ? Color.FromArgb(240, 120, 60) : d.IsMaster ? Color.FromArgb(160, 90, 220) : School.Subjects[d.Room.Subject].Color;
            float ph = 330 * u, pw = ph * img.Width / img.Height;
            var pr = new RectangleF(30 * u, H - ph - 30 * u, pw, ph);
            brush.Color = Color.FromArgb(110, Tint(accent, 0.5f));
            g.FillEllipse(brush, pr.X - 20 * u, pr.Y + 40 * u, pw + 40 * u, ph - 30 * u);
            brush.Color = Color.FromArgb(110, 255, 255, 255);
            g.FillEllipse(brush, pr.X, pr.Y + 60 * u, pw, ph - 70 * u);
            float hop = d.Phase == Phase.Correct && d.Time < 0.8 ? -(float)Math.Abs(Math.Sin(d.Time * 12)) * 14 * u * (float)(1 - d.Time / 0.8) : 0;
            g.DrawImage(img, pr.X, pr.Y + hop, pr.Width, pr.Height);
            cheerX = pr.X + pr.Width / 2;
            cheerY = Math.Max(80 * u, pr.Y - 10 * u);

            // speech bubble
            float bx = pr.Right + 20 * u, bw = W - bx - 30 * u;
            var bubble = new RectangleF(bx, 40 * u, bw, H - 300 * u);
            var paper = Color.FromArgb(255, 255, 252, 238);
            Panel(g, bubble, paper, Alpha(accent, 240), 26, false);
            float tipX = bx - 34 * u, tipY = bubble.Bottom - 70 * u, ay = bubble.Bottom - 124 * u, by = bubble.Bottom - 82 * u;
            poly3[0] = new PointF(bx + 8 * u, ay);
            poly3[1] = new PointF(tipX, tipY);
            poly3[2] = new PointF(bx + 8 * u, by);
            brush.Color = paper;
            g.FillPolygon(brush, poly3);
            float k = (bx - tipX) / (bx + 8 * u - tipX);
            pen.Color = Alpha(accent, 240);
            pen.Width = 4 * u;
            g.DrawLine(pen, tipX, tipY, bx, tipY + (ay - tipY) * k);
            g.DrawLine(pen, tipX, tipY, bx, tipY + (by - tipY) * k);

            // name tag on the bubble's edge
            float tagW = Math.Min(bubble.Width - 260 * u, Measure(g, d.Speaker, 22).Width + 36 * u);
            var tag = new RectangleF(bubble.X + 22 * u, bubble.Y - 18 * u, tagW, 40 * u);
            FillRound(g, Offset(tag, 0, 3 * u), 20 * u, Color.FromArgb(80, 40, 20, 10));
            FillRound(g, tag, 20 * u, accent);
            DrawRound(g, tag, 20 * u, Color.White, 3 * u);
            TextBox(g, d.Speaker, 22, new RectangleF(tag.X + 12 * u, tag.Y + 2 * u, tag.Width - 24 * u, tag.Height - 4 * u), Color.White);
            if (game.Voice.Available)
            {
                var read = new RectangleF(bubble.Right - 210 * u, bubble.Y + 12 * u, 190 * u, 36 * u);
                var face = Button(g, read, Color.FromArgb(240, 70, 150, 230), Color.White, 2.5f * u, Hover(read));
                TextPop(g, L.T("read"), 20, face.X + face.Width / 2, face.Y + face.Height / 2, Color.White, 1);
                Hit(read, 11);
            }

            var inner = new RectangleF(bubble.X + 24 * u, bubble.Y + 56 * u, bubble.Width - 48 * u, bubble.Height - 70 * u);
            if (d.Phase == Phase.Ask || d.Phase == Phase.Wrong || d.Phase == Phase.Correct)
            {
                // correct = green, wrong = calm orange (never alarming red), asking = soft brown
                Color topColor = d.Phase == Phase.Correct ? Color.FromArgb(40, 160, 60) : d.Phase == Phase.Wrong ? Color.FromArgb(215, 120, 25) : Color.FromArgb(130, 105, 80);
                TextPlain(g, d.Line, 22, new RectangleF(inner.X, inner.Y, inner.Width, 70 * u), topColor);
                TextPlain(g, d.Q.Text, 34, new RectangleF(inner.X, inner.Y + 74 * u, inner.Width, inner.Height - 80 * u), Ink);
            }
            else
            {
                TextPlain(g, d.Line, 34, inner, Ink);
            }

            if (d.Phase == Phase.Reward && d.Gift >= 0) GiftReveal(g, d);

            // answers (2 x 2 grid): chunky buttons in four friendly colors with the number key in a circle
            if (d.Q != null && (d.Phase == Phase.Ask || d.Phase == Phase.Wrong || d.Phase == Phase.Correct))
            {
                int n = d.Q.Answers.Length;
                float top = bubble.Bottom + 20 * u, aw = (bw - 20 * u) / 2, ah = (H - top - 70 * u) / 2 - 8 * u;
                float pulse = (float)(0.5 + 0.5 * Math.Sin(game.Time * 6));
                for (int i = 0; i < n; i++)
                {
                    var r = new RectangleF(bx + (i % 2) * (aw + 20 * u), top + (i / 2) * (ah + 16 * u), aw, ah);
                    Hit(r, i);
                    var col = AnswerColors[i % AnswerColors.Length];
                    bool sel = d.Phase == Phase.Ask && i == d.Selected;
                    bool hover = d.Phase == Phase.Ask && Hover(r);
                    bool right = d.Phase != Phase.Ask && i == d.Q.Correct, chosen = d.Phase != Phase.Ask && i == d.Chosen;
                    Color face = Tint(col, 0.72f), border = col;
                    float bw2 = 3 * u;
                    if (d.Phase != Phase.Ask)
                    {
                        if (right) { face = Color.FromArgb(160, 235, 140); border = Color.FromArgb(60, 170, 70); bw2 = 5 * u; }
                        else if (chosen) face = Color.FromArgb(255, 215, 180);
                        else face = Color.FromArgb(170, 225, 225, 230);
                    }
                    float shake = d.Phase == Phase.Wrong && chosen && d.Time < 0.5 ? (float)Math.Sin(d.Time * 40) * 4 * u * (float)(1 - d.Time * 2) : 0;
                    r.X += shake;
                    if (right && d.Time < 0.6) { float p = 6 * u * (float)Math.Sin(d.Time / 0.6 * Math.PI); r.Inflate(p, p); }
                    if (sel)
                    {
                        r.Inflate(4 * u, 4 * u);
                        var glow = r;
                        glow.Inflate((5 + 4 * pulse) * u, (5 + 4 * pulse) * u);
                        FillRound(g, glow, 28 * u, Color.FromArgb((int)(70 + 70 * pulse), 255, 220, 60));
                        border = Color.FromArgb(255, 200, 30);
                        bw2 = 5 * u;
                    }
                    var top2 = Button(g, r, face, border, bw2, hover);
                    var badge = new RectangleF(top2.X + 14 * u, top2.Y + top2.Height / 2 - 23 * u, 46 * u, 46 * u);
                    brush.Color = Color.FromArgb(70, 40, 20, 10);
                    g.FillEllipse(brush, badge.X, badge.Y + 3 * u, badge.Width, badge.Height);
                    brush.Color = right ? Color.FromArgb(60, 170, 70) : col;
                    g.FillEllipse(brush, badge);
                    pen.Color = Color.White;
                    pen.Width = 3 * u;
                    g.DrawEllipse(pen, badge);
                    if (right) CheckMark(g, badge.X + badge.Width / 2, badge.Y + badge.Height / 2, 13 * u);
                    else TextPop(g, (i + 1).ToString(), 26, badge.X + badge.Width / 2, badge.Y + badge.Height / 2 + 1 * u, Color.White, 1);
                    TextPlain(g, d.Q.Answers[i], 28, new RectangleF(top2.X + 68 * u, top2.Y + 4 * u, top2.Width - 78 * u, top2.Height - 8 * u), Ink);
                }
            }

            // footer
            string hint = d.Phase == Phase.Ask ? L.T("answer_hint") : L.T("continue");
            var foot = new RectangleF(bx, H - 52 * u, bw, 44 * u);
            if (d.Phase != Phase.Ask) Hit(new RectangleF(0, 0, W, H), 10);
            Text(g, hint + "      " + L.T("esc_close"), 21, foot.X + foot.Width / 2, foot.Y + 6 * u, Color.White);
        }

        void CheckMark(Graphics g, float cx, float cy, float s)
        {
            poly3[0] = new PointF(cx - s * 0.8f, cy);
            poly3[1] = new PointF(cx - s * 0.2f, cy + s * 0.6f);
            poly3[2] = new PointF(cx + s * 0.85f, cy - s * 0.6f);
            pen.Color = Color.White;
            pen.Width = s * 0.4f;
            g.DrawLines(pen, poly3);
        }

        /// <summary>Plain wrapped text without an outline (inside the light speech bubble).</summary>
        void TextPlain(Graphics g, string s, float size, RectangleF box, Color color)
        {
            if (string.IsNullOrEmpty(s)) return;
            var sf = Sf(StringAlignment.Center, true);
            string key = s + "\u0003" + (int)box.Width + "x" + (int)box.Height + "\u0003" + (int)(size * u * 8);
            float fit;
            if (!fitSizes.TryGetValue(key, out fit))
            {
                if (fitSizes.Count > 300) fitSizes.Clear();
                fit = FitSize(g, s, size, box.Width, box.Height, StringAlignment.Center);
                fitSizes[key] = fit;
            }
            brush.Color = color;
            g.DrawString(s, FontPx(fit), brush, box, sf);
        }

        void GiftReveal(Graphics g, Dialog d)
        {
            float t = (float)Math.Min(1, d.Time * 2);
            float cx = W / 2f + 120 * u, cy = H * 0.62f, s = 180 * u * (0.4f + 0.6f * EaseOutBack(t));
            Rays(g, cx, cy, s * 1.2f, 12, game.Time * 0.7, game.Time * 0.2, 120);
            g.DrawImage(game.Art.GiftIcons[d.Gift], cx - s / 2, cy - s / 2, s, s);
            for (int i = 0; i < 5; i++)
            {
                double a = game.Time * 1.3 + i * Math.PI * 2 / 5;
                Twinkle(g, cx + (float)Math.Cos(a) * s * 0.75f, cy + (float)Math.Sin(a) * s * 0.75f,
                        12 * u * (float)Math.Abs(Math.Sin(game.Time * 3 + i)), Color.FromArgb(240, 255, 250, 200));
            }
            cheerX = cx;
            cheerY = cy;
        }

        /// <summary>Confetti and a popping word after a good answer; a calm, floating word after a wrong one.</summary>
        void Celebration(Graphics g)
        {
            double t = game.CheerTime;
            if (game.CheerHappy) Burst(g, cheerX, cheerY, t, game.CheerSeed, 64);
            string s = game.Cheer;
            if (string.IsNullOrEmpty(s)) return;
            float size = game.CheerHappy ? 70 : 46;
            float half = Measure(g, s, size).Width / 2 + 12 * u;
            float cx = Math.Max(half, Math.Min(W - half, cheerX));
            int alpha = t < CheerLength - 0.4 ? 255 : (int)(255 * Math.Max(0, (CheerLength - t) / 0.4));
            if (game.CheerHappy)
            {
                float pop = t < 0.35 ? EaseOutBack((float)(t / 0.35)) : 1 + 0.04f * (float)Math.Sin(t * 10);
                Rays(g, cx, cheerY, 120 * u * pop, 10, t * 1.5, -1, alpha / 4);
                TextPop(g, s, size, cx, cheerY, Gold, pop, (float)Math.Sin(t * 7) * 4, alpha);
            }
            else
            {
                float pop = t < 0.3 ? 0.7f + 0.3f * EaseOutBack((float)(t / 0.3)) : 1;
                TextPop(g, s, size, cx, cheerY - (float)t * 16 * u, Color.FromArgb(175, 225, 255), pop, 0, alpha);
            }
        }

        // ------------------------------------------------------------------ floor screens

        void IntroBanner(Graphics g)
        {
            float t = EaseOutBack((float)Math.Min(1, game.StateTime * 2.5));
            var panel = new RectangleF(W / 2f - 440 * u, 250 * u, 880 * u, 330 * u);
            Hit(new RectangleF(0, 0, W, H), 10);
            var st = g.Save();
            float cx = W / 2f, cy = panel.Y + panel.Height / 2;
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(Math.Max(0.05f, t), Math.Max(0.05f, t));
            g.TranslateTransform(-cx, -cy);
            Panel(g, panel, Color.FromArgb(230, 60, 130, 205), Alpha(game.Theme.Accent, 235));
            Text(g, L.T("floor") + " " + (game.FloorIndex + 1), 38, W / 2f, panel.Y + 14 * u, Gold);
            Text(g, game.Theme.Name, 46, W / 2f, panel.Y + 62 * u, Color.White);
            for (int i = 0; i < 3; i++)
            {
                float y = panel.Y + 150 * u + i * 42 * u;
                Twinkle(g, panel.X + 40 * u, y + 18 * u, 11 * u * (0.8f + 0.2f * (float)Math.Sin(game.Time * 4 + i)), Gold);
                Text(g, L.T("goal" + (i + 1)), 24, W / 2f, y, Color.FromArgb(230, 255, 210));
            }
            if ((int)(game.Time * 2) % 2 == 0 || game.StateTime < 1)
                Text(g, L.T("press"), 20, W / 2f, panel.Y + 285 * u, Color.FromArgb(225, 225, 235));
            g.Restore(st);
        }

        void FloorDone(Graphics g)
        {
            Dim(g, 130);
            Rain(g, game.Time, 70);
            var panel = new RectangleF(W / 2f - 420 * u, H * 0.1f, 840 * u, 520 * u);
            Panel(g, panel, Color.FromArgb(230, 235, 125, 45));
            Hit(new RectangleF(0, 0, W, H), 10);
            float titlePop = EaseOutBack((float)Math.Min(1, game.StateTime * 2.5));
            TextPop(g, L.T("done"), 48, W / 2f, panel.Y + 52 * u, Gold, titlePop, (float)Math.Sin(game.Time * 2) * 2);
            Text(g, game.Theme.Name, 26, W / 2f, panel.Y + 86 * u, Color.White);
            for (int i = 0; i < 3; i++)
            {
                double appear = game.StateTime * 2.5 - i * 0.6 - 0.5;
                bool filled = i < game.LastStars && appear > 0;
                float pop = filled ? (float)(1 + 0.4 * Math.Max(0, 1 - appear)) : 1;
                float sx = W / 2f + (i - 1) * 130 * u, sy = panel.Y + 200 * u;
                if (filled) Rays(g, sx, sy, 80 * u, 8, game.Time * 0.8 + i, -1, 70);
                StarShape(g, sx, sy, 50 * u * pop, filled);
            }
            float s = 52 * u, x = W / 2f - (10 * s + 9 * 6 * u) / 2;
            for (int i = 0; i < 10; i++, x += s + 6 * u)
            {
                double k = game.StateTime - 1.4 - i * 0.08;
                if (k <= 0) continue;
                float pop = EaseOutBack((float)Math.Min(1, k * 4)), sz = s * pop;
                float hop = (float)Math.Abs(Math.Sin(game.Time * 3 + i * 0.6)) * 5 * u;
                g.DrawImage(game.Art.GiftIcons[i], x + s / 2 - sz / 2, panel.Y + 280 * u + s / 2 - sz / 2 - hop, sz, sz);
            }
            Text(g, L.T("mistakes") + ": " + game.Mistakes + "      " + L.T("coins") + ": " + game.Save.Coins, 28, W / 2f, panel.Y + 360 * u, Color.White);
            if (game.StateTime > 2.2)
                TextPop(g, L.T("cheer_floor"), 30, W / 2f, panel.Y + 420 * u, Color.FromArgb(255, 240, 150), EaseOutBack((float)Math.Min(1, (game.StateTime - 2.2) * 3)));
            if (game.StateTime > 1 && (int)(game.Time * 2) % 2 == 0)
                Text(g, L.T("next"), 26, W / 2f, panel.Y + 458 * u, Color.FromArgb(230, 255, 210));
        }

        void Victory(Graphics g)
        {
            Dim(g, 100);
            Rain(g, game.Time, Bits);
            // diploma
            var paper = new RectangleF(W / 2f - 400 * u, 70 * u, 800 * u, 520 * u);
            brush.Color = Color.FromArgb(90, 40, 20, 10);
            g.FillRectangle(brush, paper.X + 8 * u, paper.Y + 10 * u, paper.Width, paper.Height);
            brush.Color = Color.FromArgb(250, 250, 240, 215);
            g.FillRectangle(brush, paper);
            pen.Color = Color.FromArgb(200, 160, 60);
            pen.Width = 8 * u;
            g.DrawRectangle(pen, paper.X + 10 * u, paper.Y + 10 * u, paper.Width - 20 * u, paper.Height - 20 * u);
            Hit(new RectangleF(0, 0, W, H), 10);
            var dark = Color.FromArgb(90, 60, 30);
            TextPlain(g, L.T("diploma"), 60, new RectangleF(paper.X, paper.Y + 24 * u, paper.Width, 80 * u), Color.FromArgb(180, 120, 20));
            TextPlain(g, L.T("school_name"), 26, new RectangleF(paper.X + 30 * u, paper.Y + 110 * u, paper.Width - 60 * u, 40 * u), dark);
            TextPlain(g, L.T("school_patron"), 18, new RectangleF(paper.X + 30 * u, paper.Y + 148 * u, paper.Width - 60 * u, 30 * u), dark);
            float tcx = W / 2f, tcy = paper.Y + 243 * u;
            Rays(g, tcx, tcy, 95 * u, 12, game.Time * 0.6, game.Time * 0.15, 70);
            g.DrawImage(game.Art.TurtleIcon, tcx - 60 * u, paper.Y + 186 * u + (float)Math.Sin(game.Time * 3) * 6 * u, 120 * u, 114 * u);
            for (int i = 0; i < 2; i++)
                StarShape(g, tcx + (i == 0 ? -150 : 150) * u, tcy, 30 * u * (1 + 0.08f * (float)Math.Sin(game.Time * 4 + i * 2)), true);
            TextPlain(g, L.T("victory"), 34, new RectangleF(paper.X + 30 * u, paper.Y + 310 * u, paper.Width - 60 * u, 50 * u), Color.FromArgb(40, 140, 60));
            TextPlain(g, L.T("diploma2"), 22, new RectangleF(paper.X + 30 * u, paper.Y + 362 * u, paper.Width - 60 * u, 40 * u), dark);
            int stars = 0;
            foreach (var s in game.Save.Stars) stars += s;
            TextPlain(g, L.F("victory2", stars, School.Floors * 3), 26, new RectangleF(paper.X, paper.Y + 410 * u, paper.Width, 40 * u), dark);
            if (game.StateTime > 1.5) Text(g, L.T("press"), 24, W / 2f, paper.Bottom + 20 * u, Color.White);
        }
    }
}
