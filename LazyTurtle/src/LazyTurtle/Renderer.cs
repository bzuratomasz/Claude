using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace LazyTurtle
{
    /// <summary>
    /// Software raycaster for indoor scenes: textured floor and ceiling, walls 1.5 cells high with a
    /// different texture per face, billboard sprites, distance darkening and a slightly lowered view
    /// (horizon shift) for the third-person camera. Pure CPU, so it runs on any PC.
    /// </summary>
    sealed class Renderer
    {
        public const double PlaneLen = 0.8;      // ~77 degree field of view
        public const double WallH = 1.5;

        public int W, H;
        public int[] Px = new int[0];
        double[] zbuf = new double[0];
        readonly List<Entity> visible = new List<Entity>();
        static readonly Comparison<Entity> FarToNear = (a, b) => b.Dist.CompareTo(a.Dist);

        public void Resize(int w, int h)
        {
            if (w == W && h == H) return;
            W = w;
            H = h;
            Px = new int[w * h];
            zbuf = new double[w];
        }

        static int Mix(int c, int fog, int a)
        {
            int ia = 256 - a;
            int r = (((c >> 16) & 255) * ia + ((fog >> 16) & 255) * a) >> 8;
            int g = (((c >> 8) & 255) * ia + ((fog >> 8) & 255) * a) >> 8;
            int b = ((c & 255) * ia + (fog & 255) * a) >> 8;
            return (r << 16) | (g << 8) | b;
        }

        static int Shade(int c) => ((c & 0xFEFEFE) >> 1) + ((c & 0xFCFCFC) >> 2) + ((c & 0xF8F8F8) >> 3);   // ~87%

        public void Render(World w, double px, double py, double ang, double camZ, double horizonFrac, double time)
        {
            double dirX = Math.Cos(ang), dirY = Math.Sin(ang);
            double plX = -dirY * PlaneLen, plY = dirX * PlaneLen;
            double S = W / (2 * PlaneLen);
            int horizon = (int)(H * horizonFrac);
            int fog = w.Theme.Fog.ToArgb() & 0xFFFFFF;
            const double fogEnd = 24, fogStart = 4;
            const double fogK = 200 / (fogEnd - fogStart);   // never fully dark: max 200/256

            // ---- floor and ceiling, row by row
            double r0x = dirX - plX, r0y = dirY - plY, r1x = dirX + plX, r1y = dirY + plY;
            for (int y = 0; y < H; y++)
            {
                bool ceiling = y < horizon;
                if (ceiling && w.Outdoor)
                {
                    // software fallback for the yard: simple sky gradient
                    int sky = (int)(110 + 120.0 * y / Math.Max(1, horizon));
                    int c = (Math.Min(255, sky - 40) << 16) | (Math.Min(255, sky + 10) << 8) | 255;
                    for (int x = 0; x < W; x++) Px[y * W + x] = c;
                    continue;
                }
                double rowDist = ceiling ? (WallH - camZ) * S / (horizon - y + 0.5) : camZ * S / (y - horizon + 0.5);
                int f = rowDist <= fogStart ? 0 : (int)Math.Min(200, (rowDist - fogStart) * fogK);
                double sx = rowDist * (r1x - r0x) / W, sy = rowDist * (r1y - r0y) / W;
                double fx = px + rowDist * r0x, fy = py + rowDist * r0y;
                int o = y * W;
                byte[] cells = ceiling ? w.Ceil : w.Floor;
                var texs = ceiling ? w.CeilTex : w.FloorTex;
                for (int x = 0; x < W; x++)
                {
                    int cx = fx < 0 ? -1 : (int)fx, cy = fy < 0 ? -1 : (int)fy;
                    int id = cx >= 0 && cy >= 0 && cx < w.Wd && cy < w.Ht ? cells[cy * w.Wd + cx] : 0;
                    int c = texs[id].Px[(((int)(fy * 128) & 127) << 7) | ((int)(fx * 128) & 127)];
                    Px[o + x] = f > 0 ? Mix(c, fog, f) : c;
                    fx += sx;
                    fy += sy;
                }
            }

            // ---- walls (one ray per column, DDA)
            for (int x = 0; x < W; x++)
            {
                double cam = 2.0 * x / W - 1;
                double rdx = dirX + plX * cam, rdy = dirY + plY * cam;
                int mx = (int)Math.Floor(px), my = (int)Math.Floor(py);
                double ddx = rdx == 0 ? 1e30 : Math.Abs(1 / rdx), ddy = rdy == 0 ? 1e30 : Math.Abs(1 / rdy);
                int stepX, stepY;
                double sdx, sdy;
                if (rdx < 0) { stepX = -1; sdx = (px - mx) * ddx; } else { stepX = 1; sdx = (mx + 1 - px) * ddx; }
                if (rdy < 0) { stepY = -1; sdy = (py - my) * ddy; } else { stepY = 1; sdy = (my + 1 - py) * ddy; }

                int hit = -1, side = 0;
                for (int i = 0; i < 200; i++)
                {
                    if (sdx < sdy) { sdx += ddx; mx += stepX; side = 0; }
                    else { sdy += ddy; my += stepY; side = 1; }
                    if (!w.InBounds(mx, my)) break;
                    int ci = my * w.Wd + mx;
                    if (w.Wall[ci] != 0) { hit = ci; break; }
                }
                if (hit < 0) { zbuf[x] = 1e30; continue; }

                double perp = side == 0 ? sdx - ddx : sdy - ddy;
                if (perp < 0.02) perp = 0.02;
                zbuf[x] = perp;

                int face = side == 0 ? (stepX > 0 ? World.FaceW : World.FaceE) : (stepY > 0 ? World.FaceN : World.FaceS);
                Tex t = w.WallAt(hit, face);
                double wallX = side == 0 ? py + perp * rdy : px + perp * rdx;
                wallX -= Math.Floor(wallX);
                int tx = (int)(wallX * t.W);
                if ((side == 0 && rdx < 0) || (side == 1 && rdy > 0)) tx = t.W - tx - 1;
                if (tx < 0) tx = 0; else if (tx >= t.W) tx = t.W - 1;

                double top = horizon - (WallH - camZ) * S / perp, bottom = horizon + camZ * S / perp;
                int y0 = Math.Max(0, (int)Math.Ceiling(top)), y1 = Math.Min(H - 1, (int)bottom);
                double texStep = t.H / (bottom - top), texPos = (y0 - top) * texStep;
                int f = perp <= fogStart ? 0 : (int)Math.Min(200, (perp - fogStart) * fogK);
                for (int y = y0; y <= y1; y++)
                {
                    int ty = (int)texPos;
                    if (ty >= t.H) ty = t.H - 1;
                    texPos += texStep;
                    int c = t.Px[ty * t.W + tx];
                    if (side == 1) c = Shade(c);
                    Px[y * W + x] = f > 0 ? Mix(c, fog, f) : c;
                }
            }

            // ---- sprites, far to near
            visible.Clear();
            foreach (var e in w.Entities)
            {
                if (!e.Alive || e.Frames == null) continue;
                double dx = e.X - px, dy = e.Y - py;
                e.Dist = dx * dx + dy * dy;
                if (e.Dist < fogEnd * fogEnd) visible.Add(e);
            }
            visible.Sort(FarToNear);

            double invDet = 1.0 / (plX * dirY - dirX * plY);
            foreach (var e in visible)
            {
                double sx = e.X - px, sy = e.Y - py;
                double tX = invDet * (dirY * sx - dirX * sy);
                double tY = invDet * (-plY * sx + plX * sy);
                if (tY < 0.15 || (e.Kind == Kind.Sparkle && tY < 0.5)) continue;

                double screenX = W / 2.0 * (1 + tX / tY);
                double z = e.Z + (e.Bob ? 0.03 + 0.03 * Math.Sin(time * 3 + e.Phase) : 0);
                double hp = e.H * S / tY, wp = e.W * S / tY;
                double bottom = horizon + (camZ - z) * S / tY, top = bottom - hp, left = screenX - wp / 2;
                int x0 = Math.Max(0, (int)Math.Ceiling(left)), x1 = Math.Min(W - 1, (int)(left + wp));
                int y0 = Math.Max(0, (int)Math.Ceiling(top)), y1 = Math.Min(H - 1, (int)bottom);
                if (x0 > x1 || y0 > y1) continue;

                var frames = e.Frames;
                Tex t = frames.Length == 1 ? frames[0] : frames[(int)(time * e.AnimRate + e.Phase * 10) % frames.Length];
                int f = tY <= fogStart ? 0 : (int)Math.Min(200, (tY - fogStart) * fogK);
                bool flip = e.Kind == Kind.Kid && e.Dir < 0;
                for (int x = x0; x <= x1; x++)
                {
                    if (tY >= zbuf[x]) continue;
                    int tx = (int)((x - left) * t.W / wp);
                    if (tx >= t.W) tx = t.W - 1;
                    if (flip) tx = t.W - 1 - tx;
                    for (int y = y0; y <= y1; y++)
                    {
                        int ty = (int)((y - top) * t.H / hp);
                        if (ty >= t.H) ty = t.H - 1;
                        int c = t.Px[ty * t.W + tx];
                        if (c == 0) continue;
                        Px[y * W + x] = f > 0 ? Mix(c, fog, f) : c;
                    }
                }
            }
        }

        // ---- fast blit of the frame onto the window (GDI StretchDIBits, available on every Windows)

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        [DllImport("gdi32.dll")]
        static extern int StretchDIBits(IntPtr hdc, int xDest, int yDest, int wDest, int hDest, int xSrc, int ySrc, int wSrc, int hSrc,
            int[] bits, ref BITMAPINFOHEADER bmi, uint usage, uint rop);

        [DllImport("gdi32.dll")]
        static extern int SetStretchBltMode(IntPtr hdc, int mode);

        public void Blit(Graphics g, Rectangle dest)
        {
            if (W == 0 || H == 0) return;
            var bmi = new BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                biWidth = W,
                biHeight = -H,          // top-down
                biPlanes = 1,
                biBitCount = 32,
            };
            IntPtr hdc = g.GetHdc();
            try
            {
                SetStretchBltMode(hdc, 4);   // HALFTONE: smooth scaling
                StretchDIBits(hdc, dest.X, dest.Y, dest.Width, dest.Height, 0, 0, W, H, Px, ref bmi, 0, 0x00CC0020);
            }
            finally
            {
                g.ReleaseHdc(hdc);
            }
        }
    }
}
