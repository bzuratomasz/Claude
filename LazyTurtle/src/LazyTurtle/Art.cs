using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace LazyTurtle
{
    /// <summary>A 32-bit pixel buffer used by the software renderer. A value of 0 means transparent.</summary>
    sealed class Tex
    {
        public readonly int W, H;
        public readonly int[] Px;

        public Tex(int w, int h)
        {
            W = w;
            H = h;
            Px = new int[w * h];
        }

        public static Tex FromBitmap(Bitmap bmp, bool transparent)
        {
            var t = new Tex(bmp.Width, bmp.Height);
            var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try { Marshal.Copy(data.Scan0, t.Px, 0, t.Px.Length); }
            finally { bmp.UnlockBits(data); }
            for (int i = 0; i < t.Px.Length; i++)
            {
                int c = t.Px[i];
                if (transparent && ((c >> 24) & 0xFF) < 128) t.Px[i] = 0;
                else t.Px[i] = c | unchecked((int)0xFF000000);
            }
            return t;
        }

        /// <summary>Cuts a wide bitmap into equally wide wall tiles (for blackboards and banners spanning several cells).</summary>
        public static Tex[] Split(Bitmap bmp, int parts)
        {
            var whole = FromBitmap(bmp, false);
            int w = whole.W / parts;
            var res = new Tex[parts];
            for (int p = 0; p < parts; p++)
            {
                var t = new Tex(w, whole.H);
                for (int y = 0; y < whole.H; y++)
                    Array.Copy(whole.Px, y * whole.W + p * w, t.Px, y * w, w);
                res[p] = t;
            }
            return res;
        }
    }

    /// <summary>
    /// All graphics of the game painted in code (school walls, floors, ceilings, characters, furniture,
    /// gifts), so the game ships as a single exe. Wall textures are 128x192 (a wall is 1 cell wide and
    /// 1.5 cells high), floors and ceilings 128x128.
    /// </summary>
    sealed partial class Art
    {
        public const int GiftKinds = 10;
        public enum D { Plant, Globe, Piano, Easel, Aquarium, Abacus, Telescope, Balls, Clock, Blocks, Shapes, Books, Bench, Fountain, Throne, Count }

        public readonly Tex[] PlayerIdle, PlayerWalk, PlayerJump;
        public readonly Tex[][] Teacher = new Tex[10][];
        public readonly Bitmap[][] TeacherPortraits = new Bitmap[10][];
        public readonly Tex[] Master;
        public readonly Bitmap[] MasterPortrait;
        public readonly Tex[][] Kid = new Tex[3][];
        public readonly Bitmap[] KidPortrait = new Bitmap[3];
        public readonly Tex[][] StudentDesk = new Tex[4][];
        public readonly Tex[] TeacherDesk, GiftBox, MarkerAsk, MarkerDone, Coin, Sparkle;
        public readonly Bitmap[] GiftIcons = new Bitmap[GiftKinds];
        public readonly Bitmap GiftBoxIcon, CoinIcon, TurtleIcon;
        public readonly Tex[][] Decor = new Tex[(int)D.Count][];

        static readonly FontFamily Family = LoadFamily();

        static FontFamily LoadFamily()
        {
            try { return new FontFamily("Comic Sans MS"); }
            catch (ArgumentException) { return FontFamily.GenericSansSerif; }
        }

        public Art()
        {
            PlayerIdle = new[] { Once(PlayerBmp(0)) };
            PlayerWalk = new[] { Once(PlayerBmp(1)), Once(PlayerBmp(0)), Once(PlayerBmp(2)), Once(PlayerBmp(0)) };
            PlayerJump = new[] { Once(PlayerBmp(3)) };
            TurtleIcon = PlayerBmp(0);

            for (int i = 0; i < 10; i++)
            {
                TeacherPortraits[i] = new[] { TeacherPortrait(i, false), TeacherPortrait(i, true) };
                Teacher[i] = new[] { Tex.FromBitmap(TeacherPortraits[i][0], true) };
            }
            MasterPortrait = new[] { MasterBmp(false), MasterBmp(true) };
            Master = new[] { Tex.FromBitmap(MasterPortrait[0], true) };
            for (int i = 0; i < 3; i++)
            {
                KidPortrait[i] = KidBmp(i, 0);
                Kid[i] = new[] { Tex.FromBitmap(KidPortrait[i], true), Once(KidBmp(i, 1)) };
            }
            for (int i = 0; i < 4; i++) StudentDesk[i] = new[] { Once(StudentDeskBmp(i)) };
            TeacherDesk = new[] { Once(TeacherDeskBmp()) };
            GiftBoxIcon = GiftBoxBmp();
            GiftBox = new[] { Tex.FromBitmap(GiftBoxIcon, true) };
            MarkerAsk = new[] { Once(MarkerBmp(false)) };
            MarkerDone = new[] { Once(MarkerBmp(true)) };
            for (int i = 0; i < GiftKinds; i++) GiftIcons[i] = GiftBmp(i);

            CoinIcon = CoinBmp(1f);
            Coin = new[] { Tex.FromBitmap(CoinIcon, true), Once(CoinBmp(0.6f)), Once(CoinBmp(0.18f)), Once(CoinBmp(0.6f)) };
            Sparkle = new[] { Once(SparkleBmp()) };

            Decor[(int)D.Plant] = new[] { Once(PlantBmp()) };
            Decor[(int)D.Globe] = new[] { Once(GlobeBmp()) };
            Decor[(int)D.Piano] = new[] { Once(PianoBmp()) };
            Decor[(int)D.Easel] = new[] { Once(EaselBmp()) };
            Decor[(int)D.Aquarium] = new[] { Once(AquariumBmp(0)), Once(AquariumBmp(1)) };
            Decor[(int)D.Abacus] = new[] { Once(AbacusBmp()) };
            Decor[(int)D.Telescope] = new[] { Once(TelescopeBmp()) };
            Decor[(int)D.Balls] = new[] { Once(BallsBmp()) };
            Decor[(int)D.Clock] = new[] { Once(ClockBmp()) };
            Decor[(int)D.Blocks] = new[] { Once(BlocksBmp("ABC")) };
            Decor[(int)D.Shapes] = new[] { Once(BlocksBmp("123")) };
            Decor[(int)D.Books] = new[] { Once(BooksBmp()) };
            Decor[(int)D.Bench] = new[] { Once(BenchBmp()) };
            Decor[(int)D.Fountain] = new[] { Once(FountainBmp()) };
            Decor[(int)D.Throne] = new[] { Once(ThroneBmp()) };
        }

        // ------------------------------------------------------------------ helpers

        static Tex Once(Bitmap b)
        {
            using (b) return Tex.FromBitmap(b, true);
        }

        static Bitmap Canvas(int w, int h, Action<Graphics> paint)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                g.Clear(Color.Transparent);
                paint(g);
            }
            return bmp;
        }

        static Tex Opaque(int w, int h, Action<Graphics> paint)
        {
            using (var b = Canvas(w, h, paint)) return Tex.FromBitmap(b, false);
        }

        static int Clamp(double v) => v < 0 ? 0 : v > 255 ? 255 : (int)v;
        public static Color C(int r, int g, int b) => Color.FromArgb(Clamp(r), Clamp(g), Clamp(b));
        static Color CA(int a, int r, int g, int b) => Color.FromArgb(Clamp(a), Clamp(r), Clamp(g), Clamp(b));
        static Color CA(int a, Color c) => Color.FromArgb(Clamp(a), c);
        public static Color Shade(Color c, int d) => C(c.R + d, c.G + d, c.B + d);
        static float F(Random r) => (float)r.NextDouble();
        static SolidBrush B(Color c) => new SolidBrush(c);
        static Pen P(Color c, float w) => new Pen(c, w) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };

        static void Grad(Graphics g, RectangleF r, Color a, Color b, float angle, bool ellipse)
        {
            if (r.Width < 1 || r.Height < 1) return;
            using (var br = new LinearGradientBrush(new RectangleF(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2), a, b, angle))
            {
                if (ellipse) g.FillEllipse(br, r);
                else g.FillRectangle(br, r);
            }
        }

        static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d < 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        static void FillRound(Graphics g, RectangleF r, float rad, Color c)
        {
            using (var p = RoundRect(r, rad))
            using (var b = B(c))
                g.FillPath(b, p);
        }

        static void FillRoundGrad(Graphics g, RectangleF r, float rad, Color a, Color b2, float angle)
        {
            using (var p = RoundRect(r, rad))
            using (var br = new LinearGradientBrush(new RectangleF(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2), a, b2, angle))
                g.FillPath(br, p);
        }

        static void StrokeRound(Graphics g, RectangleF r, float rad, Color c, float w)
        {
            using (var p = RoundRect(r, rad))
            using (var pen = P(c, w))
                g.DrawPath(pen, p);
        }

        public static PointF[] StarPoints(float cx, float cy, float outer, float inner)
        {
            var pts = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 5;
                float rad = i % 2 == 0 ? outer : inner;
                pts[i] = new PointF(cx + (float)Math.Cos(a) * rad, cy + (float)Math.Sin(a) * rad);
            }
            return pts;
        }

        static PointF[] HexPoints(float cx, float cy, float r)
        {
            var pts = new PointF[6];
            for (int i = 0; i < 6; i++)
            {
                double a = i * Math.PI / 3 + Math.PI / 6;
                pts[i] = new PointF(cx + (float)Math.Cos(a) * r, cy + (float)Math.Sin(a) * r);
            }
            return pts;
        }

        /// <summary>Draws text centered in a box, shrinking the font until it fits.</summary>
        static void TextIn(Graphics g, string s, RectangleF box, float size, Color color, bool bold = true)
        {
            var style = bold ? FontStyle.Bold : FontStyle.Regular;
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                Font f = null;
                try
                {
                    for (; size > 5; size -= 1)
                    {
                        f?.Dispose();
                        f = new Font(Family, size, style, GraphicsUnit.Pixel);
                        var m = g.MeasureString(s, f, new SizeF(box.Width + 200, box.Height), sf);
                        if (m.Width <= box.Width && m.Height <= box.Height * 1.1f) break;
                    }
                    using (var b = B(color)) g.DrawString(s, f, b, box, sf);
                }
                finally { f?.Dispose(); }
            }
        }

        static void Eye(Graphics g, float x, float y, float r, bool look = true)
        {
            g.FillEllipse(Brushes.White, x - r, y - r, 2 * r, 2 * r);
            if (look)
            {
                g.FillEllipse(B(C(30, 25, 20)), x - r * 0.55f, y - r * 0.5f, r * 1.1f, r * 1.2f);
                g.FillEllipse(Brushes.White, x - r * 0.3f, y - r * 0.4f, r * 0.4f, r * 0.4f);
            }
        }

        // ------------------------------------------------------------------ wall textures (128x192)

        public const int TW = 128, TH = 192;

        /// <summary>Crayon colors for bunting, friezes and other cheerful school details.</summary>
        static readonly Color[] Crayons = { C(240, 75, 70), C(255, 160, 40), C(255, 215, 50), C(90, 195, 90), C(60, 150, 235), C(160, 100, 220) };
        const string BuntingChars = "ABCDEFGHIJKLMNOPRSTUWZ123456789";

        /// <summary>Painted school wall: colored plaster, alphabet bunting, a dotted frieze, chair rail, wooden wainscot and skirting.</summary>
        static void WallBase(Graphics g, FloorTheme th, int width, int seed)
        {
            var r = new Random(seed);
            Grad(g, new RectangleF(0, 0, width, 124), Shade(th.Paint, 14), Shade(th.Paint, -8), 90f, false);
            for (int i = 0; i < width * 3; i++)
            {
                int d = r.Next(-12, 13);
                using (var b = B(CA(60, Shade(th.Paint, d)))) g.FillRectangle(b, F(r) * width, F(r) * 122, 2, 2);
            }
            // alphabet bunting under the ceiling: one swag per wall cell (period 128, so the walls tile)
            for (int x0 = 0; x0 < width; x0 += TW)
            {
                using (var pen = P(C(120, 95, 70), 1.2f)) g.DrawBezier(pen, x0, 3, x0 + 40, 13, x0 + 88, 13, x0 + TW, 3);
                for (int i = 0; i < 4; i++)
                {
                    float fx = x0 + 16 + i * 32, t = (fx - x0) / TW;
                    float fy = 3 + 30 * t * (1 - t);                    // follows the sagging string
                    int k = (seed * 3 + x0 / TW * 4 + i) % BuntingChars.Length;
                    var col = Crayons[(seed + x0 / TW * 4 + i) % Crayons.Length];
                    var flag = new[] { new PointF(fx - 12, fy - 1), new PointF(fx + 12, fy - 1), new PointF(fx, fy + 20) };
                    using (var b = B(CA(50, 0, 0, 0))) g.FillPolygon(b, new[] { new PointF(fx - 11, fy + 1), new PointF(fx + 13, fy + 1), new PointF(fx + 1, fy + 22) });
                    using (var b = B(col)) g.FillPolygon(b, flag);
                    using (var pen = P(Shade(col, -40), 1)) g.DrawPolygon(pen, flag);
                    TextIn(g, BuntingChars[k].ToString(), new RectangleF(fx - 8, fy, 16, 12), 11, Color.White);
                }
            }
            // a cheerful frieze: accent band with crayon dots (period 16, so the walls tile)
            using (var b = B(CA(200, th.Accent))) g.FillRectangle(b, 0, 103, width, 10);
            using (var b = B(CA(120, Color.White))) g.FillRectangle(b, 0, 103, width, 1.5f);
            for (int x = 0; x < width; x += 16)
                using (var b = B(Crayons[(x / 16) % Crayons.Length]))
                {
                    g.FillEllipse(Brushes.White, x + 4, 104.5f, 8, 7);
                    g.FillEllipse(b, x + 5, 105.5f, 6, 5);
                }
            // wainscot
            Grad(g, new RectangleF(0, 124, width, 58), Shade(th.Wood, 18), Shade(th.Wood, -12), 90f, false);
            for (int x = 0; x < width; x += 32)
            {
                var panel = new RectangleF(x + 4, 132, 24, 42);
                Grad(g, panel, Shade(th.Wood, -8), Shade(th.Wood, 18), 90f, false);
                using (var pen = P(Shade(th.Wood, -36), 1.2f)) g.DrawRectangle(pen, panel.X, panel.Y, panel.Width, panel.Height);
                using (var pen = P(Shade(th.Wood, -32), 1)) g.DrawLine(pen, x, 124, x, 182);
                // a little painted star on every panel
                using (var b = B(CA(190, Crayons[(x / 32 + seed) % Crayons.Length])))
                    g.FillPolygon(b, StarPoints(panel.X + 12, panel.Y + 21, 6.5f, 2.8f));
            }
            Grad(g, new RectangleF(0, 118, width, 8), Shade(th.Wood, 40), Shade(th.Wood, -10), 90f, false);
            Grad(g, new RectangleF(0, 182, width, 10), Shade(th.Wood, -45), Shade(th.Wood, -70), 90f, false);
        }

        static Tex Wall(FloorTheme th, int seed, Action<Graphics> extra)
        {
            return Opaque(TW, TH, g =>
            {
                WallBase(g, th, TW, seed);
                extra?.Invoke(g);
            });
        }

        public static Tex PaintedWall(FloorTheme th) => Wall(th, 1, null);

        public static Tex Lockers(FloorTheme th, int number)
        {
            return Wall(th, 2, g =>
            {
                for (int i = 0; i < 2; i++)
                {
                    var rect = new RectangleF(i * 64 + 2, 14, 60, 168);
                    Grad(g, rect, Shade(th.Accent, 35), Shade(th.Accent, -30), 0f, false);
                    using (var pen = P(Shade(th.Accent, -60), 2)) g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                    using (var pen = P(Shade(th.Accent, -50), 2))
                        for (int v = 0; v < 4; v++) g.DrawLine(pen, rect.X + 14, 26 + v * 6, rect.Right - 14, 26 + v * 6);
                    FillRound(g, new RectangleF(rect.Right - 14, 96, 6, 22), 3, C(200, 200, 205));
                    FillRound(g, new RectangleF(rect.X + 16, 58, 28, 14), 3, C(245, 245, 235));
                    TextIn(g, (number + i).ToString(), new RectangleF(rect.X + 16, 58, 28, 14), 12, C(60, 60, 60));
                    using (var pen = P(Shade(th.Accent, -50), 2))
                        for (int v = 0; v < 4; v++) g.DrawLine(pen, rect.X + 14, 156 + v * 5, rect.Right - 14, 156 + v * 5);
                }
            });
        }

        public static Tex Poster(FloorTheme th, int variant)
        {
            return Wall(th, 3 + variant, g =>
            {
                var r = new Random(variant * 13 + 5);
                float rot = (F(r) - 0.5f) * 4;
                var st = g.Save();
                g.TranslateTransform(64, 60);
                g.RotateTransform(rot);
                var paper = new RectangleF(-42, -44, 84, 88);
                g.FillRectangle(B(CA(60, 0, 0, 0)), paper.X + 3, paper.Y + 3, paper.Width, paper.Height);
                g.FillRectangle(Brushes.White, paper);
                switch (variant % 6)
                {
                    case 0:
                        TextIn(g, "A", new RectangleF(-40, -42, 40, 44), 40, C(230, 60, 60));
                        TextIn(g, "B", new RectangleF(0, -42, 40, 44), 40, C(60, 140, 230));
                        TextIn(g, "C", new RectangleF(-20, 0, 40, 44), 40, C(60, 180, 70));
                        break;
                    case 1:
                        TextIn(g, "1 2 3", new RectangleF(-40, -40, 80, 30), 26, C(230, 120, 30));
                        for (int i = 0; i < 6; i++)
                            g.FillEllipse(B(C(80 + i * 25, 120, 230 - i * 20)), -36 + i * 12, 4, 10, 10);
                        TextIn(g, "4 5 6", new RectangleF(-40, 16, 80, 26), 22, C(120, 60, 200));
                        break;
                    case 2:
                        g.FillRectangle(B(C(20, 25, 60)), paper);
                        g.FillEllipse(B(C(255, 210, 60)), -56, -16, 34, 34);
                        var planets = new[] { C(180, 170, 160), C(230, 180, 90), C(60, 130, 230), C(220, 90, 60), C(230, 190, 140) };
                        for (int i = 0; i < planets.Length; i++)
                        {
                            float s = 6 + (i % 3) * 3;
                            g.FillEllipse(B(planets[i]), -18 + i * 12, -s / 2 + (i % 2) * 8 - 4, s, s);
                        }
                        for (int i = 0; i < 14; i++) g.FillRectangle(Brushes.White, -40 + F(r) * 80, -42 + F(r) * 84, 1.5f, 1.5f);
                        break;
                    case 3:
                        g.FillRectangle(B(C(120, 190, 240)), paper);
                        g.FillEllipse(B(C(90, 180, 80)), -36, -30, 30, 24);
                        g.FillEllipse(B(C(90, 180, 80)), 0, -34, 34, 30);
                        g.FillEllipse(B(C(90, 180, 80)), -10, 4, 22, 30);
                        g.FillEllipse(B(C(90, 180, 80)), 20, 14, 18, 14);
                        break;
                    case 4:
                        g.FillEllipse(B(C(120, 190, 90)), -26, -30, 52, 46);
                        Eye(g, -10, -12, 6);
                        Eye(g, 10, -12, 6);
                        using (var pen = P(C(40, 80, 30), 2.5f)) g.DrawArc(pen, -12, -6, 24, 14, 20, 140);
                        g.FillEllipse(B(C(110, 160, 60)), -34, 16, 68, 26);
                        using (var pen = P(C(230, 200, 90), 2))
                            g.DrawPolygon(pen, HexPoints(0, 29, 10));
                        break;
                    default:
                        var cols = new[] { C(230, 60, 60), C(250, 150, 40), C(250, 220, 50), C(80, 190, 80), C(60, 130, 230), C(140, 80, 200) };
                        for (int i = 0; i < cols.Length; i++)
                            using (var pen = new Pen(cols[i], 5)) g.DrawArc(pen, -36 + i * 5, -26 + i * 5, 72 - i * 10, 72 - i * 10, 180, 180);
                        g.FillEllipse(B(C(255, 220, 70)), 16, -40, 22, 22);
                        break;
                }
                g.FillEllipse(B(C(220, 40, 40)), -3, -47, 6, 6);
                g.Restore(st);
            });
        }

        public static Tex Notice(FloorTheme th)
        {
            return Wall(th, 11, g =>
            {
                var board = new RectangleF(8, 18, 112, 92);
                g.FillRectangle(B(C(120, 80, 45)), board.X - 4, board.Y - 4, board.Width + 8, board.Height + 8);
                g.FillRectangle(B(C(200, 155, 100)), board);
                var r = new Random(4);
                for (int i = 0; i < 160; i++) g.FillRectangle(B(C(170, 125, 80)), board.X + F(r) * board.Width, board.Y + F(r) * board.Height, 2, 2);
                var papers = new[] { Color.White, C(255, 250, 170), C(190, 230, 255), C(255, 200, 220), C(200, 255, 200) };
                for (int i = 0; i < 6; i++)
                {
                    var st = g.Save();
                    g.TranslateTransform(board.X + 20 + (i % 3) * 36, board.Y + 22 + (i / 3) * 44);
                    g.RotateTransform((F(r) - 0.5f) * 14);
                    g.FillRectangle(B(papers[i % papers.Length]), -14, -16, 28, 32);
                    if (i == 1)
                    {
                        // a child's crayon drawing: sun, grass and a flower
                        g.FillEllipse(B(C(255, 200, 40)), 2, -12, 10, 10);
                        using (var pen = P(C(255, 170, 30), 1))
                            for (int k = 0; k < 8; k++)
                            {
                                double a = k * Math.PI / 4;
                                g.DrawLine(pen, 7 + (float)Math.Cos(a) * 6.5f, -7 + (float)Math.Sin(a) * 6.5f, 7 + (float)Math.Cos(a) * 9, -7 + (float)Math.Sin(a) * 9);
                            }
                        g.FillRectangle(B(C(90, 190, 80)), -14, 10, 28, 6);
                        using (var pen = P(C(60, 150, 60), 1.5f)) g.DrawLine(pen, -6, 11, -6, 0);
                        g.FillEllipse(B(C(240, 80, 120)), -10, -5, 8, 8);
                        g.FillEllipse(B(C(255, 230, 80)), -8, -3, 4, 4);
                    }
                    else if (i == 5)
                    {
                        // a child's crayon drawing: a house with a red roof
                        g.FillRectangle(B(C(255, 220, 120)), -9, -2, 18, 14);
                        g.FillPolygon(B(C(230, 70, 60)), new[] { new PointF(-12, -2), new PointF(0, -13), new PointF(12, -2) });
                        g.FillRectangle(B(C(70, 140, 230)), -6, 2, 5, 5);
                        g.FillRectangle(B(C(150, 90, 50)), 2, 4, 5, 8);
                    }
                    else
                        using (var pen = P(C(140, 140, 160), 1))
                            for (int l = 0; l < 4; l++) g.DrawLine(pen, -10, -8 + l * 6, 10, -8 + l * 6);
                    g.FillEllipse(B(new[] { C(230, 50, 50), C(50, 120, 230), C(60, 180, 60) }[i % 3]), -3, -19, 6, 6);
                    g.Restore(st);
                }
            });
        }

        public static Tex Window(FloorTheme th, int seed)
        {
            return Wall(th, 12, g =>
            {
                var glass = new RectangleF(18, 16, 92, 94);
                Grad(g, glass, C(90, 165, 240), C(200, 235, 255), 90f, false);
                var r = new Random(seed);
                g.FillEllipse(B(C(255, 250, 210)), glass.X + 60, glass.Y + 10, 18, 18);
                for (int i = 0; i < 4; i++)
                    g.FillEllipse(Brushes.White, glass.X + 6 + F(r) * 40 + i * 6, glass.Y + 22 + F(r) * 6, 24, 12);
                g.FillEllipse(B(C(80, 160, 70)), glass.X - 10, glass.Bottom - 30, 60, 46);
                g.FillEllipse(B(C(60, 140, 60)), glass.X + 40, glass.Bottom - 24, 64, 40);
                // frame and bars
                using (var pen = new Pen(Color.White, 6)) g.DrawRectangle(pen, glass.X, glass.Y, glass.Width, glass.Height);
                using (var pen = new Pen(Color.White, 4))
                {
                    g.DrawLine(pen, glass.X + glass.Width / 2, glass.Y, glass.X + glass.Width / 2, glass.Bottom);
                    g.DrawLine(pen, glass.X, glass.Y + glass.Height / 2, glass.Right, glass.Y + glass.Height / 2);
                }
                Grad(g, new RectangleF(10, 110, 108, 9), C(250, 250, 250), C(200, 200, 205), 90f, false);
                // curtains
                foreach (var x in new[] { 4f, 108f })
                {
                    var cur = new RectangleF(x, 8, 16, 108);
                    Grad(g, cur, Shade(th.Accent, 30), Shade(th.Accent, -20), 0f, false);
                    using (var pen = P(Shade(th.Accent, -40), 1))
                        for (int k = 1; k < 4; k++) g.DrawLine(pen, x + k * 4, 10, x + k * 4, 114);
                }
                g.FillRectangle(B(C(150, 110, 70)), 0, 5, 128, 5);
            });
        }

        public static Tex Bookshelf(FloorTheme th, int seed)
        {
            return Wall(th, 13, g =>
            {
                var frame = new RectangleF(6, 10, 116, 172);
                g.FillRectangle(B(C(110, 70, 40)), frame);
                g.FillRectangle(B(C(70, 42, 22)), frame.X + 6, frame.Y + 6, frame.Width - 12, frame.Height - 12);
                var r = new Random(seed);
                var colors = new[] { C(200, 50, 50), C(50, 110, 200), C(60, 160, 70), C(230, 180, 40), C(140, 70, 170), C(230, 120, 40), C(40, 150, 160) };
                for (int s = 0; s < 4; s++)
                {
                    float shelfY = frame.Y + 8 + s * 41;
                    float x = frame.X + 8;
                    while (x < frame.Right - 14)
                    {
                        float w = 6 + r.Next(6), h = 26 + r.Next(10);
                        var col = colors[r.Next(colors.Length)];
                        Grad(g, new RectangleF(x, shelfY + 36 - h, w, h), Shade(col, 25), Shade(col, -25), 0f, false);
                        g.FillRectangle(B(CA(200, 255, 235, 150)), x + 1, shelfY + 36 - h + 5, w - 2, 2);
                        x += w + 1;
                    }
                    Grad(g, new RectangleF(frame.X + 4, shelfY + 36, frame.Width - 8, 5), C(150, 100, 60), C(90, 55, 30), 90f, false);
                }
            });
        }

        public static Tex Sign(FloorTheme th, string label, string subject, Color color)
        {
            return Wall(th, 14, g =>
            {
                var plate = new RectangleF(14, 30, 100, 64);
                FillRound(g, new RectangleF(plate.X + 3, plate.Y + 3, plate.Width, plate.Height), 8, CA(70, 0, 0, 0));
                FillRoundGrad(g, plate, 8, Color.White, C(232, 232, 228), 90f);
                FillRound(g, new RectangleF(plate.X, plate.Y, 12, plate.Height), 6, color);
                StrokeRound(g, plate, 8, Shade(color, -40), 2.5f);
                TextIn(g, label, new RectangleF(plate.X + 14, plate.Y + 2, plate.Width - 16, 34), 30, C(40, 40, 50));
                TextIn(g, subject, new RectangleF(plate.X + 14, plate.Y + 36, plate.Width - 16, 26), 15, Shade(color, -50));
                // arrow pointing to the door on the right
                using (var b = B(color))
                    g.FillPolygon(b, new[] { new PointF(96, 100), new PointF(112, 106), new PointF(96, 112) });
            });
        }

        /// <summary>A blackboard spanning two wall cells with chalk drawings for the subject.</summary>
        public static Tex[] Blackboard(FloorTheme th, int subject, string title)
        {
            using (var bmp = Canvas(TW * 2, TH, g =>
            {
                WallBase(g, th, TW * 2, 20 + subject);
                var board = new RectangleF(12, 14, 232, 100);
                g.FillRectangle(B(C(130, 85, 45)), board.X - 6, board.Y - 6, board.Width + 12, board.Height + 12);
                Grad(g, board, C(40, 85, 60), C(30, 70, 50), 90f, false);
                var r = new Random(subject);
                for (int i = 0; i < 60; i++) g.FillEllipse(B(CA(25, 255, 255, 255)), board.X + F(r) * board.Width, board.Y + F(r) * board.Height, 8, 3);
                Grad(g, new RectangleF(board.X - 4, board.Bottom + 4, board.Width + 8, 6), C(150, 100, 55), C(100, 65, 35), 90f, false);
                g.FillRectangle(Brushes.White, 60, board.Bottom + 1, 12, 4);
                g.FillRectangle(B(C(255, 220, 120)), 80, board.Bottom + 1, 10, 4);

                var chalk = CA(230, 250, 250, 245);
                TextIn(g, title, new RectangleF(board.X + 4, board.Y + 2, board.Width - 8, 24), 20, chalk);
                var art = new RectangleF(board.X + 10, board.Y + 28, board.Width - 20, board.Height - 32);
                ChalkArt(g, subject, art, chalk);
            }))
                return Tex.Split(bmp, 2);
        }

        static void ChalkArt(Graphics g, int subject, RectangleF a, Color chalk)
        {
            using (var pen = P(chalk, 2.5f))
            using (var yellow = P(CA(230, 255, 230, 120), 2.5f))
            using (var pink = P(CA(230, 255, 160, 190), 2.5f))
            {
                float cx = a.X + a.Width / 2, cy = a.Y + a.Height / 2;
                switch (subject)
                {
                    case 0:
                        TextIn(g, "2 + 3 = 5", new RectangleF(a.X, a.Y, a.Width / 2, a.Height / 2), 22, chalk);
                        TextIn(g, "7 - 4 = 3", new RectangleF(a.X, cy, a.Width / 2, a.Height / 2), 22, chalk);
                        TextIn(g, "2 x 5 = 10", new RectangleF(cx, a.Y + 10, a.Width / 2, a.Height / 2), 22, CA(230, 255, 230, 120));
                        break;
                    case 1:
                        TextIn(g, "A a   B b   C c", new RectangleF(a.X, a.Y, a.Width, a.Height / 2), 24, chalk);
                        TextIn(g, L.Pl ? "Ala ma kota" : "I like books", new RectangleF(a.X, cy, a.Width, a.Height / 2), 22, CA(230, 255, 230, 120));
                        break;
                    case 2:
                        g.DrawLine(pen, cx - 50, a.Bottom, cx - 50, cy);
                        g.DrawEllipse(pen, cx - 74, a.Y, 48, 40);
                        g.DrawEllipse(yellow, cx + 30, a.Y + 2, 26, 26);
                        for (int i = 0; i < 8; i++)
                        {
                            double ang = i * Math.PI / 4;
                            g.DrawLine(yellow, cx + 43 + (float)Math.Cos(ang) * 17, a.Y + 15 + (float)Math.Sin(ang) * 17, cx + 43 + (float)Math.Cos(ang) * 24, a.Y + 15 + (float)Math.Sin(ang) * 24);
                        }
                        g.DrawEllipse(pink, cx - 4, a.Bottom - 22, 12, 12);
                        g.DrawLine(pen, cx + 2, a.Bottom - 10, cx + 2, a.Bottom);
                        break;
                    case 3:
                        g.DrawEllipse(pen, cx - 34, a.Y, 66, 66);
                        g.DrawEllipse(pen, cx - 14, a.Y, 26, 66);
                        g.DrawLine(pen, cx - 34, a.Y + 33, cx + 32, a.Y + 33);
                        TextIn(g, "N", new RectangleF(a.Right - 40, a.Y - 2, 30, 20), 16, chalk);
                        g.DrawLine(yellow, a.Right - 25, a.Y + 20, a.Right - 25, a.Y + 60);
                        g.DrawLine(yellow, a.Right - 45, a.Y + 40, a.Right - 5, a.Y + 40);
                        break;
                    case 4:
                        TextIn(g, L.Pl ? "pies = dog" : "dog = pies", new RectangleF(a.X, a.Y, a.Width, a.Height / 2), 22, chalk);
                        TextIn(g, L.Pl ? "kot = cat" : "cat = kot", new RectangleF(a.X, cy, a.Width, a.Height / 2), 22, CA(230, 255, 230, 120));
                        break;
                    case 5:
                        TextIn(g, "1, 2, 3, ?", new RectangleF(a.X, a.Y, a.Width / 2, a.Height), 22, chalk);
                        g.DrawRectangle(pen, cx + 10, cy - 14, 24, 24);
                        g.DrawEllipse(yellow, cx + 44, cy - 14, 24, 24);
                        g.DrawPolygon(pink, new[] { new PointF(cx + 90, cy - 16), new PointF(cx + 104, cy + 10), new PointF(cx + 76, cy + 10) });
                        break;
                    case 6:
                        g.DrawEllipse(pen, cx - 30, a.Y + 2, 60, 60);
                        g.DrawLine(pen, cx, a.Y + 32, cx, a.Y + 10);
                        g.DrawLine(yellow, cx, a.Y + 32, cx + 18, a.Y + 32);
                        for (int i = 0; i < 12; i++)
                        {
                            double ang = i * Math.PI / 6;
                            g.DrawLine(pen, cx + (float)Math.Cos(ang) * 25, a.Y + 32 + (float)Math.Sin(ang) * 25, cx + (float)Math.Cos(ang) * 29, a.Y + 32 + (float)Math.Sin(ang) * 29);
                        }
                        break;
                    case 7:
                        for (int i = 0; i < 5; i++) g.DrawLine(pen, a.X, a.Y + 8 + i * 9, a.Right, a.Y + 8 + i * 9);
                        foreach (var nx in new[] { 40f, 80f, 120f, 160f })
                        {
                            float ny = a.Y + 10 + (nx / 40 % 3) * 9;
                            g.FillEllipse(B(chalk), a.X + nx, ny + 6, 10, 8);
                            g.DrawLine(pen, a.X + nx + 9, ny + 9, a.X + nx + 9, ny - 16);
                        }
                        break;
                    case 8:
                        g.DrawEllipse(pink, cx - 70, a.Y + 6, 44, 44);
                        g.DrawLine(pen, cx - 48, a.Y + 6, cx - 44, a.Y - 4);
                        using (var heart = new GraphicsPath())
                        {
                            heart.AddBezier(cx + 40, a.Y + 14, cx + 40, a.Y, cx + 10, a.Y, cx + 10, a.Y + 18);
                            heart.AddBezier(cx + 10, a.Y + 18, cx + 10, a.Y + 34, cx + 40, a.Y + 50, cx + 40, a.Y + 56);
                            heart.AddBezier(cx + 40, a.Y + 56, cx + 40, a.Y + 50, cx + 70, a.Y + 34, cx + 70, a.Y + 18);
                            heart.AddBezier(cx + 70, a.Y + 18, cx + 70, a.Y, cx + 40, a.Y, cx + 40, a.Y + 14);
                            g.DrawPath(pink, heart);
                        }
                        break;
                    default:
                        g.DrawEllipse(pen, cx - 24, a.Y + 6, 48, 48);
                        g.DrawEllipse(yellow, cx - 50, a.Y + 22, 100, 18);
                        foreach (var s in new[] { new PointF(a.X + 20, a.Y + 10), new PointF(a.Right - 30, a.Y + 40), new PointF(a.X + 40, a.Bottom - 10) })
                            g.DrawPolygon(pen, StarPoints(s.X, s.Y, 8, 3));
                        break;
                }
            }
        }

        public static Tex[] Banner(FloorTheme th, string text, string subtitle)
        {
            using (var bmp = Canvas(TW * 3, TH, g =>
            {
                WallBase(g, th, TW * 3, 30);
                var cloth = new RectangleF(24, 16, TW * 3 - 48, 76);
                using (var p = new GraphicsPath())
                {
                    p.AddLine(cloth.X, cloth.Y, cloth.Right, cloth.Y);
                    p.AddLine(cloth.Right, cloth.Y, cloth.Right - 14, cloth.Y + cloth.Height / 2);
                    p.AddLine(cloth.Right - 14, cloth.Y + cloth.Height / 2, cloth.Right, cloth.Bottom);
                    p.AddLine(cloth.Right, cloth.Bottom, cloth.X, cloth.Bottom);
                    p.AddLine(cloth.X, cloth.Bottom, cloth.X + 14, cloth.Y + cloth.Height / 2);
                    p.CloseFigure();
                    using (var br = new LinearGradientBrush(cloth, Shade(th.Accent, 30), Shade(th.Accent, -30), 90f)) g.FillPath(br, p);
                    using (var pen = P(C(255, 215, 90), 3)) g.DrawPath(pen, p);
                }
                TextIn(g, text, new RectangleF(cloth.X + 60, cloth.Y + 4, cloth.Width - 80, 44), 32, Color.White);
                TextIn(g, subtitle, new RectangleF(cloth.X + 60, cloth.Y + 46, cloth.Width - 80, 26), 16, C(255, 235, 160));
                // turtle emblem
                float ex = cloth.X + 40, ey = cloth.Y + cloth.Height / 2;
                g.FillEllipse(B(C(120, 190, 90)), ex - 8, ey - 30, 16, 16);
                g.FillEllipse(B(C(90, 150, 60)), ex - 20, ey - 18, 40, 38);
                using (var pen = P(C(255, 220, 100), 2)) g.DrawPolygon(pen, HexPoints(ex, ey + 1, 10));
            }))
                return Tex.Split(bmp, 3);
        }

        public static Tex Entrance(FloorTheme th, string label)
        {
            return Wall(th, 15, g =>
            {
                var frame = new RectangleF(8, 24, 112, 160);
                g.FillRectangle(B(C(90, 60, 35)), frame);
                for (int i = 0; i < 2; i++)
                {
                    var door = new RectangleF(14 + i * 52, 30, 48, 154);
                    Grad(g, door, C(170, 115, 70), C(120, 80, 45), 0f, false);
                    Grad(g, new RectangleF(door.X + 8, door.Y + 10, door.Width - 16, 70), C(170, 215, 245), C(120, 175, 220), 90f, false);
                    g.FillRectangle(B(C(210, 210, 215)), door.X + 4, 110, door.Width - 8, 6);
                }
                FillRound(g, new RectangleF(28, 4, 72, 18), 4, C(40, 150, 70));
                TextIn(g, label, new RectangleF(28, 4, 72, 18), 13, Color.White);
            });
        }

        public static Tex StairsDown(FloorTheme th)
        {
            return Wall(th, 16, g =>
            {
                var hole = new RectangleF(14, 30, 100, 154);
                g.FillRectangle(B(C(60, 50, 45)), hole);
                for (int i = 0; i < 8; i++)
                {
                    float y = hole.Y + 60 + i * 12;
                    Grad(g, new RectangleF(hole.X + 4, y, hole.Width - 8, 10), C(150 - i * 10, 140 - i * 10, 130 - i * 10), C(110 - i * 10, 100 - i * 10, 95 - i * 10), 90f, false);
                }
                g.FillRectangle(B(C(140, 100, 60)), hole.X, hole.Y, 6, hole.Height);
                g.FillRectangle(B(C(140, 100, 60)), hole.Right - 6, hole.Y, 6, hole.Height);
                using (var b = B(C(40, 150, 70)))
                    g.FillPolygon(b, new[] { new PointF(52, 6), new PointF(76, 6), new PointF(76, 14), new PointF(84, 14), new PointF(64, 28), new PointF(44, 14), new PointF(52, 14) });
            });
        }

        // ---- master hall

        static void RoyalBase(Graphics g, int width)
        {
            g.FillRectangle(B(C(125, 62, 150)), 0, 0, width, 124);
            using (var pen = P(CA(160, 230, 190, 90), 1.5f))
                for (int x = 0; x < width; x += 32)
                    for (int y = 0; y < 124; y += 32)
                        g.DrawPolygon(pen, new[] { new PointF(x + 16, y + 2), new PointF(x + 30, y + 16), new PointF(x + 16, y + 30), new PointF(x + 2, y + 16) });
            using (var b = B(CA(180, 230, 190, 90)))
                for (int x = 0; x < width; x += 32)
                    for (int y = 0; y < 124; y += 32)
                        g.FillEllipse(b, x + 14, y + 14, 4, 4);
            Grad(g, new RectangleF(0, 124, width, 58), C(90, 50, 30), C(60, 32, 18), 90f, false);
            for (int x = 0; x < width; x += 32)
                using (var pen = P(C(200, 160, 70), 1.5f)) g.DrawRectangle(pen, x + 5, 132, 22, 42);
            Grad(g, new RectangleF(0, 118, width, 8), C(240, 200, 90), C(170, 120, 40), 90f, false);
            Grad(g, new RectangleF(0, 182, width, 10), C(60, 30, 15), C(40, 20, 10), 90f, false);
        }

        public static Tex Royal() => Opaque(TW, TH, g => RoyalBase(g, TW));

        public const int PaintingKinds = 5;
        public const int PaintWhiteLady = 0, PaintTytus = 1, PaintCelina = 2, PaintJan = 3, PaintCastle = 4;

        /// <summary>
        /// Stylized paintings from Kórnik in a gilded frame: the White Lady (Teofila z Działyńskich
        /// Szołdrska-Potulicka, the school's patron), Tytus, Celina and Jan Kanty Działyński, and Kórnik Castle.
        /// Hung on the royal wallpaper (royal = true) or on a painted school wall.
        /// </summary>
        public static Tex Painting(int kind, FloorTheme th, bool royal)
        {
            return Opaque(TW, TH, g =>
            {
                if (royal) RoyalBase(g, TW);
                else WallBase(g, th, TW, 40 + kind);
                var frame = new RectangleF(16, 6, 96, 108);
                g.FillRectangle(B(CA(80, 0, 0, 0)), frame.X + 3, frame.Y + 3, frame.Width, frame.Height);
                Grad(g, frame, C(255, 220, 110), C(165, 115, 35), 45f, false);
                using (var pen = P(C(130, 85, 20), 1.5f))
                {
                    g.DrawRectangle(pen, frame.X + 3, frame.Y + 3, frame.Width - 6, frame.Height - 6);
                    for (int i = 0; i < 12; i++)
                        g.DrawEllipse(pen, frame.X + 2 + i * 8, frame.Y, 4, 4);
                }
                var pic = new RectangleF(24, 14, 80, 92);
                var st = g.Save();
                g.SetClip(pic);
                PaintPicture(g, kind, pic);
                g.Restore(st);
                using (var pen = P(C(110, 70, 15), 2)) g.DrawRectangle(pen, pic.X, pic.Y, pic.Width, pic.Height);
            });
        }

        static void PaintPicture(Graphics g, int kind, RectangleF p)
        {
            float cx = p.X + p.Width / 2;
            if (kind == PaintCastle)
            {
                Grad(g, new RectangleF(p.X, p.Y, p.Width, 56), C(120, 170, 225), C(225, 235, 245), 90f, false);
                g.FillEllipse(Brushes.White, p.X + 6, p.Y + 8, 26, 10);
                g.FillEllipse(Brushes.White, p.X + 50, p.Y + 14, 22, 8);
                g.FillEllipse(B(C(60, 120, 60)), p.X - 10, p.Y + 40, 40, 30);
                g.FillEllipse(B(C(50, 105, 55)), p.Right - 26, p.Y + 38, 40, 32);
                // the castle: cream walls, red roofs, crenellated towers
                var wall = C(235, 225, 200);
                g.FillRectangle(B(wall), cx - 26, p.Y + 40, 52, 22);
                g.FillRectangle(B(Shade(wall, -15)), cx - 34, p.Y + 30, 14, 32);
                g.FillRectangle(B(Shade(wall, -15)), cx + 20, p.Y + 26, 14, 36);
                for (int i = 0; i < 3; i++)
                {
                    g.FillRectangle(B(Shade(wall, -15)), cx - 34 + i * 5, p.Y + 27, 3, 4);
                    g.FillRectangle(B(Shade(wall, -15)), cx + 20 + i * 5, p.Y + 23, 3, 4);
                }
                g.FillPolygon(B(C(170, 60, 50)), new[] { new PointF(cx - 28, p.Y + 40), new PointF(cx + 28, p.Y + 40), new PointF(cx + 18, p.Y + 32), new PointF(cx - 18, p.Y + 32) });
                g.FillPolygon(B(C(170, 60, 50)), new[] { new PointF(cx - 4, p.Y + 32), new PointF(cx + 4, p.Y + 32), new PointF(cx, p.Y + 18) });
                for (int i = 0; i < 4; i++) g.FillRectangle(B(C(70, 80, 110)), cx - 20 + i * 11, p.Y + 46, 5, 8);
                // the lake with the reflection
                Grad(g, new RectangleF(p.X, p.Y + 62, p.Width, p.Height - 62), C(80, 140, 200), C(40, 90, 150), 90f, false);
                g.FillRectangle(B(CA(90, 235, 225, 200)), cx - 26, p.Y + 64, 52, 10);
                using (var pen = P(CA(140, 255, 255, 255), 1))
                    for (int i = 0; i < 5; i++) g.DrawLine(pen, p.X + 8 + i * 14, p.Y + 78 + (i % 2) * 6, p.X + 18 + i * 14, p.Y + 78 + (i % 2) * 6);
                return;
            }

            // portraits: dark varnished background, figure from the waist up
            bool lady = kind == PaintWhiteLady || kind == PaintCelina;
            Grad(g, p, kind == PaintWhiteLady ? C(120, 135, 150) : C(85, 70, 55), kind == PaintWhiteLady ? C(60, 70, 85) : C(40, 30, 25), 90f, false);
            var face = C(245, 215, 190);
            Color dress = kind == PaintWhiteLady ? C(250, 250, 248) : kind == PaintCelina ? C(40, 55, 110) : C(35, 35, 45);
            // body
            using (var body = new GraphicsPath())
            {
                body.AddBezier(cx - 40, p.Bottom, cx - 34, p.Y + 66, cx - 20, p.Y + 58, cx, p.Y + 58);
                body.AddBezier(cx, p.Y + 58, cx + 20, p.Y + 58, cx + 34, p.Y + 66, cx + 40, p.Bottom);
                body.CloseFigure();
                using (var br = new LinearGradientBrush(new RectangleF(cx - 40, p.Y + 56, 80, p.Bottom - p.Y - 56), Shade(dress, 10), Shade(dress, kind == PaintWhiteLady ? -40 : -20), 0f))
                    g.FillPath(br, body);
            }
            if (kind == PaintWhiteLady)
            {
                // 18th-century white gown with lace and a pearl necklace
                g.FillPolygon(B(face), new[] { new PointF(cx - 14, p.Y + 58), new PointF(cx + 14, p.Y + 58), new PointF(cx, p.Y + 74) });
                using (var pen = P(C(215, 215, 225), 1.5f))
                    for (int i = 0; i < 6; i++) g.DrawArc(pen, cx - 18 + i * 6, p.Y + 70, 6, 5, 0, 180);
                for (int i = 0; i < 9; i++) g.FillEllipse(Brushes.White, cx - 10 + i * 2.4f, p.Y + 62 + (float)Math.Sin(i / 8.0 * Math.PI) * 6, 2.5f, 2.5f);
                g.FillEllipse(B(C(230, 180, 190)), cx + 14, p.Y + 76, 8, 8);
            }
            else if (kind == PaintCelina)
            {
                g.FillPolygon(Brushes.White, new[] { new PointF(cx - 12, p.Y + 58), new PointF(cx + 12, p.Y + 58), new PointF(cx, p.Y + 70) });
                g.FillEllipse(B(C(220, 190, 90)), cx - 3, p.Y + 68, 6, 6);
            }
            else
            {
                // gentlemen: white high collar and a cravat
                g.FillPolygon(Brushes.White, new[] { new PointF(cx - 10, p.Y + 56), new PointF(cx + 10, p.Y + 56), new PointF(cx + 6, p.Y + 72), new PointF(cx - 6, p.Y + 72) });
                g.FillPolygon(B(C(30, 30, 35)), new[] { new PointF(cx - 6, p.Y + 62), new PointF(cx + 6, p.Y + 62), new PointF(cx, p.Y + 70) });
                using (var pen = P(C(70, 70, 80), 1)) g.DrawLine(pen, cx, p.Y + 72, cx, p.Bottom);
                g.FillEllipse(B(C(200, 170, 80)), cx - 2, p.Y + 78, 4, 4);
                g.FillEllipse(B(C(200, 170, 80)), cx - 2, p.Y + 86, 4, 4);
            }
            // neck and face
            g.FillRectangle(B(face), cx - 6, p.Y + 46, 12, 14);
            g.FillEllipse(B(face), cx - 14, p.Y + 22, 28, 34);
            g.FillEllipse(Brushes.White, cx - 9, p.Y + 34, 7, 4);
            g.FillEllipse(Brushes.White, cx + 2, p.Y + 34, 7, 4);
            g.FillEllipse(B(C(70, 50, 40)), cx - 7, p.Y + 34, 3, 4);
            g.FillEllipse(B(C(70, 50, 40)), cx + 4, p.Y + 34, 3, 4);
            g.FillEllipse(B(CA(90, 230, 120, 120)), cx - 12, p.Y + 41, 6, 4);
            g.FillEllipse(B(CA(90, 230, 120, 120)), cx + 6, p.Y + 41, 6, 4);
            using (var pen = P(C(170, 90, 90), 1.5f)) g.DrawArc(pen, cx - 4, p.Y + 44, 8, 4, 20, 140);
            // hair
            switch (kind)
            {
                case PaintWhiteLady:
                    // tall powdered hairstyle of the 1700s
                    g.FillEllipse(B(C(235, 232, 228)), cx - 16, p.Y + 6, 32, 28);
                    g.FillEllipse(B(C(225, 222, 218)), cx - 18, p.Y + 18, 10, 14);
                    g.FillEllipse(B(C(225, 222, 218)), cx + 8, p.Y + 18, 10, 14);
                    g.FillEllipse(B(C(240, 200, 210)), cx + 4, p.Y + 6, 8, 6);
                    break;
                case PaintCelina:
                    g.FillPie(B(C(40, 28, 22)), cx - 15, p.Y + 19, 30, 26, 180, 180);
                    g.FillEllipse(B(C(40, 28, 22)), cx - 18, p.Y + 30, 8, 12);
                    g.FillEllipse(B(C(40, 28, 22)), cx + 10, p.Y + 30, 8, 12);
                    using (var pen = P(C(245, 215, 190), 1)) g.DrawLine(pen, cx, p.Y + 20, cx, p.Y + 28);
                    break;
                case PaintTytus:
                    g.FillPie(B(C(60, 40, 28)), cx - 15, p.Y + 18, 30, 24, 180, 180);
                    g.FillRectangle(B(C(60, 40, 28)), cx - 15, p.Y + 30, 4, 16);
                    g.FillRectangle(B(C(60, 40, 28)), cx + 11, p.Y + 30, 4, 16);
                    break;
                default:
                    g.FillPie(B(C(45, 32, 25)), cx - 15, p.Y + 18, 30, 22, 180, 180);
                    using (var beard = new GraphicsPath())
                    {
                        beard.AddBezier(cx - 14, p.Y + 38, cx - 12, p.Y + 58, cx + 12, p.Y + 58, cx + 14, p.Y + 38);
                        beard.AddLine(cx + 14, p.Y + 38, cx + 6, p.Y + 46);
                        beard.AddLine(cx + 6, p.Y + 46, cx - 6, p.Y + 46);
                        beard.CloseFigure();
                        g.FillPath(B(C(55, 40, 30)), beard);
                    }
                    break;
            }
            // varnish highlight
            g.FillEllipse(B(CA(25, 255, 255, 230)), p.X - 10, p.Y - 10, p.Width * 0.7f, p.Height * 0.5f);
        }

        public static Tex MasterDoor()
        {
            return Opaque(TW, TH, g =>
            {
                RoyalBase(g, TW);
                using (var door = new GraphicsPath())
                {
                    door.AddArc(18, 12, 92, 70, 180, 180);
                    door.AddLine(110, 47, 110, 184);
                    door.AddLine(110, 184, 18, 184);
                    door.CloseFigure();
                    using (var br = new LinearGradientBrush(new RectangleF(18, 12, 92, 172), C(150, 85, 40), C(95, 50, 22), 0f)) g.FillPath(br, door);
                    using (var pen = P(C(240, 200, 90), 4)) g.DrawPath(pen, door);
                }
                using (var pen = P(C(80, 40, 15), 2)) g.DrawLine(pen, 64, 20, 64, 184);
                // crown
                g.FillPolygon(B(C(255, 210, 60)), new[] { new PointF(44, 66), new PointF(48, 40), new PointF(56, 54), new PointF(64, 34), new PointF(72, 54), new PointF(80, 40), new PointF(84, 66) });
                // golden padlock
                using (var pen = P(C(200, 160, 50), 5)) g.DrawArc(pen, 52, 94, 24, 28, 180, 180);
                FillRoundGrad(g, new RectangleF(48, 106, 32, 28), 4, C(255, 220, 90), C(200, 150, 30), 90f);
                g.FillEllipse(B(C(80, 50, 10)), 61, 114, 6, 8);
            });
        }

        public static Tex StairsUp(bool open)
        {
            return Opaque(TW, TH, g =>
            {
                RoyalBase(g, TW);
                var hole = new RectangleF(14, 20, 100, 164);
                Grad(g, hole, open ? C(255, 245, 200) : C(70, 60, 70), open ? C(220, 190, 120) : C(40, 35, 45), 90f, false);
                for (int i = 0; i < 9; i++)
                {
                    float y = hole.Bottom - 12 - i * 14, inset = i * 4;
                    var step = new RectangleF(hole.X + inset, y, hole.Width - inset * 2, 12);
                    Grad(g, step, open ? C(250, 230, 180) : C(150, 140, 150), open ? C(200, 170, 110) : C(100, 95, 105), 90f, false);
                }
                g.FillRectangle(B(C(240, 200, 90)), hole.X - 4, hole.Y, 6, hole.Height);
                g.FillRectangle(B(C(240, 200, 90)), hole.Right - 2, hole.Y, 6, hole.Height);
                if (open)
                {
                    using (var b = B(C(60, 190, 80)))
                        g.FillPolygon(b, new[] { new PointF(64, 30), new PointF(90, 60), new PointF(74, 60), new PointF(74, 86), new PointF(54, 86), new PointF(54, 60), new PointF(38, 60) });
                    for (int i = 0; i < 6; i++)
                        g.FillPolygon(Brushes.White, StarPoints(26 + i * 15, 100 + (i % 2) * 20, 4, 1.5f));
                }
                else
                {
                    using (var pen = P(C(230, 190, 70), 5))
                        for (int x = 26; x < 112; x += 16) g.DrawLine(pen, x, hole.Y, x, hole.Bottom);
                    FillRoundGrad(g, new RectangleF(50, 90, 28, 24), 4, C(255, 220, 90), C(200, 150, 30), 90f);
                    g.FillEllipse(B(C(80, 50, 10)), 61, 98, 6, 8);
                }
            });
        }

        // ------------------------------------------------------------------ floors and ceilings (128x128)

        public static Tex Checker(FloorTheme th)
        {
            return Opaque(128, 128, g =>
            {
                CheckerBase(g, th);
                g.FillEllipse(B(CA(50, 255, 255, 255)), 20, 10, 50, 20);
            });
        }

        /// <summary>Clean, shiny checkered tiles in the floor's two colors with a little sparkle on every colored tile (also under the corridor runner).</summary>
        static void CheckerBase(Graphics g, FloorTheme th)
        {
            var r = new Random(7);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                {
                    bool light = (x + y) % 2 == 0;
                    var c = light ? th.Floor1 : th.Floor2;
                    var rect = new RectangleF(x * 32, y * 32, 32, 32);
                    Grad(g, rect, Shade(c, 12), Shade(c, -6), 45f, false);
                    for (int i = 0; i < 10; i++)
                        using (var b = B(CA(30, Shade(c, r.Next(-30, 30))))) g.FillEllipse(b, rect.X + F(r) * 28, rect.Y + F(r) * 28, 4, 3);
                    // glossy corner highlight
                    using (var b = B(CA(55, 255, 255, 255))) g.FillEllipse(b, rect.X + 3, rect.Y + 3, 14, 6);
                    if (!light)
                        using (var b = B(CA(150, 255, 255, 255)))
                            g.FillPolygon(b, new[] { new PointF(rect.X + 16, rect.Y + 10), new PointF(rect.X + 18, rect.Y + 14), new PointF(rect.X + 22, rect.Y + 16),
                                new PointF(rect.X + 18, rect.Y + 18), new PointF(rect.X + 16, rect.Y + 22), new PointF(rect.X + 14, rect.Y + 18),
                                new PointF(rect.X + 10, rect.Y + 16), new PointF(rect.X + 14, rect.Y + 14) });
                }
            using (var pen = P(CA(60, 0, 0, 0), 1))
                for (int i = 0; i <= 128; i += 32)
                {
                    g.DrawLine(pen, i, 0, i, 128);
                    g.DrawLine(pen, 0, i, 128, i);
                }
        }

        public static Tex Parquet()
        {
            return Opaque(128, 128, g =>
            {
                var r = new Random(8);
                for (int row = 0; row < 8; row++)
                    for (int col = -1; col < 4; col++)
                    {
                        float x = col * 40 + (row % 2) * 20, y = row * 16;
                        int d = r.Next(-18, 18);
                        var c = C(205 + d, 148 + d, 88 + d / 2);
                        var rect = new RectangleF(x, y, 40, 16);
                        Grad(g, rect, Shade(c, 12), Shade(c, -14), 90f, false);
                        using (var pen = P(CA(90, 120, 70, 30), 1))
                            g.DrawBezier(pen, x + 2, y + 5 + F(r) * 4, x + 14, y + 3, x + 26, y + 11, x + 38, y + 7);
                        using (var pen = P(CA(140, 90, 55, 25), 1)) g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                    }
            });
        }

        public static Tex Marble()
        {
            return Opaque(128, 128, g =>
            {
                var r = new Random(9);
                for (int y = 0; y < 2; y++)
                    for (int x = 0; x < 2; x++)
                    {
                        var c = (x + y) % 2 == 0 ? C(244, 234, 214) : C(238, 208, 184);
                        var rect = new RectangleF(x * 64, y * 64, 64, 64);
                        Grad(g, rect, Shade(c, 10), Shade(c, -10), 30f, false);
                        using (var pen = P(CA(55, 170, 140, 120), 1))
                            for (int v = 0; v < 3; v++)
                                g.DrawBezier(pen, rect.X + F(r) * 64, rect.Y, rect.X + F(r) * 64, rect.Y + 20, rect.X + F(r) * 64, rect.Y + 44, rect.X + F(r) * 64, rect.Bottom);
                    }
                using (var pen = P(CA(100, 150, 120, 100), 1.5f))
                {
                    g.DrawLine(pen, 64, 0, 64, 128);
                    g.DrawLine(pen, 0, 64, 128, 64);
                    g.DrawRectangle(pen, 0, 0, 128, 128);
                }
                // small colored inlays where the slabs meet (the corner one wraps around, so the floor tiles)
                var inlay = new[] { C(60, 150, 235), C(255, 190, 50) };
                foreach (var p in new[] { new PointF(64, 64), new PointF(0, 0), new PointF(128, 0), new PointF(0, 128), new PointF(128, 128) })
                {
                    var col = p.X == 64 ? inlay[0] : inlay[1];
                    var d = new[] { new PointF(p.X, p.Y - 7), new PointF(p.X + 7, p.Y), new PointF(p.X, p.Y + 7), new PointF(p.X - 7, p.Y) };
                    g.FillPolygon(B(col), d);
                    using (var pen = P(Color.White, 1.2f)) g.DrawPolygon(pen, d);
                }
            });
        }

        public static Tex Carpet()
        {
            return Opaque(128, 128, g =>
            {
                Grad(g, new RectangleF(0, 0, 128, 128), C(195, 45, 55), C(160, 32, 45), 45f, false);
                using (var pen = P(C(235, 190, 80), 3))
                {
                    g.DrawPolygon(pen, new[] { new PointF(64, 18), new PointF(110, 64), new PointF(64, 110), new PointF(18, 64) });
                    g.DrawRectangle(pen, 3, 3, 122, 122);
                }
                g.FillPolygon(B(C(235, 190, 80)), StarPoints(64, 64, 14, 6));
                using (var b = B(C(120, 20, 30)))
                    foreach (var p in new[] { new PointF(8, 8), new PointF(120, 8), new PointF(8, 120), new PointF(120, 120) })
                        g.FillEllipse(b, p.X - 5, p.Y - 5, 10, 10);
            });
        }

        public static Tex CeilingTile(bool lamp)
        {
            return Opaque(128, 128, g =>
            {
                g.FillRectangle(B(C(248, 244, 232)), 0, 0, 128, 128);
                var r = new Random(10);
                for (int i = 0; i < 260; i++) g.FillRectangle(B(C(228, 222, 208)), F(r) * 128, F(r) * 128, 1.5f, 1.5f);
                using (var pen = P(C(210, 202, 185), 2))
                {
                    g.DrawRectangle(pen, 0, 0, 128, 128);
                    g.DrawLine(pen, 64, 0, 64, 128);
                    g.DrawLine(pen, 0, 64, 128, 64);
                }
                if (lamp)
                {
                    var l = new RectangleF(20, 36, 88, 56);
                    g.FillRectangle(B(C(200, 200, 205)), l.X - 4, l.Y - 4, l.Width + 8, l.Height + 8);
                    using (var p = new GraphicsPath())
                    {
                        p.AddRectangle(l);
                        using (var br = new PathGradientBrush(p) { CenterColor = C(255, 255, 245), SurroundColors = new[] { C(255, 245, 200) } })
                            g.FillPath(br, p);
                    }
                    using (var pen = P(C(230, 225, 200), 1.5f))
                        for (int x = 1; x < 4; x++) g.DrawLine(pen, l.X + x * 22, l.Y, l.X + x * 22, l.Bottom);
                }
            });
        }

        // ------------------------------------------------------------------ characters

        static readonly Color Skin = C(125, 190, 95), SkinDark = C(75, 140, 60), ShellA = C(140, 185, 70), ShellB = C(55, 110, 40);

        /// <summary>The player turtle seen from behind: shell with plates, yellow school backpack and red cap.</summary>
        static Bitmap PlayerBmp(int frame) => Canvas(160, 152, g =>
        {
            float a = frame == 1 ? 7 : frame == 2 ? -7 : 0;
            bool jump = frame == 3;
            using (var skin = new LinearGradientBrush(new Rectangle(0, 0, 160, 152), Skin, SkinDark, 90f))
            using (var outline = P(C(50, 90, 40), 2.5f))
            {
                // legs
                var legs = jump
                    ? new[] { new RectangleF(2, 42, 34, 26), new RectangleF(124, 42, 34, 26), new RectangleF(10, 112, 34, 26), new RectangleF(116, 112, 34, 26) }
                    : new[] { new RectangleF(10, 50 - a, 32, 28), new RectangleF(118, 50 + a, 32, 28), new RectangleF(20, 110 + a, 32, 30), new RectangleF(108, 110 - a, 32, 30) };
                foreach (var l in legs)
                {
                    g.FillEllipse(skin, l);
                    g.DrawEllipse(outline, l);
                }
                g.FillPolygon(skin, new[] { new PointF(72, 136), new PointF(88, 136), new PointF(80, 151) });
                // head with a red cap
                g.FillEllipse(skin, 52, 4, 56, 50);
                g.DrawEllipse(outline, 52, 4, 56, 50);
                using (var cap = new GraphicsPath())
                {
                    cap.AddArc(50, 0, 60, 46, 180, 180);
                    cap.CloseFigure();
                    using (var br = new LinearGradientBrush(new RectangleF(50, 0, 60, 24), C(240, 70, 60), C(180, 30, 30), 90f)) g.FillPath(br, cap);
                }
                g.FillEllipse(Brushes.White, 76, -1, 8, 6);
                g.FillRectangle(B(C(250, 250, 250)), 72, 18, 16, 5);
                // shell
                var shell = new RectangleF(16, 30, 128, 114);
                using (var br = new LinearGradientBrush(shell, ShellA, ShellB, 70f)) g.FillEllipse(br, shell);
                using (var pen = P(C(235, 210, 120), 4)) g.DrawEllipse(pen, shell.X + 4, shell.Y + 4, shell.Width - 8, shell.Height - 8);
                using (var pen = P(C(40, 80, 30), 3)) g.DrawEllipse(pen, shell);
                using (var pen = P(C(70, 120, 45), 2.5f))
                {
                    g.DrawPolygon(pen, HexPoints(80, 118, 16));
                    g.DrawLine(pen, 36, 110, 62, 116);
                    g.DrawLine(pen, 124, 110, 98, 116);
                }
                // backpack
                using (var pen = P(C(200, 130, 20), 7))
                {
                    g.DrawLine(pen, 56, 58, 38, 40);
                    g.DrawLine(pen, 104, 58, 122, 40);
                }
                var pack = new RectangleF(48, 52, 64, 58);
                FillRoundGrad(g, pack, 14, C(255, 215, 60), C(235, 160, 20), 90f);
                StrokeRound(g, pack, 14, C(170, 110, 10), 2.5f);
                var pocket = new RectangleF(58, 76, 44, 28);
                FillRoundGrad(g, pocket, 8, C(255, 190, 40), C(225, 140, 15), 90f);
                StrokeRound(g, pocket, 8, C(170, 110, 10), 2);
                g.FillEllipse(B(C(230, 60, 60)), 75, 80, 10, 10);
                g.FillEllipse(B(CA(110, 255, 255, 255)), 54, 56, 22, 10);
                g.FillEllipse(B(CA(90, 255, 255, 255)), 30, 44, 36, 18);
            }
        });

        static Color SubjectCoat(int i) => School.Subjects[i].Color;

        /// <summary>An old, friendly turtle teacher standing up (front view). Even indexes are ladies, odd are gentlemen.</summary>
        static Bitmap TeacherBmp(int v, bool talk) => Canvas(170, 210, g =>
        {
            bool lady = v % 2 == 0;
            var coat = SubjectCoat(v);
            var skin = C(130, 180, 105);
            var skinD = C(85, 135, 70);
            using (var outline = P(C(50, 80, 40), 2.5f))
            {
                // shell behind the body
                g.FillEllipse(B(C(110, 120, 60)), 22, 76, 126, 116);
                g.DrawEllipse(outline, 22, 76, 126, 116);
                // feet
                g.FillEllipse(B(skinD), 46, 188, 34, 20);
                g.FillEllipse(B(skinD), 90, 188, 34, 20);
                // belly plate
                g.FillEllipse(B(C(240, 225, 170)), 58, 96, 54, 96);
                using (var pen = P(C(200, 180, 120), 1.5f))
                    for (int i = 0; i < 4; i++) g.DrawLine(pen, 64, 120 + i * 18, 106, 120 + i * 18);
                // cardigan
                FillRoundGrad(g, new RectangleF(36, 92, 30, 100), 12, Shade(coat, 25), Shade(coat, -30), 0f);
                FillRoundGrad(g, new RectangleF(104, 92, 30, 100), 12, Shade(coat, 25), Shade(coat, -30), 0f);
                for (int i = 0; i < 3; i++) g.FillEllipse(B(C(250, 240, 210)), 58, 112 + i * 22, 6, 6);
                // arms: one holds a book
                g.FillEllipse(B(skin), 18, 104, 24, 60);
                g.FillEllipse(B(skin), 128, 104, 24, 54);
                FillRoundGrad(g, new RectangleF(118, 140, 40, 30), 3, Shade(coat, -10), Shade(coat, -50), 0f);
                g.FillRectangle(B(C(250, 250, 240)), 122, 143, 32, 3);
                g.FillEllipse(B(skin), 130, 150, 20, 16);
                // neck and head
                g.FillRectangle(B(skin), 68, 80, 34, 20);
                var head = new RectangleF(34, 8, 102, 84);
                using (var br = new LinearGradientBrush(head, Shade(skin, 15), skinD, 90f)) g.FillEllipse(br, head);
                g.DrawEllipse(outline, head);
                using (var pen = P(CA(140, 70, 110, 55), 1.8f))
                    for (int i = 0; i < 3; i++) g.DrawArc(pen, 62, 14 + i * 5, 46, 14, 200, 140);
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
                    g.FillEllipse(B(C(205, 205, 210)), 68, -2, 34, 26);
                    g.FillEllipse(B(C(225, 225, 230)), 74, 2, 14, 10);
                    g.FillEllipse(B(coat), 64, 16, 10, 8);
                    for (int i = 0; i < 7; i++) g.FillEllipse(Brushes.White, 64 + i * 6, 94 + (float)Math.Sin(i / 6.0 * Math.PI) * 5, 6, 6);
                }
                else
                {
                    g.FillEllipse(B(Color.White), 70, 60, 16, 8);
                    g.FillEllipse(B(Color.White), 84, 60, 16, 8);
                    if (talk) g.FillRectangle(B(skin), 80, 66, 10, 2);
                    g.FillPolygon(B(C(200, 40, 50)), new[] { new PointF(85, 98), new PointF(70, 90), new PointF(70, 106) });
                    g.FillPolygon(B(C(200, 40, 50)), new[] { new PointF(85, 98), new PointF(100, 90), new PointF(100, 106) });
                    g.FillEllipse(B(C(160, 20, 30)), 81, 94, 8, 8);
                }
            }
        });

        /// <summary>The wise Master Turtle: purple robe with stars, long white beard, graduation cap and a star staff.</summary>
        static Bitmap MasterBmp(bool talk) => Canvas(220, 280, g =>
        {
            var skin = C(140, 175, 110);
            var skinD = C(90, 125, 70);
            using (var outline = P(C(40, 30, 50), 3))
            {
                // staff
                using (var pen = P(C(120, 75, 35), 8)) g.DrawLine(pen, 186, 60, 186, 276);
                g.FillPolygon(B(C(255, 215, 60)), StarPoints(186, 50, 24, 10));
                g.DrawPolygon(P(C(200, 140, 0), 2), StarPoints(186, 50, 24, 10));
                // shell and robe
                g.FillEllipse(B(C(100, 110, 60)), 26, 96, 150, 150);
                using (var robe = new GraphicsPath())
                {
                    robe.AddLine(64, 112, 136, 112);
                    robe.AddLine(136, 112, 168, 272);
                    robe.AddLine(168, 272, 32, 272);
                    robe.CloseFigure();
                    using (var br = new LinearGradientBrush(new RectangleF(32, 112, 136, 160), C(140, 70, 200), C(70, 30, 120), 0f)) g.FillPath(br, robe);
                    g.DrawPath(outline, robe);
                }
                var r = new Random(3);
                for (int i = 0; i < 9; i++)
                    g.FillPolygon(B(C(255, 220, 90)), StarPoints(52 + F(r) * 96, 170 + F(r) * 90, 7, 3));
                g.FillRectangle(B(C(240, 200, 80)), 46, 262, 108, 8);
                // arms
                g.FillEllipse(B(C(120, 60, 180)), 150, 120, 36, 80);
                g.FillEllipse(B(skin), 168, 176, 26, 24);
                g.FillEllipse(B(C(120, 60, 180)), 16, 124, 36, 80);
                g.FillEllipse(B(skin), 14, 190, 26, 24);
                // head
                g.FillRectangle(B(skin), 82, 100, 44, 20);
                var head = new RectangleF(44, 26, 120, 96);
                using (var br = new LinearGradientBrush(head, Shade(skin, 15), skinD, 90f)) g.FillEllipse(br, head);
                g.DrawEllipse(outline, head);
                // beard
                using (var beard = new GraphicsPath())
                {
                    beard.AddBezier(62, 86, 70, 150, 90, 190, 104, 214);
                    beard.AddBezier(104, 214, 118, 190, 138, 150, 146, 86);
                    beard.AddBezier(146, 86, 130, 110, 78, 110, 62, 86);
                    using (var br = new LinearGradientBrush(new RectangleF(60, 86, 90, 130), Color.White, C(210, 210, 220), 90f)) g.FillPath(br, beard);
                    using (var pen = P(C(180, 180, 190), 2)) g.DrawPath(pen, beard);
                }
                using (var pen = P(C(200, 200, 210), 1.5f))
                    for (int i = 0; i < 4; i++) g.DrawBezier(pen, 84 + i * 8, 110, 88 + i * 8, 140, 92 + i * 6, 170, 100 + i * 2, 196);
                // moustache and mouth
                if (talk) g.FillEllipse(B(C(90, 30, 40)), 92, 92, 24, 16);
                g.FillEllipse(Brushes.White, 74, 84, 32, 14);
                g.FillEllipse(Brushes.White, 102, 84, 32, 14);
                // eyes, glasses, eyebrows
                Eye(g, 82, 64, 10);
                Eye(g, 126, 64, 10);
                using (var pen = P(C(200, 160, 50), 3.5f))
                {
                    g.DrawEllipse(pen, 66, 48, 32, 32);
                    g.DrawEllipse(pen, 110, 48, 32, 32);
                    g.DrawLine(pen, 98, 62, 110, 62);
                }
                using (var pen = P(Color.White, 6))
                {
                    g.DrawArc(pen, 62, 36, 38, 16, 200, 120);
                    g.DrawArc(pen, 108, 36, 38, 16, 220, 120);
                }
                // graduation cap
                g.FillPolygon(B(C(35, 30, 45)), new[] { new PointF(104, 0), new PointF(176, 22), new PointF(104, 42), new PointF(32, 22) });
                g.FillRectangle(B(C(45, 40, 55)), 66, 26, 76, 16);
                using (var pen = P(C(255, 210, 60), 3)) g.DrawLines(pen, new[] { new PointF(104, 21), new PointF(160, 26), new PointF(162, 56) });
                g.FillEllipse(B(C(255, 210, 60)), 156, 54, 12, 16);
                // medallion
                g.FillEllipse(B(C(255, 210, 60)), 92, 200, 24, 24);
                g.FillPolygon(B(C(200, 140, 20)), StarPoints(104, 212, 8, 3.5f));
            }
        });

        static Bitmap KidBmp(int v, int frame) => Canvas(100, 116, g =>
        {
            var shirt = new[] { C(240, 90, 90), C(80, 150, 240), C(250, 180, 50) }[v];
            float a = frame == 0 ? 4 : -4;
            g.FillEllipse(B(ShellB), 14, 46, 72, 60);
            g.FillEllipse(B(SkinDark), 26, 96 + a, 22, 18);
            g.FillEllipse(B(SkinDark), 52, 96 - a, 22, 18);
            FillRoundGrad(g, new RectangleF(26, 52, 48, 50), 18, Shade(shirt, 25), Shade(shirt, -25), 90f);
            g.FillEllipse(B(Skin), 12, 58 - a, 18, 30);
            g.FillEllipse(B(Skin), 70, 58 + a, 18, 30);
            using (var pen = P(C(120, 70, 20), 4))
            {
                g.DrawLine(pen, 34, 54, 38, 90);
                g.DrawLine(pen, 66, 54, 62, 90);
            }
            var head = new RectangleF(20, 2, 60, 54);
            using (var br = new LinearGradientBrush(head, Shade(Skin, 15), SkinDark, 90f)) g.FillEllipse(br, head);
            using (var pen = P(C(50, 90, 40), 2)) g.DrawEllipse(pen, head);
            Eye(g, 40, 26, 8);
            Eye(g, 60, 26, 8);
            using (var pen = P(C(50, 40, 30), 2.5f)) g.DrawArc(pen, 40, 32, 20, 12, 20, 140);
            g.FillEllipse(B(CA(110, 255, 130, 140)), 26, 36, 10, 6);
            g.FillEllipse(B(CA(110, 255, 130, 140)), 64, 36, 10, 6);
        });

        /// <summary>A pupil's desk and chair seen from behind, optionally with a classmate sitting on it.</summary>
        static Bitmap StudentDeskBmp(int v) => Canvas(128, 124, g =>
        {
            // desk
            using (var pen = P(C(120, 120, 130), 4))
            {
                g.DrawLine(pen, 22, 50, 22, 118);
                g.DrawLine(pen, 106, 50, 106, 118);
            }
            g.FillPolygon(B(C(215, 175, 120)), new[] { new PointF(10, 54), new PointF(118, 54), new PointF(106, 38), new PointF(22, 38) });
            g.FillRectangle(B(C(170, 125, 80)), 10, 54, 108, 8);
            g.FillRectangle(B(C(150, 105, 65)), 24, 62, 80, 24);
            if (v == 0)
            {
                g.FillPolygon(B(C(70, 120, 220)), new[] { new PointF(40, 50), new PointF(64, 50), new PointF(60, 42), new PointF(44, 42) });
                g.FillRectangle(B(C(255, 210, 60)), 72, 44, 22, 3);
            }
            // classmate
            switch (v)
            {
                case 1:
                    g.FillEllipse(B(Skin), 52, 8, 26, 24);
                    g.FillEllipse(B(ShellB), 38, 24, 52, 48);
                    using (var pen = P(C(235, 210, 120), 2.5f)) g.DrawEllipse(pen, 42, 28, 44, 40);
                    using (var pen = P(C(70, 120, 45), 2)) g.DrawPolygon(pen, HexPoints(64, 48, 9));
                    break;
                case 2:
                    g.FillEllipse(B(C(225, 220, 220)), 46, -2, 12, 30);
                    g.FillEllipse(B(C(225, 220, 220)), 70, -2, 12, 30);
                    g.FillEllipse(B(C(255, 190, 200)), 49, 2, 6, 20);
                    g.FillEllipse(B(C(255, 190, 200)), 73, 2, 6, 20);
                    g.FillEllipse(B(C(230, 225, 225)), 46, 18, 36, 30);
                    g.FillEllipse(B(C(80, 160, 220)), 40, 40, 48, 36);
                    g.FillEllipse(Brushes.White, 60, 62, 10, 10);
                    break;
                case 3:
                    g.FillEllipse(B(C(90, 180, 80)), 44, 16, 40, 30);
                    g.FillEllipse(B(C(90, 180, 80)), 44, 10, 14, 14);
                    g.FillEllipse(B(C(90, 180, 80)), 70, 10, 14, 14);
                    g.FillEllipse(B(C(240, 120, 60)), 40, 40, 48, 36);
                    break;
            }
            // chair (in front of the pupil)
            using (var pen = P(C(110, 110, 120), 4))
            {
                g.DrawLine(pen, 40, 76, 38, 122);
                g.DrawLine(pen, 88, 76, 90, 122);
            }
            FillRoundGrad(g, new RectangleF(34, 66, 60, 26), 6, C(200, 140, 80), C(150, 95, 50), 90f);
            g.FillRectangle(B(C(130, 85, 45)), 34, 98, 60, 6);
        });

        static Bitmap TeacherDeskBmp() => Canvas(200, 112, g =>
        {
            Grad(g, new RectangleF(12, 40, 176, 70), C(160, 105, 60), C(110, 70, 38), 90f, false);
            Grad(g, new RectangleF(4, 30, 192, 12), C(205, 150, 95), C(150, 100, 60), 90f, false);
            using (var pen = P(C(90, 55, 28), 2))
            {
                g.DrawRectangle(pen, 22, 50, 70, 24);
                g.DrawRectangle(pen, 108, 50, 70, 24);
                g.DrawRectangle(pen, 22, 80, 70, 24);
                g.DrawRectangle(pen, 108, 80, 70, 24);
            }
            foreach (var p in new[] { new PointF(57, 62), new PointF(143, 62), new PointF(57, 92), new PointF(143, 92) })
                g.FillEllipse(B(C(230, 200, 100)), p.X - 4, p.Y - 3, 8, 6);
            // apple, books, pencil cup
            g.FillEllipse(B(C(220, 40, 40)), 150, 12, 20, 20);
            g.FillEllipse(B(CA(150, 255, 255, 255)), 154, 16, 6, 6);
            g.DrawLine(P(C(100, 60, 20), 2.5f), 160, 13, 162, 5);
            g.FillEllipse(B(C(70, 170, 50)), 162, 4, 10, 5);
            g.FillRectangle(B(C(60, 110, 200)), 20, 20, 50, 10);
            g.FillRectangle(B(C(220, 80, 60)), 24, 11, 44, 9);
            g.FillRectangle(B(C(60, 170, 90)), 22, 4, 46, 7);
            g.FillRectangle(B(C(90, 90, 100)), 104, 12, 16, 18);
            using (var pen = P(C(255, 200, 40), 3)) g.DrawLine(pen, 108, 12, 106, 0);
            using (var pen = P(C(230, 60, 60), 3)) g.DrawLine(pen, 114, 12, 118, 1);
        });

        static Bitmap GiftBoxBmp() => Canvas(80, 80, g =>
        {
            Grad(g, new RectangleF(12, 32, 56, 44), C(240, 70, 80), C(180, 30, 50), 90f, false);
            Grad(g, new RectangleF(7, 22, 66, 14), C(250, 100, 110), C(200, 40, 60), 90f, false);
            g.FillRectangle(B(C(255, 210, 60)), 34, 22, 12, 54);
            g.FillRectangle(B(C(255, 210, 60)), 12, 48, 56, 8);
            g.FillEllipse(B(C(255, 215, 70)), 20, 4, 22, 20);
            g.FillEllipse(B(C(255, 215, 70)), 38, 4, 22, 20);
            g.FillEllipse(B(C(220, 160, 30)), 34, 14, 12, 12);
            g.FillEllipse(B(CA(120, 255, 255, 255)), 16, 36, 14, 8);
        });

        static Bitmap MarkerBmp(bool done) => Canvas(64, 64, g =>
        {
            g.FillEllipse(B(CA(90, 0, 0, 0)), 6, 8, 54, 54);
            g.FillEllipse(B(done ? C(70, 190, 80) : C(255, 215, 50)), 4, 4, 54, 54);
            g.DrawEllipse(P(done ? C(30, 110, 40) : C(190, 130, 0), 3), 4, 4, 54, 54);
            if (done)
                using (var pen = P(Color.White, 7)) g.DrawLines(pen, new[] { new PointF(17, 32), new PointF(28, 43), new PointF(46, 20) });
            else
            {
                FillRound(g, new RectangleF(26, 12, 10, 26), 5, C(60, 40, 10));
                g.FillEllipse(B(C(60, 40, 10)), 25, 42, 12, 12);
            }
        });

        // ------------------------------------------------------------------ gifts (96x96 icons)

        static Bitmap GiftBmp(int kind) => Canvas(96, 96, g =>
        {
            switch (kind)
            {
                case 0: // teddy bear
                {
                    var fur = C(170, 115, 70);
                    foreach (var r in new[] { new RectangleF(18, 8, 20, 20), new RectangleF(58, 8, 20, 20) }) g.FillEllipse(B(fur), r);
                    g.FillEllipse(B(fur), 20, 46, 56, 46);
                    g.FillEllipse(B(fur), 8, 52, 20, 26);
                    g.FillEllipse(B(fur), 68, 52, 20, 26);
                    g.FillEllipse(B(fur), 24, 12, 48, 42);
                    g.FillEllipse(B(C(230, 190, 140)), 36, 30, 24, 18);
                    g.FillEllipse(B(C(230, 190, 140)), 34, 56, 28, 26);
                    g.FillEllipse(B(C(40, 25, 15)), 44, 32, 9, 7);
                    g.FillEllipse(Brushes.Black, 36, 22, 6, 7);
                    g.FillEllipse(Brushes.Black, 54, 22, 6, 7);
                    g.FillPolygon(B(C(240, 80, 120)), new[] { new PointF(48, 52), new PointF(34, 44), new PointF(34, 60) });
                    g.FillPolygon(B(C(240, 80, 120)), new[] { new PointF(48, 52), new PointF(62, 44), new PointF(62, 60) });
                    break;
                }
                case 1: // beach ball
                {
                    var cols = new[] { C(240, 60, 60), C(255, 220, 50), C(60, 130, 240), Color.White, C(60, 190, 90), C(255, 150, 40) };
                    for (int i = 0; i < 6; i++) g.FillPie(B(cols[i]), 8, 8, 80, 80, i * 60, 60);
                    g.FillEllipse(Brushes.White, 38, 38, 20, 20);
                    g.FillEllipse(B(CA(110, 255, 255, 255)), 22, 16, 26, 16);
                    g.DrawEllipse(P(C(80, 80, 90), 2), 8, 8, 80, 80);
                    break;
                }
                case 2: // book
                {
                    g.FillPolygon(Brushes.White, new[] { new PointF(14, 30), new PointF(74, 18), new PointF(84, 70), new PointF(24, 82) });
                    g.FillPolygon(B(C(50, 110, 210)), new[] { new PointF(10, 26), new PointF(70, 14), new PointF(80, 66), new PointF(20, 78) });
                    g.FillPolygon(B(C(30, 80, 170)), new[] { new PointF(10, 26), new PointF(18, 24), new PointF(28, 76), new PointF(20, 78) });
                    g.FillPolygon(B(C(255, 210, 60)), StarPoints(50, 46, 14, 6));
                    break;
                }
                case 3: // crayons
                {
                    var cols = new[] { C(230, 50, 50), C(250, 160, 30), C(250, 220, 40), C(60, 180, 70), C(60, 120, 230) };
                    for (int i = 0; i < 5; i++)
                    {
                        var st = g.Save();
                        g.TranslateTransform(48, 88);
                        g.RotateTransform(-40 + i * 20);
                        g.FillRectangle(B(cols[i]), -6, -66, 12, 54);
                        g.FillPolygon(B(Shade(cols[i], -30)), new[] { new PointF(-6, -66), new PointF(6, -66), new PointF(0, -80) });
                        g.FillRectangle(B(CA(140, 255, 255, 255)), -6, -48, 12, 5);
                        g.Restore(st);
                    }
                    break;
                }
                case 4: // toy car
                {
                    FillRoundGrad(g, new RectangleF(6, 44, 84, 28), 10, C(250, 80, 70), C(190, 30, 30), 90f);
                    using (var top = new GraphicsPath())
                    {
                        top.AddArc(22, 22, 52, 44, 180, 180);
                        top.CloseFigure();
                        g.FillPath(B(C(230, 60, 55)), top);
                    }
                    g.FillPie(B(C(170, 220, 250)), 28, 28, 40, 36, 180, 180);
                    g.FillRectangle(B(C(230, 60, 55)), 46, 26, 4, 20);
                    foreach (var x in new[] { 14f, 62f })
                    {
                        g.FillEllipse(B(C(40, 40, 45)), x, 60, 22, 22);
                        g.FillEllipse(B(C(200, 200, 205)), x + 6, 66, 10, 10);
                    }
                    g.FillEllipse(B(C(255, 240, 150)), 82, 50, 8, 8);
                    break;
                }
                case 5: // kite
                {
                    var pts = new[] { new PointF(48, 4), new PointF(80, 36), new PointF(48, 70), new PointF(16, 36) };
                    g.FillPolygon(B(C(240, 70, 70)), new[] { pts[0], pts[1], new PointF(48, 36) });
                    g.FillPolygon(B(C(60, 140, 240)), new[] { pts[1], pts[2], new PointF(48, 36) });
                    g.FillPolygon(B(C(255, 210, 50)), new[] { pts[2], pts[3], new PointF(48, 36) });
                    g.FillPolygon(B(C(70, 190, 90)), new[] { pts[3], pts[0], new PointF(48, 36) });
                    using (var pen = P(C(90, 70, 50), 2)) g.DrawBezier(pen, 48, 70, 30, 80, 60, 86, 40, 95);
                    g.FillEllipse(B(C(240, 70, 120)), 38, 78, 10, 7);
                    g.FillEllipse(B(C(60, 140, 240)), 46, 86, 10, 7);
                    break;
                }
                case 6: // balloon
                {
                    using (var pen = P(C(120, 120, 130), 2)) g.DrawBezier(pen, 48, 68, 40, 78, 58, 86, 48, 95);
                    Grad(g, new RectangleF(20, 4, 56, 64), C(255, 110, 140), C(210, 30, 80), 60f, true);
                    g.FillPolygon(B(C(210, 30, 80)), new[] { new PointF(44, 72), new PointF(52, 72), new PointF(48, 64) });
                    g.FillEllipse(B(CA(150, 255, 255, 255)), 30, 14, 14, 20);
                    break;
                }
                case 7: // cupcake
                {
                    g.FillPolygon(B(C(240, 130, 170)), new[] { new PointF(18, 52), new PointF(78, 52), new PointF(68, 92), new PointF(28, 92) });
                    using (var pen = P(C(255, 200, 220), 3))
                        for (int i = 0; i < 6; i++) g.DrawLine(pen, 24 + i * 10, 54, 30 + i * 8, 90);
                    g.FillEllipse(B(C(255, 240, 245)), 12, 34, 72, 28);
                    g.FillEllipse(B(C(255, 220, 235)), 20, 20, 56, 26);
                    g.FillEllipse(B(C(255, 240, 245)), 30, 10, 36, 20);
                    g.FillEllipse(B(C(220, 30, 50)), 40, 0, 16, 16);
                    var r = new Random(2);
                    var sprinkles = new[] { C(60, 140, 240), C(255, 210, 50), C(70, 190, 90) };
                    for (int i = 0; i < 10; i++) g.FillRectangle(B(sprinkles[i % 3]), 20 + F(r) * 56, 22 + F(r) * 30, 5, 2);
                    break;
                }
                case 8: // medal
                {
                    g.FillPolygon(B(C(220, 50, 60)), new[] { new PointF(24, 0), new PointF(44, 0), new PointF(56, 44), new PointF(40, 48) });
                    g.FillPolygon(B(C(60, 110, 220)), new[] { new PointF(72, 0), new PointF(52, 0), new PointF(40, 44), new PointF(56, 48) });
                    Grad(g, new RectangleF(18, 36, 60, 60), C(255, 230, 110), C(220, 150, 20), 45f, true);
                    g.DrawEllipse(P(C(180, 120, 10), 3), 24, 42, 48, 48);
                    g.FillPolygon(B(C(255, 245, 190)), StarPoints(48, 66, 16, 7));
                    break;
                }
                default: // robot
                {
                    g.DrawLine(P(C(120, 120, 130), 3), 48, 16, 48, 4);
                    g.FillEllipse(B(C(240, 60, 60)), 42, 0, 12, 12);
                    FillRoundGrad(g, new RectangleF(24, 14, 48, 34), 8, C(190, 200, 215), C(130, 140, 160), 90f);
                    g.FillEllipse(B(C(80, 220, 255)), 32, 24, 12, 12);
                    g.FillEllipse(B(C(80, 220, 255)), 52, 24, 12, 12);
                    g.FillRectangle(B(C(60, 60, 70)), 38, 40, 20, 4);
                    FillRoundGrad(g, new RectangleF(20, 50, 56, 38), 6, C(170, 180, 200), C(110, 120, 140), 90f);
                    g.FillRectangle(B(C(255, 200, 50)), 34, 60, 10, 10);
                    g.FillRectangle(B(C(60, 190, 90)), 52, 60, 10, 10);
                    g.FillRectangle(B(C(150, 160, 180)), 6, 54, 12, 28);
                    g.FillRectangle(B(C(150, 160, 180)), 78, 54, 12, 28);
                    break;
                }
            }
        });

        // ------------------------------------------------------------------ furniture and decorations

        static Bitmap PlantBmp() => Canvas(96, 140, g =>
        {
            var r = new Random(5);
            for (int i = 0; i < 14; i++)
            {
                var st = g.Save();
                g.TranslateTransform(48, 88);
                g.RotateTransform(-80 + i * 12 + (F(r) - 0.5f) * 10);
                float len = 50 + F(r) * 30;
                Grad(g, new RectangleF(-8, -len, 16, len), C(110, 200, 90), C(40, 120, 40), 0f, true);
                g.Restore(st);
            }
            g.FillPolygon(B(C(200, 100, 60)), new[] { new PointF(22, 92), new PointF(74, 92), new PointF(66, 138), new PointF(30, 138) });
            g.FillRectangle(B(C(220, 120, 75)), 18, 86, 60, 10);
        });

        static Bitmap GlobeBmp() => Canvas(96, 140, g =>
        {
            g.FillEllipse(B(C(110, 70, 40)), 22, 126, 52, 12);
            g.FillRectangle(B(C(110, 70, 40)), 44, 96, 8, 32);
            Grad(g, new RectangleF(14, 20, 68, 68), C(90, 170, 250), C(30, 90, 190), 45f, true);
            g.FillEllipse(B(C(90, 180, 80)), 26, 32, 22, 18);
            g.FillEllipse(B(C(90, 180, 80)), 50, 48, 22, 26);
            g.FillEllipse(B(C(90, 180, 80)), 30, 62, 14, 12);
            g.FillEllipse(B(CA(110, 255, 255, 255)), 24, 28, 20, 12);
            using (var pen = P(C(220, 180, 70), 4)) g.DrawArc(pen, 8, 14, 80, 80, 100, 250);
        });

        static Bitmap PianoBmp() => Canvas(160, 140, g =>
        {
            Grad(g, new RectangleF(8, 8, 144, 90), C(90, 50, 30), C(55, 30, 18), 0f, false);
            g.FillRectangle(B(C(70, 40, 22)), 2, 92, 156, 12);
            g.FillRectangle(Brushes.White, 10, 80, 140, 16);
            for (int i = 1; i < 14; i++) g.DrawLine(Pens.Gray, 10 + i * 10, 80, 10 + i * 10, 96);
            for (int i = 0; i < 13; i++) if (i % 7 != 2 && i % 7 != 6) g.FillRectangle(Brushes.Black, 17 + i * 10, 80, 6, 9);
            g.FillRectangle(B(C(70, 40, 22)), 14, 104, 10, 34);
            g.FillRectangle(B(C(70, 40, 22)), 136, 104, 10, 34);
            g.FillRectangle(Brushes.White, 56, 22, 48, 36);
            using (var pen = P(Color.Black, 1))
                for (int i = 0; i < 5; i++) g.DrawLine(pen, 60, 28 + i * 6, 100, 28 + i * 6);
            g.FillEllipse(Brushes.Black, 66, 36, 6, 5);
            g.FillEllipse(Brushes.Black, 84, 30, 6, 5);
        });

        static Bitmap EaselBmp() => Canvas(110, 160, g =>
        {
            using (var pen = P(C(150, 100, 55), 6))
            {
                g.DrawLine(pen, 55, 6, 18, 156);
                g.DrawLine(pen, 55, 6, 92, 156);
                g.DrawLine(pen, 55, 6, 55, 150);
            }
            g.FillRectangle(B(C(150, 100, 55)), 14, 104, 82, 8);
            g.FillRectangle(Brushes.White, 16, 20, 78, 84);
            Grad(g, new RectangleF(20, 24, 70, 50), C(130, 200, 250), C(220, 240, 255), 90f, false);
            g.FillRectangle(B(C(100, 190, 80)), 20, 74, 70, 26);
            g.FillEllipse(B(C(255, 210, 60)), 66, 30, 16, 16);
            g.FillRectangle(B(C(230, 120, 80)), 32, 56, 26, 22);
            g.FillPolygon(B(C(200, 60, 50)), new[] { new PointF(28, 58), new PointF(62, 58), new PointF(45, 42) });
        });

        static Bitmap AquariumBmp(int frame) => Canvas(160, 128, g =>
        {
            g.FillRectangle(B(C(90, 60, 40)), 10, 88, 140, 40);
            var tank = new RectangleF(14, 10, 132, 78);
            Grad(g, tank, C(120, 200, 245), C(40, 120, 200), 90f, false);
            g.FillRectangle(B(C(220, 200, 150)), tank.X, tank.Bottom - 10, tank.Width, 10);
            using (var pen = P(C(50, 160, 70), 4))
                foreach (var x in new[] { 30f, 40f, 120f, 128f })
                    g.DrawBezier(pen, x, tank.Bottom - 8, x - 6, 64, x + 6, 50, x, 36);
            float s = frame == 0 ? 0 : 6;
            foreach (var f in new[] { new PointF(60 + s, 34), new PointF(96 - s, 58), new PointF(70 - s, 66) })
            {
                g.FillEllipse(B(C(255, 140, 30)), f.X, f.Y, 22, 13);
                g.FillPolygon(B(C(255, 110, 20)), new[] { new PointF(f.X + 20, f.Y + 6), new PointF(f.X + 30, f.Y), new PointF(f.X + 30, f.Y + 13) });
                g.FillEllipse(Brushes.Black, f.X + 4, f.Y + 4, 3, 3);
            }
            using (var pen = P(CA(200, 255, 255, 255), 1.5f))
                for (int i = 0; i < 4; i++) g.DrawEllipse(pen, 108 + (i % 2) * 4, 50 - i * 10 - s, 6, 6);
            using (var pen = P(C(40, 40, 50), 4)) g.DrawRectangle(pen, tank.X, tank.Y, tank.Width, tank.Height);
            g.FillRectangle(B(CA(70, 255, 255, 255)), tank.X + 6, tank.Y + 4, 10, tank.Height - 8);
        });

        static Bitmap AbacusBmp() => Canvas(128, 128, g =>
        {
            using (var pen = P(C(140, 90, 50), 8))
            {
                g.DrawRectangle(pen, 12, 12, 104, 92);
                g.DrawLine(pen, 18, 104, 10, 126);
                g.DrawLine(pen, 110, 104, 118, 126);
            }
            var cols = new[] { C(230, 60, 60), C(250, 170, 40), C(250, 220, 50), C(60, 180, 80), C(60, 130, 230) };
            for (int row = 0; row < 5; row++)
            {
                float y = 26 + row * 17;
                g.DrawLine(P(C(170, 170, 180), 2), 16, y, 112, y);
                for (int b = 0; b < 5; b++) g.FillEllipse(B(cols[row]), 20 + b * 11 + (row % 2) * 30, y - 7, 12, 14);
            }
        });

        static Bitmap TelescopeBmp() => Canvas(128, 150, g =>
        {
            using (var pen = P(C(120, 90, 60), 5))
            {
                g.DrawLine(pen, 64, 78, 26, 148);
                g.DrawLine(pen, 64, 78, 102, 148);
                g.DrawLine(pen, 64, 78, 64, 148);
            }
            var st = g.Save();
            g.TranslateTransform(64, 70);
            g.RotateTransform(-30);
            Grad(g, new RectangleF(-56, -12, 112, 24), C(70, 90, 180), C(30, 40, 100), 90f, false);
            g.FillRectangle(B(C(240, 200, 80)), -40, -13, 8, 26);
            g.FillRectangle(B(C(240, 200, 80)), 24, -13, 8, 26);
            g.FillEllipse(B(C(170, 220, 255)), 50, -12, 10, 24);
            g.Restore(st);
        });

        static Bitmap BallsBmp() => Canvas(128, 112, g =>
        {
            g.FillEllipse(B(C(240, 130, 40)), 14, 20, 44, 44);
            using (var pen = P(C(120, 60, 20), 2))
            {
                g.DrawLine(pen, 36, 20, 36, 64);
                g.DrawArc(pen, 14, 28, 22, 30, 270, 180);
            }
            g.FillEllipse(Brushes.White, 60, 14, 46, 46);
            g.FillPolygon(Brushes.Black, HexPoints(83, 37, 8));
            g.FillEllipse(B(C(60, 140, 240)), 38, 34, 40, 40);
            g.FillRectangle(B(CA(230, 170, 120, 70)), 8, 56, 112, 54);
            using (var pen = P(C(120, 80, 40), 2))
            {
                for (int x = 8; x <= 120; x += 14) g.DrawLine(pen, x, 56, x, 110);
                for (int y = 56; y <= 110; y += 13) g.DrawLine(pen, 8, y, 120, y);
            }
        });

        static Bitmap ClockBmp() => Canvas(80, 170, g =>
        {
            Grad(g, new RectangleF(12, 10, 56, 158), C(150, 95, 50), C(95, 55, 28), 0f, false);
            g.FillPolygon(B(C(120, 75, 38)), new[] { new PointF(8, 14), new PointF(40, 0), new PointF(72, 14) });
            g.FillEllipse(Brushes.White, 16, 16, 48, 48);
            g.DrawEllipse(P(C(220, 180, 70), 3), 16, 16, 48, 48);
            using (var pen = P(Color.Black, 3))
            {
                g.DrawLine(pen, 40, 40, 40, 24);
                g.DrawLine(pen, 40, 40, 52, 44);
            }
            g.FillRectangle(B(C(60, 35, 18)), 24, 74, 32, 80);
            g.DrawLine(P(C(220, 180, 70), 2), 40, 76, 40, 130);
            g.FillEllipse(B(C(240, 200, 80)), 32, 126, 16, 16);
        });

        static Bitmap BlocksBmp(string letters) => Canvas(128, 104, g =>
        {
            var cols = new[] { C(240, 70, 70), C(60, 140, 240), C(250, 200, 50) };
            var pos = new[] { new PointF(10, 56), new PointF(62, 56), new PointF(36, 10) };
            for (int i = 0; i < 3; i++)
            {
                var p = pos[i];
                g.FillPolygon(B(Shade(cols[i], 40)), new[] { new PointF(p.X, p.Y), new PointF(p.X + 10, p.Y - 8), new PointF(p.X + 54, p.Y - 8), new PointF(p.X + 44, p.Y) });
                g.FillPolygon(B(Shade(cols[i], -40)), new[] { new PointF(p.X + 44, p.Y), new PointF(p.X + 54, p.Y - 8), new PointF(p.X + 54, p.Y + 36), new PointF(p.X + 44, p.Y + 44) });
                g.FillRectangle(B(cols[i]), p.X, p.Y, 44, 44);
                TextIn(g, letters[i].ToString(), new RectangleF(p.X, p.Y, 44, 44), 34, Color.White);
            }
        });

        static Bitmap BooksBmp() => Canvas(96, 96, g =>
        {
            var cols = new[] { C(200, 50, 50), C(50, 110, 200), C(60, 160, 70), C(230, 180, 40), C(140, 70, 170) };
            for (int i = 0; i < 5; i++)
            {
                float y = 80 - i * 15, x = 10 + (i % 2) * 6;
                FillRoundGrad(g, new RectangleF(x, y, 74 - (i % 3) * 6, 14), 3, Shade(cols[i], 25), Shade(cols[i], -25), 90f);
                g.FillRectangle(Brushes.White, x + 66 - (i % 3) * 6, y + 2, 5, 10);
            }
        });

        static Bitmap BenchBmp() => Canvas(160, 84, g =>
        {
            g.FillRectangle(B(C(90, 90, 100)), 14, 40, 8, 44);
            g.FillRectangle(B(C(90, 90, 100)), 138, 40, 8, 44);
            for (int i = 0; i < 2; i++) Grad(g, new RectangleF(4, 4 + i * 14, 152, 11), C(200, 140, 80), C(150, 95, 50), 90f, false);
            Grad(g, new RectangleF(0, 40, 160, 12), C(210, 150, 90), C(150, 95, 50), 90f, false);
        });

        static Bitmap FountainBmp() => Canvas(96, 140, g =>
        {
            Grad(g, new RectangleF(24, 50, 48, 88), C(225, 230, 235), C(160, 165, 175), 0f, false);
            FillRoundGrad(g, new RectangleF(10, 36, 76, 20), 8, C(240, 245, 250), C(180, 185, 195), 90f);
            using (var pen = P(C(90, 170, 240), 4)) g.DrawArc(pen, 40, 8, 34, 48, 190, 120);
            g.FillEllipse(B(C(120, 190, 250)), 30, 40, 36, 8);
            g.FillRectangle(B(C(120, 125, 135)), 56, 70, 10, 6);
        });

        static Bitmap ThroneBmp() => Canvas(160, 210, g =>
        {
            Grad(g, new RectangleF(20, 20, 120, 190), C(250, 210, 90), C(170, 120, 30), 0f, false);
            Grad(g, new RectangleF(34, 36, 92, 100), C(200, 40, 60), C(130, 20, 40), 90f, false);
            g.FillPolygon(B(C(255, 215, 70)), new[] { new PointF(40, 24), new PointF(48, 0), new PointF(64, 16), new PointF(80, -2), new PointF(96, 16), new PointF(112, 0), new PointF(120, 24) });
            Grad(g, new RectangleF(10, 120, 140, 34), C(200, 40, 60), C(130, 20, 40), 90f, false);
            g.FillRectangle(B(C(220, 170, 60)), 4, 110, 22, 60);
            g.FillRectangle(B(C(220, 170, 60)), 134, 110, 22, 60);
        });

        static Bitmap CoinBmp(float squash) => Canvas(64, 64, g =>
        {
            float w = 50 * squash, x = 32 - w / 2;
            g.FillEllipse(B(C(200, 130, 10)), x, 7, w, 50);
            Grad(g, new RectangleF(x + 3 * squash, 9, Math.Max(1, w - 6 * squash), 46), C(255, 240, 120), C(240, 170, 20), 45f, true);
            if (squash > 0.5f) g.FillPolygon(B(C(255, 250, 200)), StarPoints(32, 32, 13 * squash, 6 * squash));
        });

        static Bitmap SparkleBmp() => Canvas(32, 32, g =>
        {
            g.FillPolygon(B(C(255, 250, 170)), StarPoints(16, 16, 15, 5));
            g.FillEllipse(Brushes.White, 12, 12, 8, 8);
        });
    }
}
