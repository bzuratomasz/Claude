using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace LazyTurtle
{
    /// <summary>
    /// Kórnik themed graphics: posters about the town and the school's patron (Teofila z Działyńskich
    /// Szołdrska-Potulicka), a display case, the schoolyard door, the lobby floor medallion, a corridor
    /// runner and all outdoor (schoolyard) textures.
    /// </summary>
    sealed partial class Art
    {
        // ------------------------------------------------------------------ small Kórnik helpers

        /// <summary>Calls draw at the 9 positions (x,y)+(±size) so shapes crossing an edge wrap around (tileable textures).</summary>
        static void KWrap(int size, float x, float y, Action<float, float> draw)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    draw(x + dx * size, y + dy * size);
        }

        /// <summary>Kórnik Castle in cartoon style; origin is the bottom centre, s = 1 is about 74x56 px.</summary>
        static void KCastle(Graphics g, float cx, float baseY, float s, Color wall, Color roof, Color win)
        {
            var st = g.Save();
            g.TranslateTransform(cx, baseY);
            g.ScaleTransform(s, s);
            using (var w = B(wall))
            using (var w2 = B(Shade(wall, -18)))
            using (var rf = B(roof))
            using (var wn = B(win))
            {
                // main body with battlements
                g.FillRectangle(w, -26, -26, 52, 26);
                for (int i = 0; i < 9; i++) g.FillRectangle(w, -26 + i * 6, -30, 3.5f, 5);
                // left: wide square tower with battlements
                g.FillRectangle(w2, -38, -42, 15, 42);
                for (int i = 0; i < 3; i++) g.FillRectangle(w2, -38 + i * 5.5f, -46, 3.5f, 5);
                // right: slender tower with a pointed roof
                g.FillRectangle(w2, 22, -40, 12, 40);
                g.FillPolygon(rf, new[] { new PointF(20, -40), new PointF(36, -40), new PointF(28, -56) });
                // central gable with a little spire
                g.FillPolygon(rf, new[] { new PointF(-11, -26), new PointF(11, -26), new PointF(0, -40) });
                g.FillRectangle(rf, -0.8f, -47, 1.6f, 8);
                // arched windows
                foreach (var x in new[] { -21f, -13f, 9f, 17f })
                {
                    g.FillRectangle(wn, x, -18, 4, 6);
                    g.FillEllipse(wn, x, -20, 4, 4);
                }
                for (int i = 0; i < 2; i++)
                {
                    g.FillRectangle(wn, -32.5f, -34 + i * 14, 4, 5);
                    g.FillEllipse(wn, -32.5f, -36 + i * 14, 4, 4);
                }
                g.FillRectangle(wn, 26, -30, 4, 5);
                g.FillEllipse(wn, 26, -32, 4, 4);
                g.FillEllipse(wn, -3, -32, 6, 6);
                // gate
                g.FillRectangle(wn, -4, -9, 8, 9);
                g.FillEllipse(wn, -4, -13, 8, 8);
            }
            g.Restore(st);
        }

        /// <summary>A white swan floating on water; (x,y) is the waterline centre.</summary>
        static void KSwan(Graphics g, float x, float y, float s, bool faceLeft)
        {
            var st = g.Save();
            g.TranslateTransform(x, y);
            g.ScaleTransform(faceLeft ? -s : s, s);
            using (var body = new GraphicsPath())
            {
                body.AddBezier(-12, -3, -10, -11, 4, -11, 8, -5);
                body.AddBezier(8, -5, 10, -2, 8, 1, 4, 1);
                body.AddLine(4, 1, -8, 1);
                body.AddBezier(-8, 1, -12, 1, -15, -4, -16, -9);
                body.AddLine(-16, -9, -12, -3);
                body.CloseFigure();
                g.FillPath(Brushes.White, body);
                using (var pen = P(C(160, 170, 195), 0.8f)) g.DrawPath(pen, body);
            }
            using (var pen = P(C(190, 198, 215), 1f)) g.DrawArc(pen, -10, -9, 15, 8, 200, 140);
            using (var pen = P(C(160, 170, 195), 4f)) g.DrawBezier(pen, 6, -5, 11, -9, 4, -14, 7, -19);
            using (var pen = P(Color.White, 2.8f)) g.DrawBezier(pen, 6, -5, 11, -9, 4, -14, 7, -19);
            g.FillEllipse(Brushes.White, 4.5f, -22, 6, 5);
            using (var b = B(C(245, 140, 40)))
                g.FillPolygon(b, new[] { new PointF(10, -21), new PointF(14.5f, -19.3f), new PointF(10, -18.3f) });
            using (var b = B(C(30, 30, 30)))
            {
                g.FillPolygon(b, new[] { new PointF(9.4f, -21.4f), new PointF(11, -19.8f), new PointF(9.4f, -18.4f) });
                g.FillEllipse(b, 7.2f, -21, 1.7f, 1.7f);
            }
            g.Restore(st);
        }

        /// <summary>One of six tree kinds (oak, pine, birch, autumn maple, spruce, golden tree); (x,by) is the trunk base.</summary>
        static void KTree(Graphics g, float x, float by, float s, int kind)
        {
            var trunk = C(120, 80, 45);
            switch (kind % 6)
            {
                case 0: // oak: broad dark crown
                    g.FillRectangle(B(trunk), x - 2.5f * s, by - 16 * s, 5 * s, 16 * s);
                    g.FillEllipse(B(C(45, 120, 50)), x - 14 * s, by - 26 * s, 14 * s, 13 * s);
                    g.FillEllipse(B(C(45, 120, 50)), x, by - 27 * s, 14 * s, 13 * s);
                    g.FillEllipse(B(C(55, 140, 55)), x - 11 * s, by - 34 * s, 22 * s, 18 * s);
                    g.FillEllipse(B(CA(120, 120, 200, 100)), x - 6 * s, by - 32 * s, 8 * s, 6 * s);
                    break;
                case 1: // pine: stacked triangles
                    g.FillRectangle(B(trunk), x - 2 * s, by - 8 * s, 4 * s, 8 * s);
                    for (int i = 0; i < 3; i++)
                    {
                        float w = (18 - i * 4) * s, top = by - (20 + i * 8) * s, bot = by - (6 + i * 8) * s;
                        g.FillPolygon(B(Shade(C(30, 105, 65), i * 10)), new[] { new PointF(x - w / 2, bot), new PointF(x + w / 2, bot), new PointF(x, top) });
                    }
                    break;
                case 2: // birch: white trunk with black marks
                    g.FillEllipse(B(C(140, 205, 90)), x - 8 * s, by - 36 * s, 16 * s, 24 * s);
                    g.FillEllipse(B(CA(150, 175, 230, 120)), x - 5 * s, by - 34 * s, 7 * s, 9 * s);
                    g.FillRectangle(Brushes.White, x - 1.8f * s, by - 18 * s, 3.6f * s, 18 * s);
                    for (int i = 0; i < 3; i++) g.FillRectangle(B(C(60, 60, 60)), x - 1.8f * s, by - (4 + i * 5) * s, 3.6f * s, 0.9f * s);
                    break;
                case 3: // autumn maple
                    g.FillRectangle(B(trunk), x - 2 * s, by - 14 * s, 4 * s, 14 * s);
                    g.FillEllipse(B(C(230, 110, 35)), x - 12 * s, by - 32 * s, 24 * s, 20 * s);
                    g.FillEllipse(B(C(215, 60, 40)), x - 9 * s, by - 26 * s, 10 * s, 8 * s);
                    g.FillEllipse(B(C(250, 170, 50)), x + 1 * s, by - 31 * s, 9 * s, 8 * s);
                    break;
                case 4: // blue spruce
                    g.FillRectangle(B(trunk), x - 1.5f * s, by - 5 * s, 3 * s, 5 * s);
                    g.FillPolygon(B(C(70, 130, 125)), new[] { new PointF(x - 8 * s, by - 4 * s), new PointF(x + 8 * s, by - 4 * s), new PointF(x, by - 38 * s) });
                    g.FillPolygon(B(C(95, 155, 145)), new[] { new PointF(x - 2 * s, by - 10 * s), new PointF(x + 4 * s, by - 10 * s), new PointF(x, by - 32 * s) });
                    break;
                default: // golden tree
                    g.FillRectangle(B(trunk), x - 2 * s, by - 13 * s, 4 * s, 13 * s);
                    g.FillEllipse(B(C(235, 195, 50)), x - 11 * s, by - 30 * s, 22 * s, 19 * s);
                    g.FillEllipse(B(C(250, 225, 110)), x - 6 * s, by - 28 * s, 8 * s, 6 * s);
                    break;
            }
        }

        /// <summary>A smiling child; (x,by) is between the feet.</summary>
        static void KKid(Graphics g, float x, float by, float s, Color shirt, Color hair, bool girl)
        {
            var skin = C(250, 215, 185);
            using (var b = B(C(50, 60, 110)))
            {
                g.FillRectangle(b, x - 4 * s, by - 9 * s, 3 * s, 9 * s);
                g.FillRectangle(b, x + 1 * s, by - 9 * s, 3 * s, 9 * s);
            }
            using (var pen = P(skin, 2.2f * s))
            {
                g.DrawLine(pen, x - 6 * s, by - 19 * s, x - 9 * s, by - 12 * s);
                g.DrawLine(pen, x + 6 * s, by - 19 * s, x + 9 * s, by - 12 * s);
            }
            FillRound(g, new RectangleF(x - 6 * s, by - 21 * s, 12 * s, 13 * s), 3 * s, shirt);
            if (girl)
                g.FillPolygon(B(Shade(shirt, -30)), new[] { new PointF(x - 6 * s, by - 11 * s), new PointF(x + 6 * s, by - 11 * s), new PointF(x + 8 * s, by - 6 * s), new PointF(x - 8 * s, by - 6 * s) });
            g.FillEllipse(B(skin), x - 6 * s, by - 33 * s, 12 * s, 12 * s);
            g.FillPie(B(hair), x - 6.5f * s, by - 34 * s, 13 * s, 10 * s, 180, 180);
            if (girl)
            {
                g.FillEllipse(B(hair), x - 9 * s, by - 29 * s, 4 * s, 6 * s);
                g.FillEllipse(B(hair), x + 5 * s, by - 29 * s, 4 * s, 6 * s);
            }
            using (var b = B(C(40, 30, 25)))
            {
                g.FillEllipse(b, x - 3.2f * s, by - 28 * s, 1.8f * s, 2 * s);
                g.FillEllipse(b, x + 1.4f * s, by - 28 * s, 1.8f * s, 2 * s);
            }
            using (var pen = P(C(190, 80, 80), 0.9f * s)) g.DrawArc(pen, x - 2.5f * s, by - 27 * s, 5 * s, 3.5f * s, 20, 140);
        }

        /// <summary>A white football with black patches.</summary>
        static void KBall(Graphics g, float cx, float cy, float r)
        {
            g.FillEllipse(Brushes.White, cx - r, cy - r, 2 * r, 2 * r);
            var st = g.Save();
            using (var clip = new GraphicsPath())
            {
                clip.AddEllipse(cx - r, cy - r, 2 * r, 2 * r);
                g.SetClip(clip, CombineMode.Intersect);
                using (var b = B(C(30, 30, 35)))
                {
                    g.FillPolygon(b, KPoly(cx, cy, r * 0.38f, 5, -Math.PI / 2));
                    for (int i = 0; i < 5; i++)
                    {
                        double a = -Math.PI / 2 + i * Math.PI * 2 / 5;
                        g.FillPolygon(b, KPoly(cx + (float)Math.Cos(a) * r * 1.05f, cy + (float)Math.Sin(a) * r * 1.05f, r * 0.36f, 5, a + Math.PI / 5));
                    }
                }
            }
            g.Restore(st);
            using (var pen = P(C(60, 60, 70), Math.Max(0.8f, r * 0.08f))) g.DrawEllipse(pen, cx - r, cy - r, 2 * r, 2 * r);
        }

        /// <summary>A regular polygon with n corners.</summary>
        static PointF[] KPoly(float cx, float cy, float r, int n, double start)
        {
            var pts = new PointF[n];
            for (int i = 0; i < n; i++)
            {
                double a = start + i * Math.PI * 2 / n;
                pts[i] = new PointF(cx + (float)Math.Cos(a) * r, cy + (float)Math.Sin(a) * r);
            }
            return pts;
        }

        /// <summary>A peacock with an open tail; (x,by) is at its feet.</summary>
        static void KPeacock(Graphics g, float x, float by, float s)
        {
            for (int i = 0; i < 9; i++)
            {
                double a = Math.PI * (1.08 + i * 0.105);
                float ex = x + (float)Math.Cos(a) * 15 * s, ey = by - 8 * s + (float)Math.Sin(a) * 15 * s;
                using (var pen = P(C(40, 130, 80), 1.2f * s)) g.DrawLine(pen, x, by - 8 * s, ex, ey);
                g.FillEllipse(B(C(50, 160, 90)), ex - 3.5f * s, ey - 3.5f * s, 7 * s, 7 * s);
                g.FillEllipse(B(C(230, 190, 60)), ex - 2 * s, ey - 2 * s, 4 * s, 4 * s);
                g.FillEllipse(B(C(30, 70, 170)), ex - 1.1f * s, ey - 1.1f * s, 2.2f * s, 2.2f * s);
            }
            using (var pen = P(C(200, 150, 60), 1f * s))
            {
                g.DrawLine(pen, x - 1 * s, by - 4 * s, x - 1.5f * s, by);
                g.DrawLine(pen, x + 1 * s, by - 4 * s, x + 1.5f * s, by);
            }
            g.FillEllipse(B(C(30, 90, 190)), x - 4 * s, by - 12 * s, 8 * s, 9 * s);
            using (var pen = P(C(30, 90, 190), 2.4f * s)) g.DrawLine(pen, x + 1 * s, by - 10 * s, x + 2 * s, by - 17 * s);
            g.FillEllipse(B(C(30, 90, 190)), x + 0.5f * s, by - 20 * s, 4 * s, 4 * s);
            g.FillPolygon(B(C(240, 180, 60)), new[] { new PointF(x + 4.3f * s, by - 18.6f * s), new PointF(x + 6.5f * s, by - 18 * s), new PointF(x + 4.3f * s, by - 17.4f * s) });
            using (var pen = P(C(30, 90, 190), 0.6f * s))
                for (int i = 0; i < 3; i++) g.DrawLine(pen, x + 2.5f * s, by - 20 * s, x + (1 + i * 1.5f) * s, by - 23 * s);
            g.FillEllipse(Brushes.White, x + 2.3f * s, by - 19.2f * s, 1.3f * s, 1.3f * s);
        }

        /// <summary>A soft cloud made of overlapping ellipses.</summary>
        static void KCloud(Graphics g, float x, float y, float w, int alpha)
        {
            using (var b = B(CA(alpha, 255, 255, 255)))
            {
                g.FillEllipse(b, x, y + w * 0.12f, w * 0.5f, w * 0.26f);
                g.FillEllipse(b, x + w * 0.22f, y, w * 0.45f, w * 0.34f);
                g.FillEllipse(b, x + w * 0.5f, y + w * 0.08f, w * 0.5f, w * 0.3f);
            }
        }

        // ------------------------------------------------------------------ Kórnik posters

        public const int KornikPosterKinds = 8;

        /// <summary>Short bilingual title shown inside the picture of a Kórnik poster.</summary>
        static string KornikPosterTitle(int kind)
        {
            switch (kind)
            {
                case 0: return L.Pl ? "Łabędzie na jeziorze" : "Swans on the lake";
                case 1: return L.Pl ? "Arboretum Kórnickie" : "Kórnik Arboretum";
                case 2: return L.Pl ? "Ratusz w Bninie 1777" : "Bnin Town Hall 1777";
                case 3: return L.Pl ? "Biała Dama" : "The White Lady";
                case 4: return L.Pl ? "Biblioteka Kórnicka" : "Kórnik Library";
                case 5: return L.Pl ? "Mapa Kórnika" : "Map of Kórnik";
                case 6: return L.Pl ? "Ogród Teofili" : "Teofila's garden";
                default: return L.Pl ? "Uczymy się razem!" : "We learn together!";
            }
        }

        /// <summary>
        /// A framed poster about Kórnik on the painted wall (kind 0..7: swans on the lake, the Arboretum,
        /// Bnin town hall, the White Lady legend, the Kórnik Library, a map, Teofila's garden, SP 2 poster).
        /// </summary>
        public static Tex KornikPoster(FloorTheme th, int kind)
        {
            kind = ((kind % KornikPosterKinds) + KornikPosterKinds) % KornikPosterKinds;
            return Wall(th, 60 + kind, g =>
            {
                var frame = new RectangleF(14, 10, 100, 102);
                g.FillRectangle(B(CA(80, 0, 0, 0)), frame.X + 3, frame.Y + 3, frame.Width, frame.Height);
                Grad(g, frame, C(175, 120, 65), C(105, 65, 30), 45f, false);
                using (var pen = P(C(225, 185, 110), 1.2f)) g.DrawRectangle(pen, frame.X + 2.5f, frame.Y + 2.5f, frame.Width - 5, frame.Height - 5);
                var pic = new RectangleF(20, 16, 88, 90);
                var st = g.Save();
                g.SetClip(pic);
                KornikPicture(g, kind, pic);
                // title strip
                var strip = new RectangleF(pic.X, pic.Bottom - 17, pic.Width, 17);
                g.FillRectangle(B(CA(225, 255, 252, 240)), strip);
                g.FillRectangle(B(kind == 7 ? C(220, 60, 60) : C(60, 120, 190)), strip.X, strip.Y, strip.Width, 1.5f);
                TextIn(g, KornikPosterTitle(kind), new RectangleF(strip.X + 2, strip.Y + 1.5f, strip.Width - 4, strip.Height - 2), 12, kind == 7 ? C(200, 40, 40) : C(40, 50, 80));
                g.Restore(st);
                using (var pen = P(C(80, 50, 25), 1.5f)) g.DrawRectangle(pen, pic.X, pic.Y, pic.Width, pic.Height);
                // glossy photo highlight
                g.FillPolygon(B(CA(28, 255, 255, 255)), new[] { new PointF(pic.X, pic.Y), new PointF(pic.X + 40, pic.Y), new PointF(pic.X, pic.Y + 40) });
            });
        }

        /// <summary>Paints the picture of a Kórnik poster into p (the bottom 17 px are covered by the title).</summary>
        static void KornikPicture(Graphics g, int kind, RectangleF p)
        {
            var r = new Random(300 + kind);
            float cx = p.X + p.Width / 2;
            switch (kind)
            {
                case 0: // swans on Lake Kórnik, castle on the far shore
                    {
                        Grad(g, new RectangleF(p.X, p.Y, p.Width, 36), C(110, 170, 235), C(220, 238, 250), 90f, false);
                        KCloud(g, p.X + 4, p.Y + 4, 30, 230);
                        KCloud(g, p.X + 52, p.Y + 10, 24, 210);
                        g.FillEllipse(B(C(70, 140, 70)), p.X - 20, p.Y + 28, 70, 18);
                        g.FillEllipse(B(C(60, 125, 65)), p.X + 40, p.Y + 27, 80, 18);
                        for (int i = 0; i < 6; i++) KTree(g, p.X + 6 + i * 16 + (i > 2 ? 12 : 0), p.Y + 36, 0.35f, i % 2);
                        KCastle(g, p.X + 52, p.Y + 37, 0.42f, C(240, 228, 200), C(175, 60, 50), C(80, 90, 120));
                        Grad(g, new RectangleF(p.X, p.Y + 36, p.Width, p.Height - 36), C(90, 150, 210), C(40, 95, 160), 90f, false);
                        g.FillRectangle(B(CA(70, 240, 228, 200)), p.X + 38, p.Y + 37, 28, 5);
                        using (var pen = P(CA(150, 255, 255, 255), 1))
                            for (int i = 0; i < 9; i++)
                            {
                                float wx = p.X + F(r) * p.Width, wy = p.Y + 42 + F(r) * 30;
                                g.DrawLine(pen, wx, wy, wx + 6 + F(r) * 6, wy);
                            }
                        KSwan(g, p.X + 44, p.Y + 62, 1.25f, false);
                        KSwan(g, p.X + 66, p.Y + 69, 0.85f, true);
                        using (var pen = P(CA(120, 255, 255, 255), 1))
                        {
                            g.DrawEllipse(pen, p.X + 26, p.Y + 61, 34, 4);
                            g.DrawEllipse(pen, p.X + 56, p.Y + 68, 22, 3);
                        }
                        // reeds and cattails in the corners
                        foreach (var rx in new[] { p.X + 2f, p.Right - 14f })
                            for (int i = 0; i < 6; i++)
                            {
                                float x = rx + i * 2.2f, top = p.Y + 46 + F(r) * 12;
                                using (var pen = P(C(80, 140, 60), 1.3f)) g.DrawBezier(pen, x, p.Bottom, x, p.Bottom - 10, x + 1.5f, top + 10, x + (i % 2 == 0 ? 2 : -1), top);
                                if (i % 2 == 1) g.FillEllipse(B(C(120, 75, 40)), x - 1.5f, top + 2, 3, 8);
                            }
                        break;
                    }
                case 1: // the Arboretum: trees from all over the world
                    {
                        Grad(g, new RectangleF(p.X, p.Y, p.Width, 50), C(120, 185, 240), C(225, 242, 250), 90f, false);
                        g.FillEllipse(B(C(255, 230, 90)), p.Right - 22, p.Y + 4, 16, 16);
                        KCloud(g, p.X + 6, p.Y + 6, 26, 220);
                        g.FillEllipse(B(C(110, 185, 80)), p.X - 30, p.Y + 40, 100, 50);
                        g.FillEllipse(B(C(95, 170, 70)), p.X + 30, p.Y + 44, 100, 50);
                        g.FillRectangle(B(C(95, 170, 70)), p.X, p.Y + 62, p.Width, 30);
                        using (var path = new GraphicsPath())
                        {
                            path.AddPolygon(new[] { new PointF(cx - 4, p.Y + 54), new PointF(cx + 4, p.Y + 54), new PointF(cx + 22, p.Bottom), new PointF(cx - 14, p.Bottom) });
                            g.FillPath(B(C(225, 200, 150)), path);
                        }
                        int[] kinds = { 4, 0, 3, 2, 1, 5, 0, 2, 3, 1 };
                        float[] xs = { 8, 22, 36, 50, 64, 78, 14, 30, 60, 80 };
                        float[] ys = { 50, 47, 52, 48, 50, 47, 72, 70, 71, 73 };
                        float[] ss = { 0.75f, 0.8f, 0.7f, 0.8f, 0.75f, 0.8f, 1.05f, 1f, 1.05f, 1f };
                        for (int i = 0; i < kinds.Length; i++) KTree(g, p.X + xs[i], p.Y + ys[i], ss[i], kinds[i]);
                        // a tiny signpost
                        g.FillRectangle(B(C(120, 80, 45)), cx + 3, p.Y + 58, 2, 12);
                        FillRound(g, new RectangleF(cx - 3, p.Y + 56, 14, 6), 1.5f, C(250, 240, 210));
                        break;
                    }
                case 2: // the baroque town hall in Bnin (built by Teofila in 1777)
                    {
                        Grad(g, new RectangleF(p.X, p.Y, p.Width, 70), C(120, 180, 235), C(225, 240, 250), 90f, false);
                        KCloud(g, p.X + 4, p.Y + 6, 24, 220);
                        KCloud(g, p.X + 62, p.Y + 3, 22, 200);
                        KTree(g, p.X + 7, p.Y + 66, 0.9f, 0);
                        KTree(g, p.Right - 7, p.Y + 66, 0.9f, 0);
                        g.FillRectangle(B(C(190, 180, 165)), p.X, p.Y + 64, p.Width, 30);
                        using (var pen = P(C(160, 150, 135), 0.8f))
                            for (int i = 0; i < 14; i++) g.DrawEllipse(pen, p.X + (i * 7) % 88, p.Y + 66 + (i % 3) * 3, 6, 2.5f);
                        var wall = C(248, 236, 212);
                        float bx = cx - 30, by = p.Y + 40;
                        g.FillRectangle(B(Shade(wall, -10)), bx, by, 60, 26);
                        g.FillRectangle(B(wall), bx + 1, by, 58, 25);
                        // mansard roof
                        g.FillPolygon(B(C(150, 62, 45)), new[] { new PointF(bx - 3, by), new PointF(bx + 63, by), new PointF(bx + 54, by - 11), new PointF(bx + 6, by - 11) });
                        using (var pen = P(C(120, 45, 35), 0.8f))
                            for (int i = 1; i < 4; i++) g.DrawLine(pen, bx - 3 + i * 2.2f, by - i * 2.7f, bx + 63 - i * 2.2f, by - i * 2.7f);
                        // tower with clock and baroque cupola
                        g.FillRectangle(B(wall), cx - 7, by - 24, 14, 14);
                        g.FillRectangle(B(Shade(wall, -20)), cx - 8, by - 25, 16, 2);
                        g.FillEllipse(Brushes.White, cx - 5, by - 22, 10, 10);
                        using (var pen = P(C(40, 40, 50), 0.9f))
                        {
                            g.DrawEllipse(pen, cx - 5, by - 22, 10, 10);
                            g.DrawLine(pen, cx, by - 17, cx, by - 20.5f);
                            g.DrawLine(pen, cx, by - 17, cx + 2.5f, by - 17);
                        }
                        g.FillEllipse(B(C(70, 120, 100)), cx - 7, by - 33, 14, 11);
                        g.FillRectangle(B(C(70, 120, 100)), cx - 2, by - 38, 4, 6);
                        g.FillEllipse(B(C(70, 120, 100)), cx - 3.5f, by - 42, 7, 6);
                        using (var pen = P(C(220, 180, 60), 1)) g.DrawLine(pen, cx, by - 42, cx, by - 47);
                        g.FillEllipse(B(C(230, 190, 60)), cx - 1.3f, by - 48.5f, 2.6f, 2.6f);
                        // windows and door
                        for (int i = 0; i < 6; i++)
                        {
                            float wx = bx + 5 + i * 9.2f;
                            if (i == 2 || i == 3) continue;
                            g.FillRectangle(B(C(90, 130, 170)), wx, by + 5, 5, 7);
                            g.FillRectangle(B(C(90, 130, 170)), wx, by + 15, 5, 7);
                            g.FillRectangle(Brushes.White, wx - 0.5f, by + 12, 6, 1);
                        }
                        g.FillRectangle(B(C(120, 75, 40)), cx - 5, by + 12, 10, 14);
                        g.FillEllipse(B(C(120, 75, 40)), cx - 5, by + 8, 10, 8);
                        TextIn(g, "1777", new RectangleF(cx - 9, by + 1, 18, 7), 7, C(150, 62, 45));
                        break;
                    }
                case 3: // the White Lady: friendly night scene
                    {
                        Grad(g, p, C(25, 30, 80), C(70, 70, 135), 90f, false);
                        for (int i = 0; i < 18; i++)
                        {
                            float sx = p.X + F(r) * p.Width, sy = p.Y + F(r) * 45;
                            if (i % 4 == 0) g.FillPolygon(B(C(255, 245, 170)), StarPoints(sx, sy, 3, 1.2f));
                            else g.FillEllipse(B(C(255, 250, 220)), sx, sy, 1.6f, 1.6f);
                        }
                        // smiling moon
                        g.FillEllipse(B(CA(60, 255, 250, 200)), p.Right - 30, p.Y + 2, 26, 26);
                        g.FillEllipse(B(C(255, 245, 190)), p.Right - 26, p.Y + 6, 18, 18);
                        using (var pen = P(C(210, 180, 100), 1)) g.DrawArc(pen, p.Right - 21, p.Y + 13, 8, 6, 20, 140);
                        g.FillEllipse(B(C(210, 180, 100)), p.Right - 21, p.Y + 11, 2, 2);
                        g.FillEllipse(B(C(210, 180, 100)), p.Right - 15, p.Y + 11, 2, 2);
                        // hill and castle with warm lit windows
                        g.FillEllipse(B(C(35, 70, 60)), p.X - 20, p.Y + 62, 130, 40);
                        KCastle(g, p.X + 62, p.Y + 70, 0.62f, C(85, 90, 140), C(55, 55, 100), C(255, 215, 100));
                        // the lady in a white gown with a soft glow
                        float lx = p.X + 24, ly = p.Y + 72;
                        using (var path = new GraphicsPath())
                        {
                            path.AddEllipse(lx - 22, ly - 50, 44, 56);
                            using (var br = new PathGradientBrush(path) { CenterColor = CA(110, 230, 240, 255), SurroundColors = new[] { CA(0, 230, 240, 255) } })
                                g.FillPath(br, path);
                        }
                        using (var gown = new GraphicsPath())
                        {
                            gown.AddBezier(lx - 4, ly - 30, lx - 8, ly - 18, lx - 14, ly - 6, lx - 15, ly);
                            gown.AddLine(lx - 15, ly, lx + 15, ly);
                            gown.AddBezier(lx + 15, ly, lx + 14, ly - 6, lx + 8, ly - 18, lx + 4, ly - 30);
                            gown.CloseFigure();
                            using (var br = new LinearGradientBrush(new RectangleF(lx - 16, ly - 31, 32, 32), Color.White, C(205, 215, 235), 0f)) g.FillPath(br, gown);
                        }
                        using (var pen = P(C(200, 205, 225), 1))
                            for (int i = 0; i < 5; i++) g.DrawArc(pen, lx - 15 + i * 6, ly - 3, 6, 4, 0, 180);
                        using (var pen = P(C(250, 220, 195), 2)) { g.DrawLine(pen, lx - 4, ly - 26, lx - 9, ly - 16); g.DrawLine(pen, lx + 4, ly - 26, lx + 9, ly - 18); }
                        g.FillPolygon(B(C(255, 120, 150)), StarPoints(lx + 10, ly - 19, 2.5f, 1));
                        var face = C(250, 220, 195);
                        g.FillEllipse(B(C(235, 235, 240)), lx - 6, ly - 46, 12, 11);
                        g.FillEllipse(B(face), lx - 4.5f, ly - 39, 9, 10);
                        g.FillEllipse(B(CA(110, 240, 130, 140)), lx - 4, ly - 34, 2.5f, 1.8f);
                        g.FillEllipse(B(CA(110, 240, 130, 140)), lx + 1.5f, ly - 34, 2.5f, 1.8f);
                        g.FillEllipse(B(C(60, 50, 60)), lx - 2.5f, ly - 36.5f, 1.5f, 1.8f);
                        g.FillEllipse(B(C(60, 50, 60)), lx + 1, ly - 36.5f, 1.5f, 1.8f);
                        using (var pen = P(C(200, 90, 100), 0.8f)) g.DrawArc(pen, lx - 2, ly - 34.5f, 4, 3, 20, 140);
                        for (int i = 0; i < 4; i++) g.FillPolygon(B(CA(200, 255, 255, 230)), StarPoints(lx - 14 + i * 9, ly - 46 + (i % 2) * 8, 2.2f, 0.8f));
                        break;
                    }
                case 4: // the Kórnik Library: shelves, arched window and an open old book
                    {
                        Grad(g, p, C(185, 140, 95), C(130, 90, 55), 90f, false);
                        // arched window at the back
                        using (var win = new GraphicsPath())
                        {
                            win.AddArc(cx - 12, p.Y + 6, 24, 20, 180, 180);
                            win.AddLine(cx + 12, p.Y + 16, cx + 12, p.Y + 44);
                            win.AddLine(cx + 12, p.Y + 44, cx - 12, p.Y + 44);
                            win.CloseFigure();
                            using (var br = new LinearGradientBrush(new RectangleF(cx - 12, p.Y + 6, 24, 38), C(170, 215, 250), C(240, 248, 255), 90f)) g.FillPath(br, win);
                            using (var pen = P(C(110, 70, 40), 2)) g.DrawPath(pen, win);
                        }
                        using (var pen = P(C(110, 70, 40), 1.2f)) { g.DrawLine(pen, cx, p.Y + 6, cx, p.Y + 44); g.DrawLine(pen, cx - 12, p.Y + 26, cx + 12, p.Y + 26); }
                        // shelves on both sides
                        var cols = new[] { C(170, 50, 45), C(50, 90, 150), C(60, 130, 70), C(200, 160, 60), C(120, 60, 120), C(150, 90, 40) };
                        foreach (var sx in new[] { p.X + 2f, p.Right - 30f })
                        {
                            g.FillRectangle(B(C(95, 55, 30)), sx, p.Y + 2, 28, 72);
                            for (int s = 0; s < 4; s++)
                            {
                                float shelfY = p.Y + 6 + s * 17;
                                float x = sx + 2;
                                while (x < sx + 25)
                                {
                                    float w = 2.5f + r.Next(3), h = 10 + r.Next(4);
                                    g.FillRectangle(B(cols[r.Next(cols.Length)]), x, shelfY + 14 - h, w, h);
                                    g.FillRectangle(B(CA(180, 240, 210, 120)), x, shelfY + 14 - h + 2, w, 1);
                                    x += w + 0.6f;
                                }
                                g.FillRectangle(B(C(150, 100, 60)), sx, shelfY + 14, 28, 2.5f);
                            }
                        }
                        // parquet floor
                        g.FillRectangle(B(C(160, 110, 65)), p.X, p.Y + 62, p.Width, 30);
                        // table with the open book
                        g.FillRectangle(B(C(110, 65, 35)), cx - 26, p.Y + 58, 52, 6);
                        g.FillRectangle(B(C(90, 50, 25)), cx - 22, p.Y + 64, 4, 10);
                        g.FillRectangle(B(C(90, 50, 25)), cx + 18, p.Y + 64, 4, 10);
                        g.FillRectangle(B(C(120, 50, 40)), cx - 20, p.Y + 56, 40, 3);
                        using (var page = new GraphicsPath())
                        {
                            page.AddBezier(cx, p.Y + 57, cx - 6, p.Y + 53, cx - 14, p.Y + 53, cx - 19, p.Y + 55);
                            page.AddLine(cx - 19, p.Y + 55, cx - 18, p.Y + 44);
                            page.AddBezier(cx - 18, p.Y + 44, cx - 12, p.Y + 42, cx - 5, p.Y + 43, cx, p.Y + 47);
                            page.CloseFigure();
                            g.FillPath(B(C(250, 240, 210)), page);
                            using (var m = new Matrix(-1, 0, 0, 1, 2 * cx, 0)) page.Transform(m);
                            g.FillPath(B(C(245, 232, 200)), page);
                        }
                        using (var pen = P(C(150, 130, 100), 0.7f))
                            for (int i = 0; i < 4; i++)
                            {
                                g.DrawLine(pen, cx - 16, p.Y + 47 + i * 2, cx - 3, p.Y + 48.5f + i * 2);
                                g.DrawLine(pen, cx + 3, p.Y + 48.5f + i * 2, cx + 16, p.Y + 47 + i * 2);
                            }
                        g.FillEllipse(B(C(200, 60, 50)), cx - 15, p.Y + 46, 3, 3);
                        using (var pen = P(C(200, 40, 40), 1.2f)) g.DrawLine(pen, cx, p.Y + 50, cx + 1, p.Y + 62);
                        break;
                    }
                case 5: // a stylized map of Kórnik
                    {
                        g.FillRectangle(B(C(240, 226, 185)), p);
                        for (int i = 0; i < 90; i++) g.FillEllipse(B(CA(50, 170, 140, 90)), p.X + F(r) * p.Width, p.Y + F(r) * p.Height, 3, 2);
                        g.FillEllipse(B(CA(90, 140, 190, 110)), p.X + 6, p.Y + 10, 34, 30);
                        g.FillEllipse(B(CA(70, 140, 190, 110)), p.Right - 24, p.Y + 46, 30, 24);
                        // the long lake running from north to south
                        using (var lake = new GraphicsPath())
                        {
                            lake.AddBezier(p.X + 48, p.Y - 2, p.X + 44, p.Y + 20, p.X + 56, p.Y + 34, p.X + 50, p.Y + 52);
                            lake.AddBezier(p.X + 50, p.Y + 52, p.X + 46, p.Y + 62, p.X + 54, p.Y + 72, p.X + 58, p.Y + 80);
                            lake.AddLine(p.X + 70, p.Y + 80, p.X + 70, p.Y + 80);
                            lake.AddBezier(p.X + 70, p.Y + 80, p.X + 64, p.Y + 70, p.X + 60, p.Y + 60, p.X + 64, p.Y + 50);
                            lake.AddBezier(p.X + 64, p.Y + 50, p.X + 70, p.Y + 32, p.X + 58, p.Y + 18, p.X + 62, p.Y - 2);
                            lake.CloseFigure();
                            g.FillPath(B(C(110, 170, 225)), lake);
                            using (var pen = P(C(60, 120, 185), 1.2f)) g.DrawPath(pen, lake);
                        }
                        using (var pen = P(CA(160, 255, 255, 255), 0.8f))
                            for (int i = 0; i < 4; i++) g.DrawArc(pen, p.X + 52 + (i % 2) * 3, p.Y + 10 + i * 15, 5, 3, 200, 140);
                        // roads
                        using (var pen = new Pen(C(170, 120, 80), 1.2f) { DashStyle = DashStyle.Dash })
                        {
                            g.DrawBezier(pen, p.X, p.Y + 46, p.X + 14, p.Y + 44, p.X + 30, p.Y + 40, p.X + 38, p.Y + 24);
                            g.DrawBezier(pen, p.X + 22, p.Y + 44, p.X + 28, p.Y + 50, p.X + 34, p.Y + 56, p.X + 38, p.Y + 62);
                        }
                        // castle and the Arboretum
                        KCastle(g, p.X + 39, p.Y + 22, 0.3f, C(240, 228, 200), C(190, 60, 50), C(90, 90, 120));
                        TextIn(g, "Kórnik", new RectangleF(p.X + 2, p.Y + 2, 24, 9), 9, C(80, 50, 30));
                        for (int i = 0; i < 5; i++) KTree(g, p.X + 7 + (i % 3) * 6 + (i / 3) * 3, p.Y + 27 + (i / 3) * 6, 0.32f, i % 2 == 0 ? 0 : 1);
                        FillRound(g, new RectangleF(p.X + 1, p.Y + 34, 30, 8), 2, CA(170, 250, 240, 205));
                        TextIn(g, "Arboretum", new RectangleF(p.X + 1, p.Y + 34, 30, 8), 8, C(40, 100, 50));
                        // our school
                        float sx = p.X + 16, sy = p.Y + 52;
                        g.FillRectangle(B(C(250, 230, 170)), sx - 6, sy - 6, 12, 8);
                        g.FillPolygon(B(C(210, 60, 50)), new[] { new PointF(sx - 8, sy - 6), new PointF(sx + 8, sy - 6), new PointF(sx, sy - 12) });
                        g.FillRectangle(B(C(120, 80, 45)), sx - 1.5f, sy - 3, 3, 5);
                        FillRound(g, new RectangleF(sx - 9, sy + 3, 18, 8), 2, C(220, 60, 60));
                        TextIn(g, "SP 2", new RectangleF(sx - 9, sy + 3, 18, 8), 7, Color.White);
                        // Bnin in the south
                        for (int i = 0; i < 3; i++)
                        {
                            float hx = p.X + 34 + i * 5, hy = p.Y + 66 + (i % 2) * 2;
                            g.FillRectangle(B(C(250, 240, 220)), hx, hy, 4, 3);
                            g.FillPolygon(B(C(190, 80, 60)), new[] { new PointF(hx - 0.5f, hy), new PointF(hx + 4.5f, hy), new PointF(hx + 2, hy - 2.5f) });
                        }
                        TextIn(g, "Bnin", new RectangleF(p.X + 31, p.Y + 54, 20, 9), 9, C(80, 50, 30));
                        // compass rose
                        float rx = p.Right - 12, ry = p.Y + 12;
                        g.FillPolygon(B(C(200, 60, 50)), new[] { new PointF(rx, ry - 9), new PointF(rx + 2.5f, ry), new PointF(rx - 2.5f, ry) });
                        g.FillPolygon(B(C(90, 80, 70)), new[] { new PointF(rx, ry + 9), new PointF(rx + 2.5f, ry), new PointF(rx - 2.5f, ry) });
                        g.FillPolygon(B(C(160, 150, 130)), new[] { new PointF(rx - 7, ry), new PointF(rx, ry - 2), new PointF(rx + 7, ry), new PointF(rx, ry + 2) });
                        TextIn(g, "N", new RectangleF(rx - 4, ry - 19, 8, 9), 8, C(200, 60, 50));
                        using (var pen = P(C(150, 110, 70), 1)) g.DrawRectangle(pen, p.X + 1.5f, p.Y + 1.5f, p.Width - 3, p.Height - 3);
                        break;
                    }
                case 6: // Teofila's French garden: fountains with lions, a whale and crocodiles, swans and a peacock
                    {
                        Grad(g, new RectangleF(p.X, p.Y, p.Width, 36), C(120, 185, 240), C(225, 242, 250), 90f, false);
                        KCloud(g, p.X + 50, p.Y + 4, 26, 220);
                        g.FillRectangle(B(C(110, 185, 85)), p.X, p.Y + 30, p.Width, 64);
                        // trimmed hedges
                        for (int i = 0; i < 6; i++)
                        {
                            float hx = p.X + 6 + i * 15.5f;
                            g.FillPolygon(B(C(40, 115, 60)), new[] { new PointF(hx - 5, p.Y + 34), new PointF(hx + 5, p.Y + 34), new PointF(hx, p.Y + 16) });
                        }
                        g.FillRectangle(B(C(70, 150, 70)), p.X, p.Y + 32, p.Width, 3);
                        // gravel paths
                        g.FillRectangle(B(C(230, 215, 175)), p.X, p.Y + 50, p.Width, 5);
                        // canal with swans
                        Grad(g, new RectangleF(p.X, p.Y + 61, p.Width, 13), C(110, 175, 230), C(60, 120, 190), 90f, false);
                        g.FillRectangle(B(C(210, 205, 195)), p.X, p.Y + 60, p.Width, 1.5f);
                        KSwan(g, p.X + 22, p.Y + 70, 0.75f, false);
                        KSwan(g, p.X + 70, p.Y + 71, 0.65f, true);
                        // fountain basin
                        float fy = p.Y + 50;
                        g.FillEllipse(B(C(200, 195, 190)), cx - 26, fy - 8, 52, 14);
                        g.FillEllipse(B(C(110, 180, 235)), cx - 23, fy - 6.5f, 46, 10);
                        // whale in the middle: water from its mouth
                        g.FillEllipse(B(C(70, 110, 170)), cx - 7, fy - 17, 14, 9);
                        g.FillPolygon(B(C(70, 110, 170)), new[] { new PointF(cx - 6, fy - 13), new PointF(cx - 12, fy - 19), new PointF(cx - 11, fy - 11) });
                        g.FillEllipse(Brushes.White, cx + 2, fy - 15, 2, 2);
                        // lion on the left
                        float lx = cx - 18;
                        g.FillEllipse(B(C(200, 140, 50)), lx - 5, fy - 18, 10, 10);
                        g.FillEllipse(B(C(240, 200, 100)), lx - 3, fy - 16, 6, 6);
                        g.FillEllipse(B(C(60, 40, 20)), lx - 1.8f, fy - 14.5f, 1.2f, 1.2f);
                        g.FillEllipse(B(C(60, 40, 20)), lx + 0.6f, fy - 14.5f, 1.2f, 1.2f);
                        g.FillRectangle(B(C(180, 175, 170)), lx - 4, fy - 9, 8, 4);
                        // crocodile on the right
                        float kx = cx + 18;
                        g.FillEllipse(B(C(70, 140, 60)), kx - 5, fy - 15, 9, 7);
                        g.FillPolygon(B(C(70, 140, 60)), new[] { new PointF(kx - 4, fy - 13), new PointF(kx - 12, fy - 14), new PointF(kx - 12, fy - 12), new PointF(kx - 4, fy - 10) });
                        g.FillEllipse(Brushes.White, kx - 3, fy - 16, 2.5f, 2.5f);
                        g.FillEllipse(B(C(30, 30, 30)), kx - 2.4f, fy - 15.4f, 1.2f, 1.2f);
                        g.FillRectangle(B(C(180, 175, 170)), kx - 4, fy - 9, 8, 4);
                        using (var pen = P(CA(230, 170, 220, 255), 1.4f))
                        {
                            g.DrawBezier(pen, lx + 4, fy - 12, lx + 9, fy - 15, lx + 11, fy - 8, lx + 12, fy - 3);
                            g.DrawBezier(pen, kx - 12, fy - 13, kx - 16, fy - 15, kx - 18, fy - 8, kx - 19, fy - 3);
                            g.DrawBezier(pen, cx + 6, fy - 14, cx + 6, fy - 26, cx + 2, fy - 24, cx, fy - 4);
                            g.DrawBezier(pen, cx + 6, fy - 14, cx + 8, fy - 24, cx + 12, fy - 18, cx + 10, fy - 4);
                        }
                        // a peacock on the lawn
                        KPeacock(g, p.Right - 12, p.Y + 58, 0.8f);
                        break;
                    }
                default: // SP 2 Kórnik school poster
                    {
                        Grad(g, new RectangleF(p.X, p.Y, p.Width, 60), C(110, 180, 240), C(215, 238, 252), 90f, false);
                        g.FillEllipse(B(C(255, 225, 80)), p.X + 4, p.Y + 4, 14, 14);
                        KCloud(g, p.X + 56, p.Y + 4, 28, 230);
                        g.FillRectangle(B(C(110, 190, 80)), p.X, p.Y + 56, p.Width, 40);
                        // school building
                        float bx = p.X + 12, by = p.Y + 28;
                        g.FillRectangle(B(C(250, 228, 160)), bx, by, 64, 30);
                        g.FillPolygon(B(C(200, 70, 55)), new[] { new PointF(bx - 4, by), new PointF(bx + 68, by), new PointF(bx + 58, by - 9), new PointF(bx + 6, by - 9) });
                        for (int row = 0; row < 2; row++)
                            for (int c = 0; c < 6; c++)
                            {
                                if (row == 1 && (c == 2 || c == 3)) continue;
                                g.FillRectangle(Brushes.White, bx + 4 + c * 10, by + 3 + row * 13, 7, 8);
                                g.FillRectangle(B(C(120, 180, 230)), bx + 5 + c * 10, by + 4 + row * 13, 5, 6);
                            }
                        g.FillRectangle(B(C(120, 80, 45)), bx + 27, by + 18, 10, 12);
                        FillRound(g, new RectangleF(bx + 22, by + 12, 20, 6), 1.5f, C(40, 110, 190));
                        TextIn(g, "SP 2", new RectangleF(bx + 22, by + 12, 20, 6), 6, Color.White);
                        // Polish flag on the roof
                        using (var pen = P(C(120, 120, 130), 1)) g.DrawLine(pen, bx + 32, by - 9, bx + 32, by - 22);
                        g.FillRectangle(Brushes.White, bx + 32.5f, by - 22, 10, 3);
                        g.FillRectangle(B(C(220, 30, 50)), bx + 32.5f, by - 19, 10, 3);
                        TextIn(g, "SP 2 Kórnik", new RectangleF(p.X + 36, p.Y + 1, p.Width - 38, 12), 11, C(200, 40, 40));
                        // children holding hands
                        var shirts = new[] { C(230, 70, 70), C(60, 140, 230), C(250, 190, 40), C(130, 80, 200) };
                        var hairs = new[] { C(90, 55, 30), C(240, 200, 100), C(40, 30, 25), C(160, 80, 40) };
                        for (int i = 0; i < 4; i++)
                            KKid(g, p.X + 14 + i * 20, p.Y + 73, 1f, shirts[i], hairs[i], i % 2 == 1);
                        break;
                    }
            }
        }

        /// <summary>A friendly fact (1-2 sentences) for the caption box of a Kórnik poster.</summary>
        public static string KornikPosterCaption(int kind)
        {
            kind = ((kind % KornikPosterKinds) + KornikPosterKinds) % KornikPosterKinds;
            switch (kind)
            {
                case 0:
                    return L.Pl ? "Po Jeziorze Kórnickim pływają białe łabędzie. Nad jeziorem stoi Zamek w Kórniku."
                                : "White swans swim on Lake Kórnik. Kórnik Castle stands by the lake.";
                case 1:
                    return L.Pl ? "Arboretum Kórnickie to wielki ogród drzew. Rosną w nim drzewa z całego świata!"
                                : "The Kórnik Arboretum is a big garden of trees. Trees from all over the world grow there!";
                case 2:
                    return L.Pl ? "Teofila, patronka naszej szkoły, kazała zbudować ratusz w Bninie w 1777 roku."
                                : "Teofila, the patron of our school, had the Bnin town hall built in 1777.";
                case 3:
                    return L.Pl ? "Teofila z Działyńskich Szołdrska-Potulicka (1714–1790) to patronka naszej szkoły. Legenda mówi, że o północy jako Biała Dama wychodzi ze swojego portretu i spaceruje po zamku."
                                : "Teofila z Działyńskich Szołdrska-Potulicka (1714–1790) is the patron of our school. The legend says that at midnight she steps out of her portrait as the White Lady and walks around the castle.";
                case 4:
                    return L.Pl ? "Bibliotekę Kórnicką założył Tytus Działyński. Można w niej zobaczyć bardzo stare książki."
                                : "Tytus Działyński founded the Kórnik Library. Very old books are kept there.";
                case 5:
                    return L.Pl ? "Na mapie widać długie Jezioro Kórnickie, zamek, Arboretum, Bnin i naszą szkołę SP 2."
                                : "The map shows the long Lake Kórnik, the castle, the Arboretum, Bnin and our school SP 2.";
                case 6:
                    return L.Pl ? "W ogrodzie Teofili woda tryskała z pysków krokodyli, wielorybów i lwów. Po kanale pływały łabędzie, a po dziedzińcu chodziły żurawie, pawie i bażanty."
                                : "In Teofila's garden water flowed from the mouths of crocodiles, whales and lions. Swans swam on the canal, and cranes, peacocks and pheasants walked in the courtyard.";
                default:
                    return L.Pl ? "Szkoła Podstawowa nr 2 im. Teofili z Działyńskich Szołdrskiej-Potulickiej w Kórniku. Uczymy się razem!"
                                : "Primary School No. 2 named after Teofila z Działyńskich Szołdrska-Potulicka in Kórnik. We learn together!";
            }
        }

        // ------------------------------------------------------------------ other school walls

        /// <summary>A glass display case (gablota) with a model of Kórnik Castle, a swan figurine, a golden cup and an old book.</summary>
        public static Tex DisplayCase(FloorTheme th)
        {
            return Wall(th, 70, g =>
            {
                var outer = new RectangleF(10, 8, 108, 110);
                g.FillRectangle(B(CA(80, 0, 0, 0)), outer.X + 3, outer.Y + 3, outer.Width, outer.Height);
                Grad(g, outer, C(130, 85, 50), C(85, 50, 25), 0f, false);
                var inner = new RectangleF(16, 14, 96, 96);
                Grad(g, inner, C(50, 75, 135), C(30, 45, 90), 90f, false);
                // spot light from the top
                g.FillEllipse(B(CA(50, 255, 250, 220)), inner.X + 10, inner.Y - 20, 76, 50);
                // glass shelf in the middle
                float shelf = inner.Y + 50;
                g.FillRectangle(B(CA(200, 200, 230, 240)), inner.X, shelf, inner.Width, 3);
                g.FillRectangle(B(CA(90, 0, 0, 0)), inner.X, shelf + 3, inner.Width, 2);
                // upper shelf: castle model on a plinth and a swan figurine on a pedestal
                g.FillRectangle(B(C(200, 190, 170)), 22, shelf - 5, 44, 5);
                KCastle(g, 44, shelf - 5, 0.52f, C(242, 232, 205), C(185, 65, 50), C(90, 100, 130));
                g.FillEllipse(B(C(210, 210, 220)), 77, shelf - 6, 26, 7);
                g.FillRectangle(B(C(210, 210, 220)), 77, shelf - 3, 26, 3);
                KSwan(g, 91, shelf - 5, 1.1f, true);
                // lower shelf: golden cup
                float bot = inner.Bottom;
                float cx = 42;
                using (var gold = new LinearGradientBrush(new RectangleF(cx - 14, bot - 44, 28, 44), C(255, 235, 120), C(200, 140, 20), 0f))
                {
                    g.FillRectangle(gold, cx - 10, bot - 6, 20, 6);
                    g.FillRectangle(gold, cx - 2, bot - 16, 4, 10);
                    using (var bowl = new GraphicsPath())
                    {
                        bowl.AddLine(cx - 12, bot - 40, cx + 12, bot - 40);
                        bowl.AddBezier(cx + 12, bot - 40, cx + 12, bot - 24, cx + 6, bot - 16, cx, bot - 16);
                        bowl.AddBezier(cx, bot - 16, cx - 6, bot - 16, cx - 12, bot - 24, cx - 12, bot - 40);
                        bowl.CloseFigure();
                        g.FillPath(gold, bowl);
                    }
                }
                using (var pen = P(C(220, 170, 40), 2.2f))
                {
                    g.DrawArc(pen, cx - 18, bot - 37, 10, 12, 90, 180);
                    g.DrawArc(pen, cx + 8, bot - 37, 10, 12, 270, 180);
                }
                g.FillPolygon(B(C(255, 250, 200)), StarPoints(cx, bot - 30, 5, 2));
                g.FillRectangle(B(CA(150, 255, 255, 230)), cx - 9, bot - 39, 3, 14);
                // old book: brown leather with gold ornament, lying on a small stand
                float bx = 70, by = bot - 22;
                g.FillPolygon(B(C(240, 228, 190)), new[] { new PointF(bx, by + 4), new PointF(bx + 34, by + 4), new PointF(bx + 34, by + 18), new PointF(bx, by + 18) });
                using (var pen = P(C(200, 185, 150), 0.7f))
                    for (int i = 0; i < 4; i++) g.DrawLine(pen, bx + 1, by + 7 + i * 3, bx + 33, by + 7 + i * 3);
                Grad(g, new RectangleF(bx - 2, by - 2, 38, 8), C(140, 75, 40), C(100, 50, 25), 90f, false);
                g.FillRectangle(B(C(100, 50, 25)), bx - 2, by + 18, 38, 4);
                using (var pen = P(C(230, 190, 70), 1))
                {
                    g.DrawRectangle(pen, bx + 1, by - 0.5f, 32, 5);
                    g.DrawLine(pen, bx + 17, by - 0.5f, bx + 17, by + 4.5f);
                }
                g.FillPolygon(B(C(230, 190, 70)), StarPoints(bx + 9, by + 2, 2, 0.9f));
                g.FillPolygon(B(C(230, 190, 70)), StarPoints(bx + 25, by + 2, 2, 0.9f));
                // glass: tint and reflections
                g.FillRectangle(B(CA(35, 210, 235, 255)), inner);
                var st = g.Save();
                g.SetClip(inner);
                using (var b = B(CA(70, 255, 255, 255)))
                {
                    g.FillPolygon(b, new[] { new PointF(inner.X + 14, inner.Y), new PointF(inner.X + 30, inner.Y), new PointF(inner.X - 10, inner.Bottom), new PointF(inner.X - 26, inner.Bottom) });
                    g.FillPolygon(b, new[] { new PointF(inner.X + 40, inner.Y), new PointF(inner.X + 46, inner.Y), new PointF(inner.X + 6, inner.Bottom), new PointF(inner.X, inner.Bottom) });
                    g.FillPolygon(b, new[] { new PointF(inner.Right - 8, inner.Y), new PointF(inner.Right, inner.Y), new PointF(inner.Right - 40, inner.Bottom), new PointF(inner.Right - 48, inner.Bottom) });
                }
                g.Restore(st);
                // frame details: middle bar of the two sliding glass panes, lock and a brass plate
                using (var pen = P(C(105, 65, 35), 2.5f)) g.DrawLine(pen, 64, inner.Y, 64, inner.Bottom);
                using (var pen = P(C(160, 110, 65), 1)) g.DrawRectangle(pen, inner.X - 1, inner.Y - 1, inner.Width + 2, inner.Height + 2);
                FillRoundGrad(g, new RectangleF(44, 110.5f, 40, 6), 2, C(250, 220, 120), C(190, 145, 50), 90f);
            });
        }

        /// <summary>An open double door (green frame) showing the sunny schoolyard, with a "BOISKO"/"PLAYGROUND" sign above.</summary>
        public static Tex YardDoor(FloorTheme th)
        {
            return Wall(th, 71, g =>
            {
                var o = new RectangleF(18, 28, 92, 156);
                var st = g.Save();
                g.SetClip(o);
                // the yard outside
                Grad(g, new RectangleF(o.X, o.Y, o.Width, 90), C(100, 170, 240), C(210, 235, 252), 90f, false);
                g.FillEllipse(B(CA(90, 255, 250, 200)), o.Right - 36, o.Y + 2, 30, 30);
                g.FillEllipse(B(C(255, 235, 110)), o.Right - 29, o.Y + 9, 16, 16);
                KCloud(g, o.X + 4, o.Y + 14, 36, 235);
                KCloud(g, o.X + 50, o.Y + 34, 26, 210);
                KTree(g, o.X + 10, o.Y + 98, 1.1f, 0);
                KTree(g, o.Right - 8, o.Y + 96, 0.9f, 2);
                Grad(g, new RectangleF(o.X, o.Y + 92, o.Width, 70), C(110, 195, 80), C(60, 150, 50), 90f, false);
                using (var pen = P(CA(120, 255, 255, 255), 1.5f)) g.DrawLine(pen, o.X, o.Y + 118, o.Right, o.Y + 124);
                // part of a football goal with a net
                float gx = o.X + 52, gy = o.Y + 62, gb = o.Y + 104;
                using (var pen = P(CA(170, 220, 220, 225), 0.8f))
                {
                    for (float x = gx; x < o.Right + 4; x += 5) g.DrawLine(pen, x, gy, x + 6, gb - 6);
                    for (float y = gy; y < gb; y += 5) g.DrawLine(pen, gx + 3, y, o.Right + 4, y + 2);
                }
                using (var pen = P(Color.White, 3.5f))
                {
                    g.DrawLine(pen, gx, gb, gx, gy);
                    g.DrawLine(pen, gx, gy, o.Right + 6, gy);
                }
                KBall(g, o.X + 34, o.Y + 124, 6);
                g.Restore(st);
                // the two open door leaves (seen at an angle) with glass
                foreach (var left in new[] { true, false })
                {
                    float x0 = left ? o.X : o.Right, x1 = left ? o.X + 13 : o.Right - 13;
                    var leaf = new[] { new PointF(x0, o.Y), new PointF(x1, o.Y + 8), new PointF(x1, o.Bottom - 4), new PointF(x0, o.Bottom) };
                    g.FillPolygon(B(C(45, 145, 75)), leaf);
                    float gx0 = left ? x0 + 3 : x0 - 3, gx1 = left ? x1 - 2 : x1 + 2;
                    g.FillPolygon(B(C(170, 215, 240)), new[] { new PointF(gx0, o.Y + 8), new PointF(gx1, o.Y + 15), new PointF(gx1, o.Y + 92), new PointF(gx0, o.Y + 92) });
                    g.FillPolygon(B(CA(150, 255, 255, 255)), new[] { new PointF(gx0, o.Y + 20), new PointF(gx1, o.Y + 22), new PointF(gx1, o.Y + 30), new PointF(gx0, o.Y + 32) });
                    g.FillRectangle(B(C(220, 220, 225)), Math.Min(x0, x1) + 4, o.Y + 102, 5, 3);
                }
                // green frame and threshold
                using (var pen = new Pen(C(35, 120, 60), 6)) g.DrawRectangle(pen, o.X - 3, o.Y - 3, o.Width + 6, o.Height + 6);
                Grad(g, new RectangleF(o.X - 6, o.Bottom, o.Width + 12, 6), C(190, 190, 195), C(140, 140, 145), 90f, false);
                // sign
                var sign = new RectangleF(20, 3, 88, 20);
                FillRound(g, new RectangleF(sign.X + 2, sign.Y + 2, sign.Width, sign.Height), 5, CA(70, 0, 0, 0));
                FillRoundGrad(g, sign, 5, C(60, 175, 90), C(35, 125, 60), 90f);
                StrokeRound(g, sign, 5, Color.White, 1.5f);
                KBall(g, sign.X + 11, sign.Y + 10, 7);
                TextIn(g, L.Pl ? "BOISKO" : "PLAYGROUND", new RectangleF(sign.X + 20, sign.Y + 1, sign.Width - 22, sign.Height - 2), 15, Color.White);
            });
        }

        // ------------------------------------------------------------------ floors

        /// <summary>
        /// The lobby floor medallion: 9 floor tiles (index = row*3 + col) forming a 384x384 mosaic with the
        /// ring text "SP 2 KÓRNIK" and the castle over the lake waves, on marble matching <see cref="Marble"/>.
        /// </summary>
        public static Tex[] FloorMosaic()
        {
            const int S = 384;
            using (var bmp = Canvas(S, S, g =>
            {
                g.PixelOffsetMode = PixelOffsetMode.Half;   // full-width shapes cover the edge pixels exactly (seamless tiling)
                // marble background: same 64 px checker and grout as Marble()
                var r = new Random(9);
                for (int y = 0; y < 6; y++)
                    for (int x = 0; x < 6; x++)
                    {
                        var c = (x + y) % 2 == 0 ? C(235, 230, 222) : C(205, 200, 195);
                        var rect = new RectangleF(x * 64, y * 64, 64, 64);
                        Grad(g, rect, Shade(c, 10), Shade(c, -10), 30f, false);
                        using (var pen = P(CA(80, 120, 110, 110), 1))
                            for (int v = 0; v < 3; v++)
                                g.DrawBezier(pen, rect.X + F(r) * 64, rect.Y, rect.X + F(r) * 64, rect.Y + 20, rect.X + F(r) * 64, rect.Y + 44, rect.X + F(r) * 64, rect.Bottom);
                    }
                using (var pen = P(CA(120, 90, 85, 80), 1.5f))
                    for (int i = 0; i <= S; i += 64)
                    {
                        g.DrawLine(pen, i, 0, i, S);
                        g.DrawLine(pen, 0, i, S, i);
                    }

                float cx = S / 2f, cy = S / 2f;
                // decorative border: an eight-pointed compass star behind the medallion
                var star = new PointF[16];
                for (int i = 0; i < 16; i++)
                {
                    double a = -Math.PI / 2 + i * Math.PI / 8;
                    float rad = i % 2 == 0 ? (i % 4 == 0 ? 188 : 176) : 150;
                    star[i] = new PointF(cx + (float)Math.Cos(a) * rad, cy + (float)Math.Sin(a) * rad);
                }
                g.FillPolygon(B(C(160, 50, 45)), star);
                using (var pen = P(C(225, 180, 80), 3)) g.DrawPolygon(pen, star);
                for (int i = 0; i < 8; i++)
                {
                    double a = -Math.PI / 2 + i * Math.PI / 4;
                    float rad = i % 2 == 0 ? 180 : 168;
                    using (var pen = P(C(240, 210, 120), 2))
                        g.DrawLine(pen, cx + (float)Math.Cos(a) * 150, cy + (float)Math.Sin(a) * 150, cx + (float)Math.Cos(a) * (rad - 8), cy + (float)Math.Sin(a) * (rad - 8));
                }
                // gold rim, dark ring band for the text
                g.FillEllipse(B(C(215, 170, 70)), cx - 168, cy - 168, 336, 336);
                g.FillEllipse(B(C(30, 55, 110)), cx - 162, cy - 162, 324, 324);
                // small tesserae dots on the band edges
                using (var b = B(C(240, 220, 150)))
                    for (int i = 0; i < 72; i++)
                    {
                        double a = i * Math.PI * 2 / 72;
                        g.FillEllipse(b, cx + (float)Math.Cos(a) * 156 - 2, cy + (float)Math.Sin(a) * 156 - 2, 4, 4);
                        g.FillEllipse(b, cx + (float)Math.Cos(a) * 133 - 1.5f, cy + (float)Math.Sin(a) * 133 - 1.5f, 3, 3);
                    }
                g.FillEllipse(B(C(215, 170, 70)), cx - 128, cy - 128, 256, 256);

                // the scene: sky, castle silhouette, lake waves
                var st = g.Save();
                using (var clip = new GraphicsPath())
                {
                    clip.AddEllipse(cx - 123, cy - 123, 246, 246);
                    g.SetClip(clip);
                    Grad(g, new RectangleF(cx - 124, cy - 124, 248, 140), C(150, 200, 245), C(240, 235, 210), 90f, false);
                    g.FillEllipse(B(C(255, 215, 90)), cx + 40, cy - 92, 40, 40);
                    KCloud(g, cx - 100, cy - 90, 70, 230);
                    KCastle(g, cx, cy + 18, 2.0f, C(40, 55, 105), C(30, 40, 85), C(255, 210, 90));
                    g.FillRectangle(B(C(45, 105, 70)), cx - 124, cy + 12, 248, 8);
                    for (int i = 0; i < 9; i++)
                    {
                        float y = cy + 18 + i * 13;
                        g.FillRectangle(B(i % 2 == 0 ? C(55, 120, 200) : C(45, 100, 180)), cx - 124, y, 248, 14);
                        var pts = new PointF[33];
                        for (int k = 0; k < pts.Length; k++)
                            pts[k] = new PointF(cx - 128 + k * 8, y + 6 + (float)Math.Sin(k * 1.2 + i) * 3);
                        using (var pen = P(CA(200, 170, 215, 250), 2)) g.DrawLines(pen, pts);
                    }
                    // mosaic tesserae grid
                    using (var pen = new Pen(CA(45, 0, 0, 0), 1))
                        for (int k = -126; k <= 126; k += 7)
                        {
                            g.DrawLine(pen, cx + k, cy - 126, cx + k, cy + 126);
                            g.DrawLine(pen, cx - 126, cy + k, cx + 126, cy + k);
                        }
                }
                g.Restore(st);
                using (var pen = P(C(160, 115, 40), 2)) g.DrawEllipse(pen, cx - 124, cy - 124, 248, 248);

                // ring text "SP 2 KÓRNIK" twice, with stars in between
                const string text = "SP 2 KÓRNIK";
                const float step = 8.6f;
                using (var font = new Font(Family, 25, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var gold = B(C(255, 220, 110)))
                    foreach (var centre in new[] { 0f, 180f })
                    {
                        for (int i = 0; i < text.Length; i++)
                        {
                            // the top copy runs clockwise, the bottom one counter-clockwise so both read upright
                            float off = (i - (text.Length - 1) / 2f) * step;
                            float deg = centre == 0 ? off : 180 - off;
                            double a = deg * Math.PI / 180;
                            var s2 = g.Save();
                            g.TranslateTransform(cx + (float)Math.Sin(a) * 145, cy - (float)Math.Cos(a) * 145);
                            g.RotateTransform(centre == 0 ? deg : deg - 180);
                            g.DrawString(text[i].ToString(), font, gold, 0, 1, sf);
                            g.Restore(s2);
                        }
                        double sa = (centre + 90) * Math.PI / 180;
                        g.FillPolygon(gold, StarPoints(cx + (float)Math.Sin(sa) * 145, cy - (float)Math.Cos(sa) * 145, 11, 4.5f));
                    }
                // corner ornaments
                foreach (var c in new[] { new PointF(24, 24), new PointF(S - 24, 24), new PointF(24, S - 24), new PointF(S - 24, S - 24) })
                {
                    g.FillPolygon(B(C(160, 50, 45)), new[] { new PointF(c.X, c.Y - 14), new PointF(c.X + 14, c.Y), new PointF(c.X, c.Y + 14), new PointF(c.X - 14, c.Y) });
                    g.FillEllipse(B(C(225, 180, 80)), c.X - 4, c.Y - 4, 8, 8);
                }
            }))
            {
                var whole = Tex.FromBitmap(bmp, false);
                var res = new Tex[9];
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        var t = new Tex(128, 128);
                        for (int y = 0; y < 128; y++)
                            Array.Copy(whole.Px, (row * 128 + y) * S + col * 128, t.Px, y * 128, 128);
                        res[row * 3 + col] = t;
                    }
                return res;
            }
        }

        /// <summary>A corridor carpet runner tile repeating along X, with the checker floor showing on both sides.</summary>
        public static Tex Runner(FloorTheme th)
        {
            return Opaque(128, 128, g =>
            {
                g.PixelOffsetMode = PixelOffsetMode.Half;   // full-width shapes cover the edge pixels exactly (seamless tiling)
                // checker floor exactly like Checker()
                var r = new Random(7);
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 4; x++)
                    {
                        var c = (x + y) % 2 == 0 ? th.Floor1 : th.Floor2;
                        var rect = new RectangleF(x * 32, y * 32, 32, 32);
                        Grad(g, rect, Shade(c, 8), Shade(c, -8), 45f, false);
                        for (int i = 0; i < 12; i++)
                            using (var b = B(CA(40, Shade(c, r.Next(-30, 30))))) g.FillEllipse(b, rect.X + F(r) * 28, rect.Y + F(r) * 28, 4, 3);
                    }
                using (var pen = P(CA(90, 0, 0, 0), 1))
                    for (int i = 0; i <= 128; i += 32)
                    {
                        g.DrawLine(pen, i, 0, i, 128);
                        g.DrawLine(pen, 0, i, 128, i);
                    }
                // the carpet strip (y 19..109)
                float top = 19, bot = 109;
                g.FillRectangle(B(CA(70, 0, 0, 0)), 0, bot, 128, 3);
                Grad(g, new RectangleF(0, top, 128, bot - top), Shade(th.Accent, -10), Shade(th.Accent, -45), 90f, false);
                g.FillRectangle(B(Shade(th.Accent, -55)), 0, top, 128, 7);
                g.FillRectangle(B(Shade(th.Accent, -55)), 0, bot - 7, 128, 7);
                using (var gold = B(C(235, 195, 80)))
                {
                    g.FillRectangle(gold, 0, top + 7, 128, 3);
                    g.FillRectangle(gold, 0, bot - 10, 128, 3);
                    // small gold diamonds along the borders (period 16 so the tile repeats)
                    for (int x = 0; x < 128; x += 16)
                    {
                        g.FillPolygon(gold, new[] { new PointF(x + 8, top + 1), new PointF(x + 11, top + 3.5f), new PointF(x + 8, top + 6), new PointF(x + 5, top + 3.5f) });
                        g.FillPolygon(gold, new[] { new PointF(x + 8, bot - 6), new PointF(x + 11, bot - 3.5f), new PointF(x + 8, bot - 1), new PointF(x + 5, bot - 3.5f) });
                    }
                }
                // fringe
                using (var pen = P(C(240, 225, 190), 1))
                    for (int x = 1; x < 128; x += 3)
                    {
                        g.DrawLine(pen, x, top - 3, x, top);
                        g.DrawLine(pen, x, bot, x, bot + 3);
                    }
                // centre medallion with a white swan
                float cx = 64, cy = (top + bot) / 2;
                g.FillPolygon(B(Shade(th.Accent, 15)), new[] { new PointF(cx, cy - 28), new PointF(cx + 34, cy), new PointF(cx, cy + 28), new PointF(cx - 34, cy) });
                using (var pen = P(C(235, 195, 80), 2))
                    g.DrawPolygon(pen, new[] { new PointF(cx, cy - 28), new PointF(cx + 34, cy), new PointF(cx, cy + 28), new PointF(cx - 34, cy) });
                using (var pen = P(CA(200, 170, 215, 250), 1.5f))
                {
                    g.DrawArc(pen, cx - 16, cy + 8, 10, 4, 0, 180);
                    g.DrawArc(pen, cx - 6, cy + 8, 10, 4, 180, 180);
                    g.DrawArc(pen, cx + 4, cy + 8, 10, 4, 0, 180);
                }
                KSwan(g, cx - 1, cy + 8, 1.15f, false);
            });
        }

        // ------------------------------------------------------------------ schoolyard (outdoor) textures

        /// <summary>Lush tileable grass (128x128).</summary>
        public static Tex GrassTex()
        {
            return Opaque(128, 128, g =>
            {
                g.PixelOffsetMode = PixelOffsetMode.Half;   // full-width shapes cover the edge pixels exactly (seamless tiling)
                g.FillRectangle(B(C(85, 160, 60)), 0, 0, 128, 128);
                var r = new Random(81);
                for (int i = 0; i < 70; i++)
                {
                    float x = F(r) * 128, y = F(r) * 128, s = 8 + F(r) * 16;
                    var c = r.Next(2) == 0 ? CA(60, 120, 195, 80) : CA(60, 50, 120, 40);
                    KWrap(128, x, y, (px, py) => { using (var b = B(c)) g.FillEllipse(b, px - s / 2, py - s / 2, s, s * 0.7f); });
                }
                var greens = new[] { C(60, 135, 45), C(95, 175, 65), C(120, 195, 80), C(70, 150, 50), C(140, 210, 95) };
                for (int i = 0; i < 1100; i++)
                {
                    float x = F(r) * 128, y = F(r) * 128, len = 4 + F(r) * 5, lean = (F(r) - 0.5f) * 4;
                    var c = greens[r.Next(greens.Length)];
                    KWrap(128, x, y, (px, py) =>
                    {
                        if (px < -10 || px > 138 || py < -10 || py > 138) return;
                        using (var pen = P(c, 1.1f)) g.DrawLine(pen, px, py, px + lean, py - len);
                    });
                }
                // a few tiny daisies
                for (int i = 0; i < 5; i++)
                {
                    float x = F(r) * 128, y = F(r) * 128;
                    KWrap(128, x, y, (px, py) =>
                    {
                        if (px < -6 || px > 134 || py < -6 || py > 134) return;
                        for (int k = 0; k < 5; k++)
                        {
                            double a = k * Math.PI * 2 / 5;
                            g.FillEllipse(Brushes.White, px + (float)Math.Cos(a) * 1.8f - 1.2f, py + (float)Math.Sin(a) * 1.8f - 1.2f, 2.4f, 2.4f);
                        }
                        g.FillEllipse(B(C(250, 210, 50)), px - 1, py - 1, 2, 2);
                    });
                }
            });
        }

        /// <summary>Tileable paving stones (128x128).</summary>
        public static Tex StonePathTex()
        {
            return Opaque(128, 128, g =>
            {
                g.PixelOffsetMode = PixelOffsetMode.Half;   // full-width shapes cover the edge pixels exactly (seamless tiling)
                g.FillRectangle(B(C(150, 140, 120)), 0, 0, 128, 128);
                var r = new Random(82);
                for (int i = 0; i < 300; i++) g.FillRectangle(B(CA(90, r.Next(2) == 0 ? C(120, 110, 95) : C(180, 170, 150))), F(r) * 128, F(r) * 128, 1.5f, 1.5f);
                var stones = new[] { C(190, 185, 175), C(175, 165, 150), C(200, 190, 170), C(165, 160, 155), C(185, 175, 160) };
                for (int row = 0; row < 4; row++)
                    for (int col = 0; col < 4; col++)
                    {
                        float x = col * 32 + (row % 2) * 16 + 16 + (F(r) - 0.5f) * 3, y = row * 32 + 16 + (F(r) - 0.5f) * 3;
                        var c = stones[r.Next(stones.Length)];
                        var pts = new PointF[7];
                        for (int k = 0; k < 7; k++)
                        {
                            double a = k * Math.PI * 2 / 7 + F(r) * 0.3;
                            float rad = 13 + F(r) * 2.5f;
                            pts[k] = new PointF((float)Math.Cos(a) * rad, (float)Math.Sin(a) * rad);
                        }
                        KWrap(128, x, y, (px, py) =>
                        {
                            if (px < -20 || px > 148 || py < -20 || py > 148) return;
                            var sh = new PointF[7];
                            var p2 = new PointF[7];
                            for (int k = 0; k < 7; k++)
                            {
                                sh[k] = new PointF(px + pts[k].X + 1.5f, py + pts[k].Y + 2);
                                p2[k] = new PointF(px + pts[k].X, py + pts[k].Y);
                            }
                            using (var path = new GraphicsPath())
                            {
                                path.AddClosedCurve(sh, 0.4f);
                                using (var b = B(CA(110, 60, 50, 40))) g.FillPath(b, path);
                            }
                            using (var path = new GraphicsPath())
                            {
                                path.AddClosedCurve(p2, 0.4f);
                                using (var br = new LinearGradientBrush(new RectangleF(px - 16, py - 16, 32, 32), Shade(c, 18), Shade(c, -18), 45f)) g.FillPath(br, path);
                                using (var pen = P(Shade(c, -45), 1)) g.DrawPath(pen, path);
                            }
                            g.FillEllipse(B(CA(70, 255, 255, 255)), px - 8, py - 9, 9, 5);
                        });
                    }
            });
        }

        /// <summary>Tileable pond water with ripples (128x128).</summary>
        public static Tex PondWater()
        {
            return Opaque(128, 128, g =>
            {
                g.PixelOffsetMode = PixelOffsetMode.Half;   // full-width shapes cover the edge pixels exactly (seamless tiling)
                g.FillRectangle(B(C(55, 125, 175)), 0, 0, 128, 128);
                var r = new Random(83);
                for (int i = 0; i < 40; i++)
                {
                    float x = F(r) * 128, y = F(r) * 128, s = 16 + F(r) * 30;
                    var c = r.Next(2) == 0 ? CA(50, 90, 170, 210) : CA(50, 30, 85, 130);
                    KWrap(128, x, y, (px, py) => { using (var b = B(c)) g.FillEllipse(b, px - s / 2, py - s / 4, s, s / 2); });
                }
                for (int i = 0; i < 26; i++)
                {
                    float x = F(r) * 128, y = F(r) * 128, w = 10 + F(r) * 18;
                    int a = 90 + r.Next(90);
                    KWrap(128, x, y, (px, py) =>
                    {
                        if (px < -30 || px > 158 || py < -10 || py > 138) return;
                        using (var pen = P(CA(a, 200, 235, 255), 1.3f)) g.DrawArc(pen, px - w / 2, py - 2, w, 4, 200, 140);
                    });
                }
                // concentric ripple rings
                for (int i = 0; i < 2; i++)
                {
                    float x = 30 + i * 64, y = 40 + i * 50;
                    KWrap(128, x, y, (px, py) =>
                    {
                        for (int k = 1; k <= 3; k++)
                            using (var pen = P(CA(120 - k * 30, 220, 245, 255), 1.2f)) g.DrawEllipse(pen, px - k * 7, py - k * 3, k * 14, k * 6);
                    });
                }
                for (int i = 0; i < 14; i++)
                {
                    float x = F(r) * 128, y = F(r) * 128;
                    KWrap(128, x, y, (px, py) => g.FillEllipse(B(CA(200, 255, 255, 255)), px - 1, py - 0.6f, 2.5f, 1.2f));
                }
            });
        }

        /// <summary>A green metal school fence (lower 2/3 of a 128x192 wall); everything else is transparent.</summary>
        public static Tex FenceTex()
        {
            using (var bmp = Canvas(TW, TH, g =>
            {
                g.PixelOffsetMode = PixelOffsetMode.Half;   // full-width shapes cover the edge pixels exactly (seamless tiling)
                var dark = C(30, 95, 50);
                var light = C(70, 150, 85);
                // vertical bars with spear tips, every 16 px (repeats along X)
                for (int x = 8; x < 128; x += 16)
                {
                    Grad(g, new RectangleF(x - 2, 70, 4, 116), light, dark, 0f, false);
                    g.FillPolygon(B(dark), new[] { new PointF(x - 3.5f, 71), new PointF(x + 3.5f, 71), new PointF(x, 61) });
                    g.FillPolygon(B(light), new[] { new PointF(x - 1.5f, 70), new PointF(x + 1, 70), new PointF(x - 0.5f, 64) });
                }
                // rails
                foreach (var y in new[] { 76f, 170f })
                {
                    Grad(g, new RectangleF(0, y, 128, 7), light, dark, 90f, false);
                    using (var pen = P(C(110, 185, 120), 1)) g.DrawLine(pen, 0, y + 1, 128, y + 1);
                }
                // concrete foot
                Grad(g, new RectangleF(0, 183, 128, 9), C(175, 170, 160), C(130, 125, 118), 90f, false);
                // a little ivy climbing on some bars
                var r = new Random(84);
                foreach (var bx in new[] { 24f, 88f, 104f })
                {
                    float top = 100 + F(r) * 50;
                    using (var pen = P(C(70, 100, 40), 1.2f)) g.DrawBezier(pen, bx, 183, bx + 4, 160, bx - 4, 130, bx + 1, top);
                    for (int k = 0; k < 7; k++)
                    {
                        float ly = 180 - k * (180 - top) / 7f, lx = bx + (k % 2 == 0 ? -4 : 4) + (F(r) - 0.5f) * 2;
                        var c = k % 3 == 0 ? C(60, 145, 50) : C(90, 170, 60);
                        using (var b = B(c))
                        {
                            g.FillEllipse(b, lx - 3.5f, ly - 3, 7, 6);
                            g.FillEllipse(b, lx - 1.5f, ly - 5, 3, 3);
                        }
                    }
                }
            }))
                return Tex.FromBitmap(bmp, true);
        }

        /// <summary>Common part of the school facade (plaster, upper windows, cornices, plinth).</summary>
        static void KFacadeBase(Graphics g, bool door)
        {
            g.PixelOffsetMode = PixelOffsetMode.Half;   // full-width shapes cover the edge pixels exactly (seamless tiling)
            var plaster = C(243, 222, 160);
            Grad(g, new RectangleF(0, 0, 128, 384), Shade(plaster, 8), Shade(plaster, -8), 90f, false);
            var r = new Random(85);
            for (int i = 0; i < 900; i++)
                using (var b = B(CA(60, Shade(plaster, r.Next(-22, 18))))) g.FillRectangle(b, F(r) * 128, F(r) * 384, 2, 2);
            // top cornice
            g.FillRectangle(B(C(250, 244, 225)), 0, 0, 128, 10);
            Grad(g, new RectangleF(0, 10, 128, 6), C(200, 185, 150), C(240, 230, 205), 90f, false);
            using (var b = B(C(250, 244, 225)))
                for (int x = 2; x < 128; x += 8) g.FillRectangle(b, x, 16, 4, 4);
            g.FillRectangle(B(CA(60, 0, 0, 0)), 0, 20, 128, 2);
            // upper window
            KFacadeWindow(g, new RectangleF(32, 52, 64, 104));
            // middle band between the floors
            Grad(g, new RectangleF(0, 178, 128, 12), C(252, 246, 228), C(205, 190, 155), 90f, false);
            g.FillRectangle(B(CA(60, 0, 0, 0)), 0, 190, 128, 2);
            if (!door) KFacadeWindow(g, new RectangleF(32, 216, 64, 104));
            // stone plinth
            Grad(g, new RectangleF(0, 344, 128, 40), C(165, 160, 150), C(125, 120, 112), 90f, false);
            using (var pen = P(C(105, 100, 95), 1))
            {
                g.DrawLine(pen, 0, 344, 128, 344);
                g.DrawLine(pen, 0, 364, 128, 364);
                for (int x = 0; x < 128; x += 32)
                {
                    g.DrawLine(pen, x, 344, x, 364);
                    g.DrawLine(pen, x + 16, 364, x + 16, 384);
                }
            }
        }

        /// <summary>A facade window: white frame, sky reflection, cross bars, sill and keystone.</summary>
        static void KFacadeWindow(Graphics g, RectangleF w)
        {
            g.FillRectangle(B(C(250, 244, 225)), w.X - 7, w.Y - 7, w.Width + 14, w.Height + 14);
            g.FillPolygon(B(C(235, 225, 195)), new[] { new PointF(w.X + w.Width / 2 - 7, w.Y - 12), new PointF(w.X + w.Width / 2 + 7, w.Y - 12), new PointF(w.X + w.Width / 2 + 5, w.Y - 2), new PointF(w.X + w.Width / 2 - 5, w.Y - 2) });
            Grad(g, w, C(110, 165, 225), C(205, 232, 250), 90f, false);
            var st = g.Save();
            g.SetClip(w);
            KCloud(g, w.X + 4, w.Y + 20, 30, 170);
            using (var b = B(CA(80, 255, 255, 255)))
            {
                g.FillPolygon(b, new[] { new PointF(w.X + 10, w.Bottom), new PointF(w.X + 26, w.Bottom), new PointF(w.Right, w.Y + 30), new PointF(w.Right, w.Y + 14) });
                g.FillPolygon(b, new[] { new PointF(w.X, w.Y + 56), new PointF(w.X + 34, w.Y), new PointF(w.X + 40, w.Y), new PointF(w.X, w.Y + 66) });
            }
            g.Restore(st);
            using (var pen = new Pen(Color.White, 5)) g.DrawRectangle(pen, w.X, w.Y, w.Width, w.Height);
            using (var pen = new Pen(Color.White, 4))
            {
                g.DrawLine(pen, w.X + w.Width / 2, w.Y, w.X + w.Width / 2, w.Bottom);
                g.DrawLine(pen, w.X, w.Y + w.Height / 3, w.Right, w.Y + w.Height / 3);
            }
            Grad(g, new RectangleF(w.X - 9, w.Bottom + 6, w.Width + 18, 7), C(250, 248, 240), C(180, 175, 165), 90f, false);
            g.FillRectangle(B(CA(60, 0, 0, 0)), w.X - 7, w.Bottom + 13, w.Width + 14, 3);
        }

        /// <summary>Opaque school facade for tall outdoor walls (128x384): plaster, two windows, cornices, plinth.</summary>
        public static Tex Facade() => Opaque(128, 384, g => KFacadeBase(g, false));

        /// <summary>The school facade with the main entrance (glass double door, steps) and a "SZKOŁA"/"SCHOOL" sign.</summary>
        public static Tex SchoolDoor()
        {
            return Opaque(128, 384, g =>
            {
                KFacadeBase(g, true);
                // sign
                var sign = new RectangleF(18, 196, 92, 34);
                FillRound(g, new RectangleF(sign.X + 2, sign.Y + 2, sign.Width, sign.Height), 5, CA(70, 0, 0, 0));
                FillRoundGrad(g, sign, 5, C(50, 110, 190), C(30, 75, 145), 90f);
                StrokeRound(g, sign, 5, C(255, 215, 90), 2);
                TextIn(g, L.Pl ? "SZKOŁA" : "SCHOOL", new RectangleF(sign.X + 4, sign.Y + 2, sign.Width - 8, 19), 18, Color.White);
                TextIn(g, "SP 2 Kórnik", new RectangleF(sign.X + 4, sign.Y + 20, sign.Width - 8, 12), 11, C(255, 225, 120));
                // stone door surround
                var surround = new RectangleF(16, 236, 96, 112);
                g.FillRectangle(B(C(225, 218, 200)), surround);
                using (var pen = P(C(190, 180, 160), 1))
                    for (float y = surround.Y + 12; y < surround.Bottom; y += 14)
                    {
                        g.DrawLine(pen, surround.X, y, surround.X + 8, y);
                        g.DrawLine(pen, surround.Right - 8, y, surround.Right, y);
                    }
                // glass double door
                var door = new RectangleF(24, 244, 80, 104);
                g.FillRectangle(B(C(95, 70, 50)), door);
                for (int i = 0; i < 2; i++)
                {
                    var leaf = new RectangleF(door.X + 3 + i * 38, door.Y + 3, 36, door.Height - 3);
                    g.FillRectangle(B(C(130, 95, 65)), leaf);
                    var glass = new RectangleF(leaf.X + 4, leaf.Y + 4, leaf.Width - 8, leaf.Height - 22);
                    Grad(g, glass, C(140, 190, 230), C(215, 238, 252), 90f, false);
                    g.FillPolygon(B(CA(90, 255, 255, 255)), new[] { new PointF(glass.X, glass.Y + 30), new PointF(glass.X + 18, glass.Y), new PointF(glass.X + 24, glass.Y), new PointF(glass.X, glass.Y + 40) });
                    g.FillRectangle(B(C(200, 200, 205)), i == 0 ? leaf.Right - 8 : leaf.X + 4, leaf.Y + 46, 4, 18);
                }
                // fanlight
                Grad(g, new RectangleF(door.X + 3, door.Y - 6, door.Width - 6, 4), C(255, 230, 140), C(220, 180, 80), 90f, false);
                // steps
                for (int i = 0; i < 3; i++)
                {
                    float inset = 10 - i * 5, y = 348 + i * 12;
                    Grad(g, new RectangleF(inset, y, 128 - inset * 2, 12), C(205, 200, 192), C(150, 145, 138), 90f, false);
                    using (var pen = P(C(120, 115, 108), 1)) g.DrawLine(pen, inset, y, 128 - inset, y);
                }
            });
        }

        static Tex cachedSky, cachedShadow, cachedDrop;

        /// <summary>Sky panorama 1024x256 wrapping horizontally (row 0 = zenith, last row = horizon), with soft clouds.</summary>
        public static Tex SkyDome()
        {
            if (cachedSky != null) return cachedSky;
            const int W = 1024, H = 256;
            cachedSky = Opaque(W, H, g =>
            {
                g.PixelOffsetMode = PixelOffsetMode.Half;   // full-width shapes cover the edge pixels exactly (seamless tiling)
                using (var br = new LinearGradientBrush(new RectangleF(0, -1, W, H + 2), C(55, 115, 210), C(215, 232, 248), 90f))
                {
                    br.InterpolationColors = new ColorBlend
                    {
                        Colors = new[] { C(55, 115, 210), C(95, 160, 235), C(170, 210, 245), C(228, 238, 248) },
                        Positions = new[] { 0f, 0.45f, 0.82f, 1f }
                    };
                    g.FillRectangle(br, 0, 0, W, H);
                }
                var r = new Random(86);
                for (int i = 0; i < 22; i++)
                {
                    float x = F(r) * W, y = 50 + F(r) * 150;
                    float w = 50 + y * 0.6f + F(r) * 60;          // bigger clouds higher up, flatter near the horizon
                    float squash = 0.45f + (1 - y / H) * 0.5f;
                    int puffs = 4 + r.Next(4);
                    var parts = new RectangleF[puffs];
                    for (int k = 0; k < puffs; k++)
                    {
                        float pw = w * (0.35f + F(r) * 0.3f);
                        parts[k] = new RectangleF(k * w / puffs - pw * 0.2f, -F(r) * pw * 0.35f * squash, pw, pw * 0.55f * squash);
                    }
                    foreach (var dx in new[] { -W, 0, W })
                    {
                        float ox = x + dx;
                        if (ox + w * 1.3f < 0 || ox - w * 0.3f > W) continue;
                        using (var shade = B(CA(90, 165, 185, 215)))
                            foreach (var pr in parts) g.FillEllipse(shade, ox + pr.X, y + pr.Y + 3, pr.Width, pr.Height);
                        using (var white = B(CA(255, 255, 255, 255)))
                            foreach (var pr in parts) g.FillEllipse(white, ox + pr.X, y + pr.Y, pr.Width, pr.Height * 0.85f);
                    }
                }
                // haze over the horizon
                Grad(g, new RectangleF(0, H - 40, W, 40), CA(0, 235, 240, 248), CA(200, 235, 240, 248), 90f, false);
            });
            return cachedSky;
        }

        /// <summary>A soft round blob shadow (64x64) with smooth alpha (not thresholded).</summary>
        public static Tex BlobShadow()
        {
            if (cachedShadow != null) return cachedShadow;
            var t = new Tex(64, 64);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    double dx = (x + 0.5 - 32) / 32, dy = (y + 0.5 - 32) / 32;
                    double d = 1 - (dx * dx + dy * dy);
                    int a = d <= 0 ? 0 : (int)(150 * Math.Pow(d, 1.5));
                    t.Px[y * 64 + x] = a << 24;
                }
            cachedShadow = t;
            return t;
        }

        /// <summary>A small light blue water drop sprite (16x16).</summary>
        public static Tex WaterDrop()
        {
            if (cachedDrop != null) return cachedDrop;
            using (var bmp = Canvas(16, 16, g =>
            {
                using (var path = new GraphicsPath())
                {
                    path.AddBezier(8, 1, 9, 4, 13.5f, 7, 13.5f, 10);
                    path.AddBezier(13.5f, 10, 13.5f, 13.5f, 10.5f, 15, 8, 15);
                    path.AddBezier(8, 15, 5.5f, 15, 2.5f, 13.5f, 2.5f, 10);
                    path.AddBezier(2.5f, 10, 2.5f, 7, 7, 4, 8, 1);
                    path.CloseFigure();
                    using (var br = new LinearGradientBrush(new RectangleF(2, 1, 12, 15), C(170, 225, 255), C(60, 140, 220), 60f)) g.FillPath(br, path);
                    using (var pen = P(C(40, 110, 190), 1)) g.DrawPath(pen, path);
                }
                g.FillEllipse(Brushes.White, 4.8f, 8.5f, 2.6f, 3.6f);
            }))
                cachedDrop = Tex.FromBitmap(bmp, true);
            return cachedDrop;
        }
    }
}
