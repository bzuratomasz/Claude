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
    /// painting captions, menus and floor screens. Also remembers clickable areas for the mouse.
    /// </summary>
    sealed class Hud : IDisposable
    {
        readonly Game game;
        readonly FontFamily family;
        readonly ImageAttributes faded = new ImageAttributes();
        readonly List<KeyValuePair<RectangleF, int>> hits = new List<KeyValuePair<RectangleF, int>>();
        float u;
        int W, H;

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
        }

        public void Dispose() => faded.Dispose();

        /// <summary>Action of the clickable area under the mouse, or -1.</summary>
        public int HitTest(Point p)
        {
            for (int i = hits.Count - 1; i >= 0; i--)
                if (hits[i].Key.Contains(p)) return hits[i].Value;
            return -1;
        }

        void Hit(RectangleF r, int action) => hits.Add(new KeyValuePair<RectangleF, int>(r, action));

        public void Draw(Graphics g, Rectangle rc, Renderer r)
        {
            W = rc.Width;
            H = rc.Height;
            u = Math.Min(H / 720f, W / 1100f);
            hits.Clear();
            if (r != null) r.Blit(g, rc);     // software renderer; with OpenGL the HUD is drawn on a transparent overlay
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

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
                    Text(g, L.T("paused"), 70, W / 2f, 120 * u, Color.White);
                    MenuList(g, game.PauseItems, 260 * u);
                    break;
                case GameScreen.FloorDone: FloorDone(g); break;
                case GameScreen.Victory: Victory(g); break;
            }
        }

        // ------------------------------------------------------------------ drawing helpers

        void Text(Graphics g, string s, float size, float x, float y, Color fill, StringAlignment align = StringAlignment.Center, int alpha = 255)
        {
            if (string.IsNullOrEmpty(s)) return;
            using (var path = new GraphicsPath())
            using (var sf = new StringFormat { Alignment = align, LineAlignment = StringAlignment.Near })
            {
                path.AddString(s, family, (int)FontStyle.Bold, size * u, new PointF(x, y), sf);
                Outline(g, path, size, fill, alpha);
            }
        }

        /// <summary>Wrapped text inside a box (for questions and captions).</summary>
        void TextBox(Graphics g, string s, float size, RectangleF box, Color fill, StringAlignment align = StringAlignment.Center)
        {
            if (string.IsNullOrEmpty(s)) return;
            for (; size > 10; size -= 2)
            {
                using (var f = new Font(family, size * u, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var sf = new StringFormat { Alignment = align })
                {
                    var m = g.MeasureString(s, f, (int)box.Width, sf);
                    if (m.Height <= box.Height) break;
                }
            }
            using (var path = new GraphicsPath())
            using (var sf = new StringFormat { Alignment = align, LineAlignment = StringAlignment.Center })
            {
                path.AddString(s, family, (int)FontStyle.Bold, size * u, box, sf);
                Outline(g, path, size, fill, 255);
            }
        }

        void Outline(Graphics g, GraphicsPath path, float size, Color fill, int alpha)
        {
            using (var pen = new Pen(Color.FromArgb(alpha, 40, 28, 20), Math.Max(2f, size * u * 0.15f)) { LineJoin = LineJoin.Round })
                g.DrawPath(pen, path);
            using (var b = new SolidBrush(Color.FromArgb(alpha, fill)))
                g.FillPath(b, path);
        }

        SizeF Measure(Graphics g, string s, float size)
        {
            using (var f = new Font(family, size * u, FontStyle.Bold, GraphicsUnit.Pixel))
                return g.MeasureString(s, f);
        }

        static void Fill(Graphics g, Color c)
        {
            using (var b = new SolidBrush(c)) g.FillRectangle(b, g.VisibleClipBounds);
        }

        static void Dim(Graphics g, int alpha) => Fill(g, Color.FromArgb(alpha, 10, 15, 25));

        static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        void Panel(Graphics g, RectangleF r, Color fill, Color? border = null)
        {
            using (var p = RoundRect(r, 22 * u))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, p);
                using (var pen = new Pen(border ?? Color.FromArgb(230, 255, 215, 90), 5 * u)) g.DrawPath(pen, p);
            }
        }

        void StarShape(Graphics g, float cx, float cy, float r, bool filled)
        {
            var pts = Art.StarPoints(cx, cy, r, r * 0.45f);
            using (var b = new SolidBrush(filled ? Color.FromArgb(255, 220, 40) : Color.FromArgb(140, 60, 60, 70))) g.FillPolygon(b, pts);
            using (var pen = new Pen(filled ? Color.FromArgb(200, 120, 0) : Color.FromArgb(180, 30, 30, 40), 3 * u)) g.DrawPolygon(pen, pts);
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

        void MenuList(Graphics g, string[] items, float top)
        {
            float step = 56 * u;
            for (int i = 0; i < items.Length; i++)
            {
                bool sel = i == game.Menu;
                float y = top + i * step;
                var sz = Measure(g, items[i], 38);
                float half = Math.Max(sz.Width / 2 + 40 * u, 160 * u);
                var rect = new RectangleF(W / 2f - half, y - 4 * u, half * 2, 50 * u);
                Hit(rect, 100 + i);
                if (sel)
                {
                    float bounce = (float)Math.Sin(game.Time * 6) * 6 * u;
                    using (var p = RoundRect(rect, 24 * u))
                    using (var b = new SolidBrush(Color.FromArgb(170, 40, 120, 60)))
                        g.FillPath(b, p);
                    Arrow(g, rect.X + 18 * u + bounce, y + 21 * u, 11 * u, false);
                    Arrow(g, rect.Right - 18 * u - bounce, y + 21 * u, 11 * u, true);
                }
                Text(g, items[i], sel ? 38 : 33, W / 2f, y + (sel ? 0 : 3 * u), sel ? Color.FromArgb(255, 235, 80) : Color.White);
            }
        }

        static void Arrow(Graphics g, float x, float y, float s, bool pointLeft)
        {
            float d = pointLeft ? -1 : 1;
            var pts = new[] { new PointF(x - s * d, y - s), new PointF(x + s * d, y), new PointF(x - s * d, y + s) };
            using (var b = new SolidBrush(Color.FromArgb(255, 235, 80))) g.FillPolygon(b, pts);
        }

        // ------------------------------------------------------------------ menus

        void TitleScreen(Graphics g)
        {
            using (var br = new LinearGradientBrush(new Rectangle(0, 0, W, H), Color.FromArgb(170, 10, 30, 20), Color.FromArgb(60, 0, 0, 0), 90f))
                g.FillRectangle(br, 0, 0, W, H);
            float bob = (float)Math.Sin(game.Time * 2) * 6 * u;
            g.DrawImage(game.Art.TurtleIcon, W / 2f - 330 * u, 20 * u + bob, 110 * u, 105 * u);
            using (var path = new GraphicsPath())
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            {
                path.AddString(L.T("title"), family, (int)FontStyle.Bold, 92 * u, new PointF(W / 2f + 40 * u, 10 * u + bob), sf);
                using (var pen = new Pen(Color.FromArgb(40, 70, 20), 12 * u) { LineJoin = LineJoin.Round }) g.DrawPath(pen, path);
                using (var br = new LinearGradientBrush(new RectangleF(0, 10 * u + bob, W, 110 * u), Color.FromArgb(255, 245, 120), Color.FromArgb(120, 220, 70), 90f))
                    g.FillPath(br, path);
            }
            Text(g, L.T("subtitle"), 34, W / 2f, 122 * u, Color.FromArgb(255, 255, 220));
            Text(g, L.T("school_name"), 22, W / 2f, 172 * u, Color.FromArgb(200, 235, 255));
            Text(g, L.T("school_patron"), 18, W / 2f, 202 * u, Color.FromArgb(200, 235, 255));
            MenuList(g, game.TitleItems, 252 * u);
            int coins = game.Save.Coins;
            g.DrawImage(game.Art.CoinIcon, 16 * u, H - 56 * u, 40 * u, 40 * u);
            Text(g, "x " + coins, 26, 62 * u, H - 54 * u, Color.FromArgb(255, 230, 90), StringAlignment.Near);
            Text(g, "v4.0", 18, W - 20 * u, H - 40 * u, Color.FromArgb(220, 255, 255, 255), StringAlignment.Far);
        }

        void HelpScreen(Graphics g)
        {
            Dim(g, 120);
            var panel = new RectangleF(W / 2f - 540 * u, 30 * u, 1080 * u, 660 * u);
            Panel(g, panel, Color.FromArgb(220, 25, 60, 35));
            Hit(panel, 1);
            Text(g, L.T("help"), 52, W / 2f, 44 * u, Color.FromArgb(255, 235, 80));
            TextBox(g, L.T("help_goal"), 24, new RectangleF(panel.X + 30 * u, 116 * u, panel.Width - 60 * u, 60 * u), Color.White);
            float y = 190 * u;
            for (int i = 1; i <= 8; i++)
            {
                Text(g, L.T("help" + i), 25, W / 2f - 20 * u, y, Color.FromArgb(255, 235, 80), StringAlignment.Far);
                Text(g, L.T("help" + i + "b"), 25, W / 2f + 10 * u, y, Color.White, StringAlignment.Near);
                y += 44 * u;
            }
            TextBox(g, L.T("help_tip"), 21, new RectangleF(panel.X + 30 * u, y + 4 * u, panel.Width - 60 * u, 56 * u), Color.FromArgb(200, 255, 200));
            Text(g, L.T("back"), 21, W / 2f, panel.Bottom - 44 * u, Color.FromArgb(220, 220, 220));
        }

        void FloorSelect(Graphics g)
        {
            Dim(g, 120);
            Text(g, L.T("floors"), 60, W / 2f, 50 * u, Color.FromArgb(255, 235, 80));
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
                var th = School.Themes[i];
                using (var p = RoundRect(rect, 20 * u))
                {
                    using (var br = new LinearGradientBrush(rect, th.Paint, th.Accent, 90f)) g.FillPath(br, p);
                    using (var pen = new Pen(sel ? Color.FromArgb(255, 235, 80) : Color.FromArgb(200, 255, 255, 255), (sel ? 7 : 3) * u)) g.DrawPath(pen, p);
                }
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
                    using (var p = RoundRect(rect, 20 * u))
                    using (var b = new SolidBrush(Color.FromArgb(170, 20, 20, 30)))
                        g.FillPath(b, p);
                    Padlock(g, rect.X + cw / 2, rect.Y + ch / 2, 36 * u);
                }
            }
            float bottom = y0 + 2 * ch + gap;
            Text(g, L.T("select_hint"), 24, W / 2f, bottom + 14 * u, Color.White);
            Text(g, L.T("back"), 20, W / 2f, bottom + 52 * u, Color.FromArgb(220, 220, 220));
        }

        static void Padlock(Graphics g, float cx, float cy, float s)
        {
            using (var pen = new Pen(Color.FromArgb(220, 220, 220), s * 0.25f))
                g.DrawArc(pen, cx - s * 0.6f, cy - s * 1.3f, s * 1.2f, s * 1.4f, 180, 180);
            using (var b = new SolidBrush(Color.FromArgb(255, 200, 60)))
                g.FillRectangle(b, cx - s, cy - s * 0.6f, s * 2, s * 1.6f);
            g.FillEllipse(Brushes.Black, cx - s * 0.15f, cy - s * 0.1f, s * 0.3f, s * 0.4f);
        }

        // ------------------------------------------------------------------ in-game HUD

        void PlayHud(Graphics g)
        {
            var w = game.World;

            // floor badge and coins
            var badge = new RectangleF(14 * u, 12 * u, 330 * u, 86 * u);
            Panel(g, badge, Color.FromArgb(170, 20, 30, 25), Color.FromArgb(200, game.Theme.Accent));
            Text(g, game.InYard ? L.T("yard") : L.T("floor") + " " + (game.FloorIndex + 1), 26, badge.X + 16 * u, badge.Y + 6 * u, Color.FromArgb(255, 235, 80), StringAlignment.Near);
            TextBox(g, game.InYard ? L.T("school_name") : game.Theme.Name, 17, new RectangleF(badge.X + 14 * u, badge.Y + 40 * u, 210 * u, 40 * u), Color.White, StringAlignment.Near);
            g.DrawImage(game.Art.CoinIcon, badge.Right - 106 * u, badge.Y + 12 * u, 34 * u, 34 * u);
            Text(g, game.Save.Coins.ToString(), 24, badge.Right - 68 * u, badge.Y + 12 * u, Color.FromArgb(255, 230, 90), StringAlignment.Near);

            if (game.InYard) Scoreboard(g);
            else GiftBar(g);
            Compass(g, W / 2f, 128 * u, 30 * u);

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
                    Text(g, L.T("kick"), 26, W / 2f, H - 90 * u, Color.White);
            }

            if (game.MessageTime > 0 && game.Message != null)
            {
                int a = (int)(Math.Min(1, game.MessageTime) * 255);
                var box = new RectangleF(W / 2f - 420 * u, 170 * u, 840 * u, 80 * u);
                using (var p = RoundRect(box, 18 * u))
                using (var b = new SolidBrush(Color.FromArgb(a * 150 / 255, 20, 20, 30)))
                    g.FillPath(b, p);
                TextBox(g, game.Message, 28, new RectangleF(box.X + 14 * u, box.Y + 6 * u, box.Width - 28 * u, box.Height - 12 * u), Color.FromArgb(a, 255, 245, 140));
            }

            foreach (var p in game.Popups)
            {
                int a = (int)(255 * Math.Max(0, 1 - p.Time / 1.5));
                Text(g, p.Text, 38, W * p.X, H * 0.4f - (float)p.Time * 80 * u, p.Color, StringAlignment.Center, a);
            }
        }

        void GiftBar(Graphics g)
        {
            var rooms = game.World.Rooms;
            float s = 46 * u, gap = 6 * u;
            float total = 10 * s + 9 * gap;
            float x = (W - total) / 2, y = 14 * u;
            var bg = new RectangleF(x - 12 * u, y - 6 * u, total + 24 * u, s + 12 * u);
            using (var p = RoundRect(bg, 16 * u))
            {
                using (var b = new SolidBrush(Color.FromArgb(160, 20, 25, 30))) g.FillPath(b, p);
                using (var pen = new Pen(Color.FromArgb(200, 220, 175, 70), 3 * u)) g.DrawPath(pen, p);
            }
            var byIndex = new Room[10];
            foreach (var r in rooms) byIndex[r.Index] = r;
            for (int i = 0; i < 10; i++)
            {
                var dest = new Rectangle((int)x, (int)y, (int)s, (int)s);
                if (byIndex[i] != null && byIndex[i].Done) g.DrawImage(game.Art.GiftIcons[i], dest);
                else g.DrawImage(game.Art.GiftBoxIcon, dest, 0, 0, game.Art.GiftBoxIcon.Width, game.Art.GiftBoxIcon.Height, GraphicsUnit.Pixel, faded);
                x += s + gap;
            }
        }

        void TalkPrompt(Graphics g)
        {
            float bounce = (float)Math.Abs(Math.Sin(game.Time * 4)) * 8 * u;
            bool kid = game.NearKid != null && game.NearRoom == null && !game.NearMaster;
            string label = kid ? L.F("talk_to", game.NearKid.Name) : L.T("talk");
            float half = Math.Max(170 * u, Measure(g, label, 32).Width / 2 + 30 * u);
            var box = new RectangleF(W / 2f - half, H - 120 * u - bounce, half * 2, 64 * u);
            Panel(g, box, Color.FromArgb(220, 40, 120, 60));
            Hit(box, 20);
            Text(g, label, 32, W / 2f, box.Y + 10 * u, Color.White);
        }

        /// <summary>Football score and clock (yard), or a hint to talk to the captain.</summary>
        void Scoreboard(Graphics g)
        {
            var m = game.Match;
            var box = new RectangleF(W / 2f - 300 * u, 10 * u, 600 * u, 66 * u);
            Panel(g, box, Color.FromArgb(190, 20, 60, 30), Color.FromArgb(230, 255, 255, 255));
            if (m != null && m.Active)
            {
                TextBox(g, m.ScoreText(), 26, new RectangleF(box.X + 10 * u, box.Y + 4 * u, box.Width - 110 * u, box.Height - 8 * u), Color.White);
                Text(g, m.TimeText(), 26, box.Right - 52 * u, box.Y + 16 * u, Color.FromArgb(255, 235, 80));
            }
            else TextBox(g, L.T("yard_hint"), 22, new RectangleF(box.X + 10 * u, box.Y + 4 * u, box.Width - 20 * u, box.Height - 8 * u), Color.White);
            if (m != null && m.EventTime > 0 && !string.IsNullOrEmpty(m.LastEvent))
            {
                float pop = (float)(1 + 0.3 * Math.Max(0, m.EventTime - 1));
                Text(g, m.LastEvent, 70 * pop, W / 2f, H * 0.3f, Color.FromArgb(255, 235, 80));
            }
        }

        void Caption(Graphics g, string text)
        {
            var box = new RectangleF(W / 2f - 440 * u, H - 150 * u, 880 * u, 120 * u);
            Panel(g, box, Color.FromArgb(225, 70, 30, 85), Color.FromArgb(230, 240, 200, 90));
            TextBox(g, text, 24, new RectangleF(box.X + 20 * u, box.Y + 10 * u, box.Width - 40 * u, box.Height - 20 * u), Color.White);
        }

        void Orb(Graphics g, float cx, float cy, float R, double frac)
        {
            var rect = new RectangleF(cx - R, cy - R, 2 * R, 2 * R);
            using (var b = new SolidBrush(Color.FromArgb(220, 20, 15, 20))) g.FillEllipse(b, rect);
            float level = rect.Bottom - (float)(Math.Max(0, Math.Min(1, frac)) * 2 * R);
            var st = g.Save();
            using (var clip = new GraphicsPath())
            {
                clip.AddEllipse(rect);
                g.SetClip(clip);
                g.IntersectClip(new RectangleF(rect.X, level, rect.Width, rect.Bottom - level + 1));
                using (var br = new LinearGradientBrush(new RectangleF(rect.X, rect.Y - 1, rect.Width, rect.Height + 2), Color.FromArgb(255, 230, 90), Color.FromArgb(200, 110, 0), 90f))
                    g.FillEllipse(br, rect);
                float wave = (float)Math.Sin(game.Time * 3) * 3 * u;
                using (var b = new SolidBrush(Color.FromArgb(100, 255, 255, 255)))
                    g.FillEllipse(b, rect.X - R * 0.2f, level - 5 * u + wave, rect.Width * 1.4f, 10 * u);
            }
            g.Restore(st);
            using (var b = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                g.FillEllipse(b, cx - R * 0.6f, cy - R * 0.85f, R * 0.9f, R * 0.55f);
            using (var pen = new Pen(Color.FromArgb(60, 40, 10), 9 * u)) g.DrawEllipse(pen, rect);
            using (var pen = new Pen(Color.FromArgb(220, 175, 70), 4 * u)) g.DrawEllipse(pen, rect);
        }

        void Compass(Graphics g, float cx, float cy, float r)
        {
            double tx, ty;
            bool master;
            if (!game.CompassTarget(out tx, out ty, out master)) return;
            double rel = Math.Atan2(ty - game.P.Y, tx - game.P.X) - game.CamAngle;
            using (var b = new SolidBrush(Color.FromArgb(170, 20, 30, 40))) g.FillEllipse(b, cx - r, cy - r, 2 * r, 2 * r);
            using (var pen = new Pen(Color.FromArgb(220, 175, 70), 4 * u)) g.DrawEllipse(pen, cx - r, cy - r, 2 * r, 2 * r);
            var st = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform((float)(rel * 180 / Math.PI));
            var pts = new[] { new PointF(0, -r * 0.8f), new PointF(r * 0.45f, r * 0.35f), new PointF(0, r * 0.12f), new PointF(-r * 0.45f, r * 0.35f) };
            using (var b = new SolidBrush(master ? Color.FromArgb(200, 120, 255) : Color.FromArgb(255, 200, 40))) g.FillPolygon(b, pts);
            using (var pen = new Pen(Color.White, 2 * u)) g.DrawPolygon(pen, pts);
            g.Restore(st);
        }

        void MiniMap(Graphics g, RectangleF area, int alpha)
        {
            var w = game.World;
            float cs = Math.Min(area.Width / w.Wd, area.Height / w.Ht);
            float ox = area.X + (area.Width - cs * w.Wd) / 2, oy = area.Y + (area.Height - cs * w.Ht) / 2;
            var frame = new RectangleF(ox - 5 * u, oy - 5 * u, cs * w.Wd + 10 * u, cs * w.Ht + 10 * u);
            using (var p = RoundRect(frame, 8 * u))
            {
                using (var b = new SolidBrush(Color.FromArgb(alpha, 15, 18, 22))) g.FillPath(b, p);
                using (var pen = new Pen(Color.FromArgb(alpha, 220, 175, 70), 3 * u)) g.DrawPath(pen, p);
            }
            var smooth = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;
            using (var wall = new SolidBrush(Color.FromArgb(alpha, 110, 95, 85)))
            using (var floor = new SolidBrush(Color.FromArgb(alpha, 200, 190, 160)))
            using (var done = new SolidBrush(Color.FromArgb(alpha, 120, 200, 110)))
            using (var hall = new SolidBrush(Color.FromArgb(alpha, 170, 90, 190)))
                for (int y = 0; y < w.Ht; y++)
                    for (int x = 0; x < w.Wd; x++)
                    {
                        int i = y * w.Wd + x;
                        if (!w.Explored[i]) continue;
                        Brush b = w.Wall[i] != 0 ? wall : floor;
                        if (w.Wall[i] == 0)
                        {
                            if (x > 41 && !game.InYard) b = hall;
                            foreach (var r in w.Rooms) if (r.Done && r.Contains(x + 0.5, y + 0.5)) b = done;
                        }
                        g.FillRectangle(b, ox + x * cs, oy + y * cs, cs + 0.5f, cs + 0.5f);
                    }
            g.SmoothingMode = smooth;

            float dot = Math.Max(6 * u, cs * 1.3f);
            foreach (var r in w.Rooms)
            {
                if (r.Done) continue;
                using (var b = new SolidBrush(Color.FromArgb(255, 255, 215, 40)))
                    g.FillEllipse(b, ox + (float)r.Teacher.X * cs - dot / 2, oy + (float)r.Teacher.Y * cs - dot / 2, dot, dot);
            }
            if (w.Master != null)
                using (var b = new SolidBrush((int)(game.Time * 3) % 2 == 0 ? Color.FromArgb(255, 220, 140, 255) : Color.FromArgb(255, 160, 80, 220)))
                    g.FillEllipse(b, ox + (float)w.Master.X * cs - dot / 2, oy + (float)w.Master.Y * cs - dot / 2, dot, dot);
            if (game.InYard && game.Match != null && game.Match.Ball != null)
                g.FillEllipse(Brushes.White, ox + (float)game.Match.Ball.X * cs - dot / 2, oy + (float)game.Match.Ball.Y * cs - dot / 2, dot, dot);

            var st = g.Save();
            g.TranslateTransform(ox + (float)game.P.X * cs, oy + (float)game.P.Y * cs);
            g.RotateTransform((float)(game.P.Angle * 180 / Math.PI));
            float a = Math.Max(7 * u, cs * 1.5f);
            var tri = new[] { new PointF(a, 0), new PointF(-a * 0.7f, a * 0.65f), new PointF(-a * 0.7f, -a * 0.65f) };
            using (var b = new SolidBrush(Color.FromArgb(80, 200, 80))) g.FillPolygon(b, tri);
            using (var pen = new Pen(Color.White, 1.5f * u)) g.DrawPolygon(pen, tri);
            g.Restore(st);
        }

        // ------------------------------------------------------------------ quiz dialog

        void DialogScreen(Graphics g)
        {
            var d = game.Dialog;
            Fill(g, Color.FromArgb(110, 10, 15, 25));

            // portrait of the teacher (mouth moves while speaking)
            bool talking = d.Time < 2.2 && (int)(d.Time * 8) % 2 == 0;
            Bitmap img;
            if (d.Kid != null) img = game.Art.KidPortrait[d.Kid.Variant % 3];
            else img = (d.IsMaster ? game.Art.MasterPortrait : game.Art.TeacherPortraits[d.Room.Subject])[talking ? 1 : 0];
            var accent = d.Kid != null ? Color.FromArgb(240, 120, 60) : d.IsMaster ? Color.FromArgb(160, 90, 220) : School.Subjects[d.Room.Subject].Color;
            float ph = 330 * u, pw = ph * img.Width / img.Height;
            var pr = new RectangleF(30 * u, H - ph - 30 * u, pw, ph);
            using (var b = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) g.FillEllipse(b, pr.X - 20 * u, pr.Y + 40 * u, pw + 40 * u, ph - 30 * u);
            g.DrawImage(img, pr);

            // speech bubble
            float bx = pr.Right + 20 * u, bw = W - bx - 30 * u;
            var bubble = new RectangleF(bx, 40 * u, bw, H - 300 * u);
            Panel(g, bubble, Color.FromArgb(255, 255, 252, 240), Color.FromArgb(230, accent));
            using (var tail = new GraphicsPath())
            {
                tail.AddPolygon(new[] { new PointF(bx + 4 * u, bubble.Bottom - 120 * u), new PointF(bx - 34 * u, bubble.Bottom - 70 * u), new PointF(bx + 4 * u, bubble.Bottom - 80 * u) });
                using (var b = new SolidBrush(Color.FromArgb(255, 255, 252, 240))) g.FillPath(b, tail);
            }
            Text(g, d.Speaker, 24, bubble.X + 24 * u, bubble.Y + 12 * u, accent, StringAlignment.Near);
            if (game.Voice.Available)
            {
                var read = new RectangleF(bubble.Right - 210 * u, bubble.Y + 12 * u, 190 * u, 36 * u);
                Panel(g, read, Color.FromArgb(220, 60, 140, 220), Color.White);
                Text(g, L.T("read"), 20, read.X + read.Width / 2, read.Y + 4 * u, Color.White);
                Hit(read, 11);
            }

            var inner = new RectangleF(bubble.X + 24 * u, bubble.Y + 56 * u, bubble.Width - 48 * u, bubble.Height - 70 * u);
            var dark = Color.FromArgb(60, 45, 35);
            if (d.Phase == Phase.Ask || d.Phase == Phase.Wrong || d.Phase == Phase.Correct)
            {
                string top = d.Phase == Phase.Ask ? d.Line : d.Line;
                Color topColor = d.Phase == Phase.Correct ? Color.FromArgb(40, 160, 60) : d.Phase == Phase.Wrong ? Color.FromArgb(220, 80, 60) : Color.FromArgb(120, 100, 80);
                TextPlain(g, top, 22, new RectangleF(inner.X, inner.Y, inner.Width, 70 * u), topColor);
                TextPlain(g, d.Q.Text, 34, new RectangleF(inner.X, inner.Y + 74 * u, inner.Width, inner.Height - 80 * u), dark);
            }
            else
            {
                TextPlain(g, d.Line, 34, inner, dark);
            }

            if (d.Phase == Phase.Reward && d.Gift >= 0) GiftReveal(g, d);

            // answers (2 x 2 grid)
            if (d.Q != null && (d.Phase == Phase.Ask || d.Phase == Phase.Wrong || d.Phase == Phase.Correct))
            {
                int n = d.Q.Answers.Length;
                float top = bubble.Bottom + 20 * u, aw = (bw - 20 * u) / 2, ah = (H - top - 70 * u) / 2 - 8 * u;
                var colors = new[] { Color.FromArgb(230, 80, 80), Color.FromArgb(60, 140, 230), Color.FromArgb(240, 170, 40), Color.FromArgb(70, 180, 90) };
                for (int i = 0; i < n; i++)
                {
                    var r = new RectangleF(bx + (i % 2) * (aw + 20 * u), top + (i / 2) * (ah + 16 * u), aw, ah);
                    bool sel = d.Phase == Phase.Ask && i == d.Selected;
                    Color fill = Color.FromArgb(235, 255, 255, 255);
                    if (d.Phase != Phase.Ask)
                    {
                        if (i == d.Q.Correct) fill = Color.FromArgb(240, 150, 230, 140);
                        else if (i == d.Chosen) fill = Color.FromArgb(240, 250, 150, 140);
                        else fill = Color.FromArgb(150, 220, 220, 220);
                    }
                    float shake = d.Phase == Phase.Wrong && i == d.Chosen && d.Time < 0.5 ? (float)Math.Sin(d.Time * 60) * 6 * u : 0;
                    r.X += shake;
                    if (sel) r.Inflate(4 * u, 4 * u);
                    Panel(g, r, fill, sel ? Color.FromArgb(255, 255, 200, 40) : colors[i]);
                    Hit(r, i);
                    var badge = new RectangleF(r.X + 12 * u, r.Y + r.Height / 2 - 22 * u, 44 * u, 44 * u);
                    using (var b = new SolidBrush(colors[i])) g.FillEllipse(b, badge);
                    Text(g, (i + 1).ToString(), 26, badge.X + badge.Width / 2, badge.Y + 4 * u, Color.White);
                    TextPlain(g, d.Q.Answers[i], 28, new RectangleF(r.X + 64 * u, r.Y + 4 * u, r.Width - 74 * u, r.Height - 8 * u), dark);
                }
            }

            // footer
            string hint = d.Phase == Phase.Ask ? L.T("answer_hint") : L.T("continue");
            var foot = new RectangleF(bx, H - 52 * u, bw, 44 * u);
            if (d.Phase != Phase.Ask) Hit(new RectangleF(0, 0, W, H), 10);
            Text(g, hint + "      " + L.T("esc_close"), 21, foot.X + foot.Width / 2, foot.Y + 6 * u, Color.White);
        }

        /// <summary>Plain wrapped text without an outline (inside the light speech bubble).</summary>
        void TextPlain(Graphics g, string s, float size, RectangleF box, Color color)
        {
            if (string.IsNullOrEmpty(s)) return;
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                Font f = null;
                try
                {
                    for (; size > 10; size -= 2)
                    {
                        f?.Dispose();
                        f = new Font(family, size * u, FontStyle.Bold, GraphicsUnit.Pixel);
                        if (g.MeasureString(s, f, (int)box.Width, sf).Height <= box.Height) break;
                    }
                    using (var b = new SolidBrush(color)) g.DrawString(s, f, b, box, sf);
                }
                finally { f?.Dispose(); }
            }
        }

        void GiftReveal(Graphics g, Dialog d)
        {
            float t = (float)Math.Min(1, d.Time * 2);
            float cx = W / 2f + 120 * u, cy = H * 0.62f, s = 180 * u * (0.4f + 0.6f * t);
            var st = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform((float)(game.Time * 40));
            for (int i = 0; i < 12; i++)
            {
                g.RotateTransform(30);
                using (var b = new SolidBrush(Hue(i / 12.0 + game.Time * 0.2, 120)))
                    g.FillPolygon(b, new[] { new PointF(0, 0), new PointF(-s * 0.15f, -s * 1.2f), new PointF(s * 0.15f, -s * 1.2f) });
            }
            g.Restore(st);
            g.DrawImage(game.Art.GiftIcons[d.Gift], cx - s / 2, cy - s / 2, s, s);
        }

        // ------------------------------------------------------------------ floor screens

        void IntroBanner(Graphics g)
        {
            float t = (float)Math.Min(1, game.StateTime * 2.5);
            var panel = new RectangleF(W / 2f - 440 * u, 250 * u - (1 - t) * 400 * u, 880 * u, 330 * u);
            Panel(g, panel, Color.FromArgb(225, 30, 60, 45), Color.FromArgb(230, game.Theme.Accent));
            Hit(new RectangleF(0, 0, W, H), 10);
            Text(g, L.T("floor") + " " + (game.FloorIndex + 1), 38, W / 2f, panel.Y + 14 * u, Color.FromArgb(255, 235, 80));
            Text(g, game.Theme.Name, 46, W / 2f, panel.Y + 62 * u, Color.White);
            Text(g, L.T("goal1"), 24, W / 2f, panel.Y + 150 * u, Color.FromArgb(220, 255, 200));
            Text(g, L.T("goal2"), 24, W / 2f, panel.Y + 192 * u, Color.FromArgb(220, 255, 200));
            Text(g, L.T("goal3"), 24, W / 2f, panel.Y + 234 * u, Color.FromArgb(220, 255, 200));
            Text(g, L.T("press"), 20, W / 2f, panel.Y + 285 * u, Color.FromArgb(200, 200, 200));
        }

        void FloorDone(Graphics g)
        {
            Dim(g, 130);
            var panel = new RectangleF(W / 2f - 420 * u, H * 0.1f, 840 * u, 520 * u);
            Panel(g, panel, Color.FromArgb(225, 30, 60, 45));
            Hit(new RectangleF(0, 0, W, H), 10);
            Text(g, L.T("done"), 48, W / 2f, panel.Y + 22 * u, Color.FromArgb(255, 235, 80));
            Text(g, game.Theme.Name, 26, W / 2f, panel.Y + 86 * u, Color.White);
            for (int i = 0; i < 3; i++)
            {
                double appear = game.StateTime * 2.5 - i * 0.6;
                bool filled = i < game.LastStars && appear > 0;
                float pop = filled ? (float)(1 + 0.4 * Math.Max(0, 1 - appear)) : 1;
                StarShape(g, W / 2f + (i - 1) * 130 * u, panel.Y + 200 * u, 50 * u * pop, filled);
            }
            float s = 52 * u, x = W / 2f - (10 * s + 9 * 6 * u) / 2;
            for (int i = 0; i < 10; i++, x += s + 6 * u) g.DrawImage(game.Art.GiftIcons[i], x, panel.Y + 280 * u, s, s);
            Text(g, L.T("mistakes") + ": " + game.Mistakes + "      " + L.T("coins") + ": " + game.Save.Coins, 28, W / 2f, panel.Y + 360 * u, Color.White);
            if (game.StateTime > 1 && (int)(game.Time * 2) % 2 == 0)
                Text(g, L.T("next"), 28, W / 2f, panel.Y + 440 * u, Color.FromArgb(220, 255, 200));
        }

        void Victory(Graphics g)
        {
            Dim(g, 100);
            var rnd = new Random(7);
            for (int i = 0; i < 90; i++)
            {
                float speed = 60 + (float)rnd.NextDouble() * 120;
                float x = (float)rnd.NextDouble() * W + (float)Math.Sin(game.Time * 2 + i) * 20 * u;
                float y = ((float)rnd.NextDouble() * H + (float)game.Time * speed * u) % (H + 20 * u) - 10 * u;
                var st = g.Save();
                g.TranslateTransform(x, y);
                g.RotateTransform((float)(game.Time * 180 + i * 37));
                using (var b = new SolidBrush(Hue(i * 0.13))) g.FillRectangle(b, -6 * u, -3 * u, 12 * u, 6 * u);
                g.Restore(st);
            }
            // diploma
            var paper = new RectangleF(W / 2f - 400 * u, 70 * u, 800 * u, 520 * u);
            using (var b = new SolidBrush(Color.FromArgb(250, 250, 240, 215))) g.FillRectangle(b, paper);
            using (var pen = new Pen(Color.FromArgb(200, 160, 60), 8 * u)) g.DrawRectangle(pen, paper.X + 10 * u, paper.Y + 10 * u, paper.Width - 20 * u, paper.Height - 20 * u);
            Hit(new RectangleF(0, 0, W, H), 10);
            var dark = Color.FromArgb(90, 60, 30);
            TextPlain(g, L.T("diploma"), 60, new RectangleF(paper.X, paper.Y + 24 * u, paper.Width, 80 * u), Color.FromArgb(180, 120, 20));
            TextPlain(g, L.T("school_name"), 26, new RectangleF(paper.X + 30 * u, paper.Y + 110 * u, paper.Width - 60 * u, 40 * u), dark);
            TextPlain(g, L.T("school_patron"), 18, new RectangleF(paper.X + 30 * u, paper.Y + 148 * u, paper.Width - 60 * u, 30 * u), dark);
            g.DrawImage(game.Art.TurtleIcon, W / 2f - 60 * u, paper.Y + 186 * u + (float)Math.Sin(game.Time * 3) * 6 * u, 120 * u, 114 * u);
            TextPlain(g, L.T("victory"), 34, new RectangleF(paper.X + 30 * u, paper.Y + 310 * u, paper.Width - 60 * u, 50 * u), Color.FromArgb(40, 140, 60));
            TextPlain(g, L.T("diploma2"), 22, new RectangleF(paper.X + 30 * u, paper.Y + 362 * u, paper.Width - 60 * u, 40 * u), dark);
            int stars = 0;
            foreach (var s in game.Save.Stars) stars += s;
            TextPlain(g, L.F("victory2", stars, School.Floors * 3), 26, new RectangleF(paper.X, paper.Y + 410 * u, paper.Width, 40 * u), dark);
            if (game.StateTime > 1.5) Text(g, L.T("press"), 24, W / 2f, paper.Bottom + 20 * u, Color.White);
        }
    }
}
