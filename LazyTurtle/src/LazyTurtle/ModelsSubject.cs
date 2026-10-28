using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace LazyTurtle
{
    /// <summary>
    /// Subject models: the ten dedicated subject teachers ("teacher0" … "teacher9") and the classroom
    /// artifacts of every subject (keys "m_*", "l_*", "n_*", "g_*", "e_*", "lo_*", "t_*", "a_*", "s_*", "sc_*").
    /// Same conventions as the rest of <see cref="Models"/>: +X is the side shown to the class, y up,
    /// origin in the centre of the footprint on the floor, 1 unit = 1 map cell.
    /// </summary>
    static partial class Models
    {
        static readonly Dictionary<string, Action<Model>> artifacts = new Dictionary<string, Action<Model>>
        {
            { "m_solids", MathSolids }, { "m_ruler", MathRuler }, { "m_abacus", MathAbacus },
            { "l_bookcase", LettersBookcase }, { "l_typewriter", LettersTypewriter }, { "l_inkwell", LettersInkwell },
            { "n_aquarium", NatureAquarium }, { "n_microscope", NatureMicroscope }, { "n_butterflies", NatureButterflies }, { "n_plants", NaturePlants },
            { "g_globe", GeoGlobe }, { "g_relief", GeoRelief }, { "g_rug", GeoRug }, { "g_knight", GeoKnight },
            { "e_bus", EnglishBus }, { "e_bigben", EnglishBigBen }, { "e_phonebox", EnglishPhoneBox },
            { "lo_chess", LogicChess }, { "lo_cube", LogicCube }, { "lo_tangram", LogicTangram },
            { "t_clock", TimeClock }, { "t_hourglass", TimeHourglass }, { "t_sundial", TimeSundial },
            { "a_piano", ArtPiano }, { "a_easel", ArtEasel }, { "a_drums", ArtDrums }, { "a_guitar", ArtGuitar },
            { "s_hoop", SportHoop }, { "s_mat", SportMat }, { "s_rack", SportRack },
            { "sc_telescope", ScienceTelescope }, { "sc_volcano", ScienceVolcano }, { "sc_planets", SciencePlanets }, { "sc_lab", ScienceLab },
        };

        /// <summary>Keys of all classroom artifact models.</summary>
        public static IEnumerable<string> ArtifactKeys => artifacts.Keys;

        /// <summary>Builds a classroom artifact; false for keys that are not artifacts.</summary>
        static bool BuildArtifact(Model m, string key)
        {
            Action<Model> build;
            if (!artifacts.TryGetValue(key, out build)) return false;
            build(m);
            return true;
        }

        // ================================================================== shared helpers

        static double Sq(double v) => v * v;

        /// <summary>Double-sided quad (both faces lit).</summary>
        static void Panel(MeshBuilder b, V3 p0, V3 p1, V3 p2, V3 p3)
        {
            b.Quad(p0, p1, p2, p3);
            b.Quad(p3, p2, p1, p0);
        }

        /// <summary>Rectangle facing +x (w along z, h along y); its texture reads upright from +x.</summary>
        static void FaceX(MeshBuilder b, double x, double cy, double cz, double w, double h)
        {
            b.Quad(P(x, cy - h / 2, cz + w / 2), P(x, cy - h / 2, cz - w / 2), P(x, cy + h / 2, cz - w / 2), P(x, cy + h / 2, cz + w / 2));
        }

        /// <summary>Points of an ellipse as (a, b) pairs: a = ca + ra·cos, b = cb + rb·sin, from angle a0 to a1.</summary>
        static double[] Oval(double ca, double cb, double ra, double rb, int n, double a0 = 0, double a1 = 2 * Math.PI)
        {
            bool closed = Math.Abs(a1 - a0 - 2 * PI) < 1e-9;
            int cnt = closed ? n : n + 1;
            var r = new double[cnt * 2];
            for (int k = 0; k < cnt; k++)
            {
                double a = a0 + (a1 - a0) * k / n;
                r[k * 2] = ca + ra * Math.Cos(a);
                r[k * 2 + 1] = cb + rb * Math.Sin(a);
            }
            return r;
        }

        /// <summary>Flat convex polygon of (y, z) pairs facing +x with planar uv over the box (cy, cz) ± (hy, hz).</summary>
        static void PolyX(MeshBuilder b, double x, double[] yz, double cy, double cz, double hy, double hz)
        {
            var n = P(1, 0, 0);
            int cnt = yz.Length / 2;
            double my = 0, mz = 0;
            for (int i = 0; i < cnt; i++) { my += yz[i * 2]; mz += yz[i * 2 + 1]; }
            my /= cnt; mz /= cnt;
            Func<double, double, float> U = (y, z) => (float)(0.5 - (z - cz) / (2 * hz));
            Func<double, double, float> Vv = (y, z) => (float)(0.5 - (y - cy) / (2 * hy));
            for (int i = 0; i < cnt; i++)
            {
                int j = (i + 1) % cnt;
                double y0 = yz[i * 2], z0 = yz[i * 2 + 1], y1 = yz[j * 2], z1 = yz[j * 2 + 1];
                b.Vertex(P(x, my, mz), n, U(my, mz), Vv(my, mz));
                b.Vertex(P(x, y1, z1), n, U(y1, z1), Vv(y1, z1));
                b.Vertex(P(x, y0, z0), n, U(y0, z0), Vv(y0, z0));
            }
        }

        /// <summary>Disc facing +x; a texture reads upright from +x.</summary>
        static void DiscX(MeshBuilder b, double x, double cy, double cz, double r, int slices) => PolyX(b, x, Oval(cy, cz, r, r, slices), cy, cz, r, r);

        /// <summary>Flat convex polygon of (x, z) pairs facing up; the top of its texture points to -x (upright for a viewer at +x).</summary>
        static void PolyUp(MeshBuilder b, double y, double[] xz, double cx, double cz, double hx, double hz)
        {
            var n = P(0, 1, 0);
            int cnt = xz.Length / 2;
            double mx = 0, mz = 0;
            for (int i = 0; i < cnt; i++) { mx += xz[i * 2]; mz += xz[i * 2 + 1]; }
            mx /= cnt; mz /= cnt;
            Func<double, double, float> U = (x, z) => (float)(0.5 - (z - cz) / (2 * hz));
            Func<double, double, float> Vv = (x, z) => (float)(0.5 + (x - cx) / (2 * hx));
            for (int i = 0; i < cnt; i++)
            {
                int j = (i + 1) % cnt;
                double x0 = xz[i * 2], z0 = xz[i * 2 + 1], x1 = xz[j * 2], z1 = xz[j * 2 + 1];
                b.Vertex(P(mx, y, mz), n, U(mx, mz), Vv(mx, mz));
                b.Vertex(P(x0, y, z0), n, U(x0, z0), Vv(x0, z0));
                b.Vertex(P(x1, y, z1), n, U(x1, z1), Vv(x1, z1));
            }
        }

        /// <summary>Flat oval in the y-z plane facing +x, rotated by rot (wings, blobs).</summary>
        static void FlatOval(MeshBuilder b, double x, double cy, double cz, double ry, double rz, double rot, int n)
        {
            var nn = P(1, 0, 0);
            var ctr = P(x, cy, cz);
            double cr = Math.Cos(rot), sr = Math.Sin(rot);
            Func<double, V3> at = a =>
            {
                double yy = ry * Math.Sin(a), zz = rz * Math.Cos(a);
                return P(x, cy + yy * cr + zz * sr, cz - yy * sr + zz * cr);
            };
            for (int k = 0; k < n; k++)
            {
                b.Vertex(ctr, nn, 0, 0);
                b.Vertex(at(2 * PI * k / n), nn, 0, 0);
                b.Vertex(at(2 * PI * (k + 1) / n), nn, 0, 0);
            }
        }

        static V3 MapFloor(double a, double c, double d) => P(a, d, c);      // (x, z) polygon, extruded along y
        static V3 MapFront(double a, double c, double d) => P(d, a, c);      // (y, z) polygon, extruded along x
        static V3 MapSide(double a, double c, double d) => P(a, c, d);       // (x, y) polygon, extruded along z

        /// <summary>Convex polygon of (a, c) pairs extruded from d0 to d1; map places (a, c, d). Lit correctly for any winding.</summary>
        static void Prism(MeshBuilder b, double[] ac, double d0, double d1, Func<double, double, double, V3> map)
        {
            int n = ac.Length / 2;
            var bot = new V3[n];
            var top = new V3[n];
            for (int i = 0; i < n; i++)
            {
                bot[i] = map(ac[i * 2], ac[i * 2 + 1], d0);
                top[i] = map(ac[i * 2], ac[i * 2 + 1], d1);
            }
            var up = top[0] - bot[0];
            if (V3.Dot(V3.Cross(top[1] - top[0], top[2] - top[0]), up) < 0)
            {
                Array.Reverse(bot);
                Array.Reverse(top);
            }
            for (int i = 1; i + 1 < n; i++)
            {
                Tri(b, top[0], top[i], top[i + 1]);
                Tri(b, bot[0], bot[i + 1], bot[i]);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                b.Quad(bot[i], bot[j], top[j], top[i]);
            }
        }

        /// <summary>Triangle frame (a triangle with a triangular hole, like a set-square): three trapezoid prisms.</summary>
        static void TriFrame(MeshBuilder b, double[] tri, double k, double d0, double d1, Func<double, double, double, V3> map)
        {
            double la = Math.Sqrt(Sq(tri[2] - tri[4]) + Sq(tri[3] - tri[5]));
            double lb = Math.Sqrt(Sq(tri[0] - tri[4]) + Sq(tri[1] - tri[5]));
            double lc = Math.Sqrt(Sq(tri[0] - tri[2]) + Sq(tri[1] - tri[3]));
            double s = la + lb + lc;
            double ia = (la * tri[0] + lb * tri[2] + lc * tri[4]) / s, ic = (la * tri[1] + lb * tri[3] + lc * tri[5]) / s;
            var inner = new double[6];
            for (int i = 0; i < 3; i++)
            {
                inner[i * 2] = ia + (tri[i * 2] - ia) * k;
                inner[i * 2 + 1] = ic + (tri[i * 2 + 1] - ic) * k;
            }
            for (int i = 0; i < 3; i++)
            {
                int j = (i + 1) % 3;
                Prism(b, new[] { tri[i * 2], tri[i * 2 + 1], tri[j * 2], tri[j * 2 + 1], inner[j * 2], inner[j * 2 + 1], inner[i * 2], inner[i * 2 + 1] }, d0, d1, map);
            }
        }

        /// <summary>Square pyramid with flat faces, base centred on (cx, y, cz), faces towards ±x and ±z.</summary>
        static void Pyramid(MeshBuilder b, double cx, double y, double cz, double half, double h)
        {
            var apex = P(cx, y + h, cz);
            var q = new[] { P(cx + half, y, cz + half), P(cx + half, y, cz - half), P(cx - half, y, cz - half), P(cx - half, y, cz + half) };
            for (int i = 0; i < 4; i++) Tri(b, q[i], q[(i + 1) % 4], apex);
            b.Quad(q[3], q[2], q[1], q[0]);
        }

        /// <summary>Horizontal ring (torus lying flat) at height y.</summary>
        static void Hoop(MeshBuilder b, double cx, double y, double cz, double R, double r, int slices = 16, int sides = 4)
        {
            b.Push(Mul(T(cx, y, cz), M4.RotZ(PI / 2)));
            b.Torus(0, 0, 0, R, r, slices, sides);
            b.Pop();
        }

        /// <summary>Elliptic horizontal ring (belts, collars, scarves) of radii rx, rz with a round tube of radius t.</summary>
        static void Band(MeshBuilder b, double cx, double y, double cz, double rx, double rz, double t, int slices = 18, int sides = 4)
        {
            Func<int, int, V3[]> at = (i, j) =>
            {
                double u = 2 * PI * i / slices, v = 2 * PI * j / sides;
                var radial = P(Math.Cos(u) * rz, 0, Math.Sin(u) * rx).Normalized();   // outward normal of the ellipse
                var n = radial * Math.Cos(v) + P(0, Math.Sin(v), 0);
                return new[] { P(cx + Math.Cos(u) * rx, y, cz + Math.Sin(u) * rz) + n * t, n };
            };
            for (int i = 0; i < slices; i++)
                for (int j = 0; j < sides; j++)
                {
                    var q0 = at(i, j); var q1 = at(i + 1, j); var q2 = at(i + 1, j + 1); var q3 = at(i, j + 1);
                    b.Vertex(q0[0], q0[1], 0, 0); b.Vertex(q1[0], q1[1], 0, 0); b.Vertex(q2[0], q2[1], 0, 0);
                    b.Vertex(q0[0], q0[1], 0, 0); b.Vertex(q2[0], q2[1], 0, 0); b.Vertex(q3[0], q3[1], 0, 0);
                }
        }

        /// <summary>Quad turned so that its normal points up (terrain cells).</summary>
        static void FaceUp(MeshBuilder b, V3 a, V3 bb, V3 c, V3 d)
        {
            if (V3.Cross(bb - a, c - a).Y >= 0) b.Quad(a, bb, c, d);
            else b.Quad(d, c, bb, a);
        }

        /// <summary>Wooden table centred on the origin; returns the height of its top.</summary>
        static double Table(Model m, double sx, double sz, double h, Color top, Color frame)
        {
            var w = m.Get(WoodTex());
            w.Col(top); w.Box(0, h - 0.02, 0, sx, 0.04, sz);
            w.Col(frame);
            double lx = sx / 2 - 0.05, lz = sz / 2 - 0.05;
            for (int a = -1; a <= 1; a += 2)
            {
                w.Box(a * lx, (h - 0.04) / 2, -lz, 0.045, h - 0.04, 0.045);
                w.Box(a * lx, (h - 0.04) / 2, lz, 0.045, h - 0.04, 0.045);
                w.Box(a * lx, h - 0.075, 0, 0.025, 0.06, sz - 0.12);
                w.Box(0, h - 0.075, a * lz, sx - 0.12, 0.06, 0.025);
            }
            return h;
        }

        static double Table(Model m, double sx, double sz, double h) => Table(m, sx, sz, h, C(215, 170, 120), C(160, 110, 70));

        /// <summary>Round column with a square base and top slab; returns the height of the top.</summary>
        static double Pedestal(Model m, double h, double r, Color col)
        {
            var c = m.Plain;
            c.Col(Shade(col, -22)); c.Box(0, 0.04, 0, r * 2, 0.08, r * 2);
            c.Col(Shade(col, -10)); c.Cylinder(0, 0.08, 0, r * 0.85, r * 0.7, 0.13, 16);
            c.Col(col); c.Cylinder(0, 0.13, 0, r * 0.62, r * 0.55, h - 0.1, 16);
            c.Col(Shade(col, -10)); c.Cylinder(0, h - 0.1, 0, r * 0.6, r * 0.88, h - 0.05, 16);
            c.Col(Shade(col, -22)); c.Box(0, h - 0.025, 0, r * 2, 0.05, r * 2);
            return h;
        }

        // ================================================================== teachers

        /// <summary>What a teacher wears; drives the shared upright-turtle body.</summary>
        sealed class Outfit
        {
            public Color Body, Sleeve;
            public Color Buttons = C(250, 240, 210);
            public bool ShowButtons = true, Belly = true, Hair = true, Bun = true;
            public Tex BellyTex;
            public Color Legs = C(130, 180, 105), Feet = C(85, 135, 70);
        }

        /// <summary>Front surface (x) of the teacher's body ellipsoid at height y and side offset z.</summary>
        static double BodyFront(double y, double z) => 0.2 * Math.Sqrt(Math.Max(0, 1 - Sq((y - 0.45) / 0.33) - Sq(z / 0.24)));

        /// <summary>Front surface (x) of the cream belly plate.</summary>
        static double BellyFront(double y, double z) => 0.07 + 0.15 * Math.Sqrt(Math.Max(0, 1 - Sq((y - 0.43) / 0.27) - Sq(z / 0.14)));

        /// <summary>Pushes a frame on the body surface at (y, z) whose +x is the surface normal (for badges and pockets).</summary>
        static void PushOnBody(MeshBuilder c, double y, double z, double lift)
        {
            double x = BodyFront(y, z);
            double nx = x / 0.04, nz = z / 0.0576;
            double a = Math.Atan2(nz, nx);
            c.Push(Mul(T(x + lift * Math.Cos(a), y, z + lift * Math.Sin(a)), M4.RotY(-a)));
        }

        static readonly V3 ShoulderL = P(0, 0.69, -0.22), ShoulderR = P(0, 0.69, 0.22), HangL = P(0.06, 0.39, -0.27);

        /// <summary>
        /// The shared old upright turtle (≈1.0 tall): feet, shell, outfit body, belly plate, head with round glasses,
        /// white eyebrows and a smile; ladies get grey hair (with a bun), gentlemen a white moustache. Returns the head centre.
        /// </summary>
        static V3 TeacherBody(Model m, bool lady, Outfit o)
        {
            var c = m.Plain;
            for (int s = -1; s <= 1; s += 2)
            {
                c.Col(o.Feet); c.Ellipsoid(0.07, 0.04, s * 0.10, 0.10, 0.045, 0.07, 8, 3);
                c.Col(o.Legs); c.Ellipsoid(0, 0.12, s * 0.10, 0.065, 0.08, 0.065, 6, 4);
            }
            BackShell(m, P(-0.15, 0.47, 0), 0.36, 0.14, 0.28, C(210, 200, 160));
            c.Col(o.Body); c.Ellipsoid(0, 0.45, 0, 0.20, 0.33, 0.24, 12, 8);
            if (o.BellyTex != null)
            {
                var t = m.Get(o.BellyTex);
                t.Col(White); t.Ellipsoid(0.07, 0.43, 0, 0.15, 0.27, 0.14, 16, 10, -1, 1, 3);
            }
            else if (o.Belly)
            {
                c.Col(Cream); c.Ellipsoid(0.07, 0.43, 0, 0.15, 0.27, 0.14, 10, 6);
                c.Col(C(205, 185, 125));
                foreach (double y in new[] { 0.34, 0.43, 0.52 })
                {
                    var pts = new V3[5];
                    for (int k = 0; k < 5; k++)
                    {
                        double z = -0.1 + 0.05 * k;
                        pts[k] = P(BellyFront(y, z) + 0.002, y, z);
                    }
                    for (int k = 0; k < 4; k++) Cone(c, pts[k], pts[k + 1], 0.006, 0.006, 4, false);
                }
            }
            if (o.ShowButtons)
            {
                c.Col(o.Buttons);
                foreach (double y in new[] { 0.35, 0.45, 0.55 }) c.Sphere(BodyFront(y, 0.16) + 0.008, y, 0.16, 0.017, 5, 3);
            }
            // neck and head
            c.Col(OldSkin); c.Ellipsoid(0.02, 0.76, 0, 0.09, 0.08, 0.09, 8, 3);
            var head = P(0.04, 0.88, 0);
            c.Ellipsoid(head.X, head.Y, head.Z, 0.19, 0.18, 0.19, 14, 8);
            Eyes(c, head, 0.19, 0.38, 0.12, 0.045);
            Glasses(c, head, 0.19, 0.38, 0.12, 0.066, 0.009, 0.045, C(60, 45, 30));
            Brows(c, head, 0.19, 0.58);
            Cheeks(c, head, 0.19, 0.78, -0.15, 0.04);
            Smile(c, head, 0.19, -0.3, 0.3, 0.011, C(70, 40, 30));
            if (lady && o.Hair)
            {
                c.Col(C(205, 205, 212)); c.Ellipsoid(0.03, 0.95, 0, 0.198, 0.16, 0.2, 16, 3, 0.3, 1);
                if (o.Bun)
                {
                    c.Col(C(225, 225, 230)); c.Sphere(-0.02, 1.13, 0, 0.075, 8, 5);
                    c.Col(o.Body); c.Ellipsoid(0.03, 1.10, 0.065, 0.025, 0.02, 0.03, 6, 3);
                }
            }
            if (!lady)
            {
                c.Col(White);
                for (int s = -1; s <= 1; s += 2)
                {
                    c.Push(Mul(T(On(head, s * 0.17, -0.2, 0.19)), M4.RotX(s * 0.35)));
                    c.Ellipsoid(0, 0, 0, 0.035, 0.025, 0.06, 6, 4);
                    c.Pop();
                }
            }
            return head;
        }

        /// <summary>Both sleeves from the shoulders to the given hand positions, with the hands.</summary>
        static void TeacherArms(MeshBuilder c, Color sleeve, V3 left, V3 right)
        {
            foreach (var pr in new[] { new[] { ShoulderL, left }, new[] { ShoulderR, right } })
            {
                var end = pr[1] + (pr[0] - pr[1]).Normalized() * 0.035;
                c.Col(sleeve); Limb(c, pr[0], end, 0.06, 8, 4);
                c.Col(OldSkin); Sph(c, pr[1], 0.05, 6, 4);
            }
        }

        /// <summary>Teacher of subject i (0..9): even = lady, odd = gentleman; outfit and hand prop show the subject.</summary>
        static void Teacher(Model m, int i)
        {
            switch (i)
            {
                case 0: TeacherMaths(m); break;
                case 1: TeacherLetters(m); break;
                case 2: TeacherNature(m); break;
                case 3: TeacherGeography(m); break;
                case 4: TeacherEnglish(m); break;
                case 5: TeacherLogic(m); break;
                case 6: TeacherTime(m); break;
                case 7: TeacherArt(m); break;
                case 8: TeacherSport(m); break;
                default: TeacherScience(m); break;
            }
        }

        static Color SubjectColor(int i) => School.Subjects[i].Color;

        /// <summary>Maths lady: blue cardigan, pearls, a pencil behind the ear and a big wooden set-square with marks.</summary>
        static void TeacherMaths(Model m)
        {
            var blue = SubjectColor(0);
            TeacherBody(m, true, new Outfit { Body = blue, Sleeve = blue });
            var c = m.Plain;
            c.Col(C(250, 248, 240));
            for (int k = 0; k < 7; k++)
            {
                double a = -1.3 + 2.6 * k / 6;
                c.Sphere(0.03 + 0.125 * Math.Cos(a), 0.735 - 0.03 * Math.Cos(a), 0.13 * Math.Sin(a), 0.018, 5, 3);
            }
            var hand = P(0.21, 0.50, 0.24);
            TeacherArms(c, blue, HangL, hand);
            // yellow pencil resting behind the right ear
            c.Col(C(250, 205, 50)); c.Rod(P(-0.06, 0.975, 0.205), P(0.10, 0.958, 0.205), 0.012, 6);
            c.Col(C(240, 200, 150)); Cone(c, P(0.10, 0.958, 0.205), P(0.135, 0.954, 0.205), 0.012, 0.003, 6);
            c.Col(C(60, 60, 60)); Cone(c, P(0.128, 0.955, 0.205), P(0.14, 0.954, 0.205), 0.004, 0.001, 4);
            c.Col(C(190, 190, 200)); c.Rod(P(-0.075, 0.977, 0.205), P(-0.06, 0.975, 0.205), 0.0128, 6);
            c.Col(C(240, 130, 150)); c.Rod(P(-0.095, 0.979, 0.205), P(-0.075, 0.977, 0.205), 0.012, 6);
            // big wooden set-square held by the hypotenuse, facing the class
            var tri = new[] { 0.30, 0.02, 0.30, 0.46, 0.74, 0.46 };
            c.Col(C(232, 188, 118)); TriFrame(c, tri, 0.45, 0.25, 0.268, MapFront);
            c.Col(C(95, 60, 30));
            for (int k = 0; k <= 10; k++)
            {
                double len = k % 5 == 0 ? 0.04 : 0.022;
                double z = 0.44 - k * 0.039;
                c.Box(0.2695, 0.30 + len / 2 + 0.004, z, 0.003, len, 0.005);
                double y = 0.32 + k * 0.038;
                if (y < 0.72) c.Box(0.2695, y, 0.46 - len / 2 - 0.004, 0.003, 0.005, len);
            }
            c.Col(OldSkin); Sph(c, P(0.245, 0.52, 0.235), 0.022, 5, 3);   // thumb over the edge
        }

        /// <summary>Letters gentleman: red waistcoat over a white shirt, black bow tie, dark beret, open book and a white quill.</summary>
        static void TeacherLetters(Model m)
        {
            var red = SubjectColor(1);
            var shirt = C(248, 246, 240);
            var head = TeacherBody(m, false, new Outfit { Body = red, Sleeve = shirt, Buttons = Gold });
            var c = m.Plain;
            c.Col(shirt); c.Ellipsoid(0.08, 0.725, 0, 0.07, 0.04, 0.10, 8, 3);
            BowTie(c, P(0.125, 0.74, 0), C(30, 30, 35), C(15, 15, 20));
            // waistcoat pocket slits
            c.Col(Shade(red, -50));
            for (int s = -1; s <= 1; s += 2)
            {
                PushOnBody(c, 0.36, s * 0.17, 0.002);
                c.Box(0, 0, 0, 0.004, 0.008, 0.05);
                c.Pop();
            }
            Beret(c, head, C(45, 40, 60));
            var lh = P(0.20, 0.50, -0.16);
            var rh = P(0.21, 0.50, 0.24);
            TeacherArms(c, shirt, lh, rh);
            OpenBook(m, P(0.29, 0.56, -0.13), 1.0);
            Quill(c, P(0.22, 0.44, 0.245), P(0.31, 0.80, 0.30), 0.045);
        }

        /// <summary>Nature lady: green vest with a leaf brooch, straw sun hat with a flower, a magnifying glass.</summary>
        static void TeacherNature(Model m)
        {
            var green = SubjectColor(2);
            var blouse = C(250, 246, 232);
            var head = TeacherBody(m, true, new Outfit { Body = green, Sleeve = blouse, Bun = false, Buttons = C(150, 105, 55) });
            var c = m.Plain;
            // leaf brooch on the left side of the vest
            PushOnBody(c, 0.6, -0.165, 0.006);
            c.Push(M4.RotX(0.6));
            c.Col(C(130, 210, 80)); c.Ellipsoid(0, 0, 0, 0.01, 0.045, 0.024, 8, 4);
            c.Col(C(60, 130, 40)); c.Box(0.009, 0, 0, 0.004, 0.08, 0.004);
            c.Pop();
            c.Pop();
            StrawHat(c, head);
            var rh = P(0.22, 0.58, 0.27);
            TeacherArms(c, blouse, HangL, rh);
            // magnifying glass
            c.Col(C(120, 75, 40)); c.Rod(rh + P(-0.005, -0.06, 0), rh + P(0.02, 0.12, 0.03), 0.014, 8);
            c.Col(Gold); c.Rod(rh + P(0.02, 0.12, 0.03), rh + P(0.025, 0.145, 0.035), 0.017, 8);
            var rc = rh + P(0.03, 0.235, 0.05);
            c.Col(C(70, 60, 55)); c.Torus(rc.X, rc.Y, rc.Z, 0.085, 0.012, 20, 5);
            c.Col(C(195, 228, 248));
            c.Push(Mul(T(rc), M4.RotZ(-PI / 2)));
            c.Cylinder(0, -0.004, 0, 0.085, 0.085, 0.004, 18);
            c.Pop();
            c.Col(White); c.Ellipsoid(rc.X + 0.006, rc.Y + 0.035, rc.Z + 0.03, 0.003, 0.02, 0.012, 6, 3);
        }

        /// <summary>Geography and history gentleman: khaki explorer jacket, pith helmet, compass on the neck, rolled map.</summary>
        static void TeacherGeography(Model m)
        {
            var khaki = C(195, 175, 115);
            var head = TeacherBody(m, false, new Outfit { Body = khaki, Sleeve = khaki, Buttons = C(110, 85, 50) });
            var c = m.Plain;
            // teal neckerchief (the subject colour)
            var teal = SubjectColor(3);
            c.Col(teal); Band(c, 0.02, 0.74, 0, 0.1, 0.1, 0.02);
            c.Push(Mul(T(0.115, 0.745, 0), M4.RotZ(0.4)));
            Prism(c, new[] { 0.0, -0.075, 0.0, 0.075, -0.13, 0.0 }, 0, 0.01, MapFront);
            c.Pop();
            // four flapped pockets and a belt
            foreach (double y in new[] { 0.34, 0.56 })
                for (int s = -1; s <= 1; s += 2)
                {
                    PushOnBody(c, y, s * 0.172, 0.0);
                    c.Col(Shade(khaki, -12)); c.Box(0, 0, 0, 0.02, 0.08, 0.07);
                    c.Col(Shade(khaki, -28)); c.Box(0.004, 0.03, 0, 0.022, 0.025, 0.076);
                    c.Col(C(110, 85, 50)); c.Sphere(0.014, 0.025, 0, 0.008, 4, 2);
                    c.Pop();
                }
            c.Col(C(110, 75, 40)); Band(c, 0, 0.27, 0, 0.17, 0.205, 0.022);
            c.Col(Gold); c.Box(BodyFront(0.27, 0) + 0.006, 0.27, 0, 0.012, 0.04, 0.05);
            PithHelmet(c, head);
            // compass on a cord
            var cp = P(0.21, 0.565, 0);
            c.Col(C(110, 80, 50));
            for (int s = -1; s <= 1; s += 2)
                Tube(c, new[] { P(0.0, 0.775, s * 0.085), P(0.1, 0.735, s * 0.075), P(0.18, 0.66, s * 0.04), cp + P(0, 0.058, s * 0.004) }, 0.005, 4);
            c.Col(C(200, 160, 60));
            c.Push(Mul(T(cp), M4.RotZ(-PI / 2)));
            c.Cylinder(0, 0, 0, 0.05, 0.05, 0.022, 14);
            c.Pop();
            c.Col(C(225, 185, 80)); c.Torus(cp.X + 0.022, cp.Y, cp.Z, 0.047, 0.006, 14, 4);
            c.Torus(cp.X, cp.Y + 0.058, cp.Z, 0.01, 0.003, 8, 3);
            c.Col(C(250, 248, 235)); DiscX(c, cp.X + 0.0225, cp.Y, cp.Z, 0.042, 14);
            c.Col(C(220, 40, 40)); Prism(c, new[] { cp.Y, cp.Z - 0.008, cp.Y, cp.Z + 0.008, cp.Y + 0.035, cp.Z }, cp.X + 0.023, cp.X + 0.026, MapFront);
            c.Col(C(40, 60, 150)); Prism(c, new[] { cp.Y, cp.Z - 0.008, cp.Y, cp.Z + 0.008, cp.Y - 0.035, cp.Z }, cp.X + 0.023, cp.X + 0.026, MapFront);
            var rh = P(0.20, 0.48, 0.23);
            TeacherArms(c, khaki, HangL, rh);
            Scroll(m, P(0.17, 0.28, 0.235), P(0.30, 0.80, 0.30), 0.045);
        }

        /// <summary>English lady: purple cardigan, hair bun, red scarf, red double-decker badge, red "ABC" dictionary.</summary>
        static void TeacherEnglish(Model m)
        {
            var purple = SubjectColor(4);
            TeacherBody(m, true, new Outfit { Body = purple, Sleeve = purple });
            var c = m.Plain;
            var red = C(215, 40, 45);
            c.Col(red); Band(c, 0.02, 0.745, 0, 0.105, 0.105, 0.03);
            for (int k = 0; k < 2; k++)
            {
                c.Push(Mul(T(0.125 + k * 0.012, 0.72, -0.06 - k * 0.035), M4.RotZ(0.3 + k * 0.08), M4.RotX(0.12 - k * 0.2)));
                c.Col(red); c.Box(0, -0.085, 0, 0.022, 0.18, 0.055);
                c.Col(White); c.Box(0, -0.13, 0, 0.024, 0.012, 0.056); c.Box(0, -0.155, 0, 0.024, 0.012, 0.056);
                c.Col(red);
                for (int f = 0; f < 4; f++) c.Box(0, -0.183, -0.02 + f * 0.013, 0.008, 0.016, 0.004);
                c.Pop();
            }
            // red double-decker bus badge
            PushOnBody(c, 0.6, -0.165, 0.004);
            c.Col(red); c.Box(0, 0, 0, 0.012, 0.042, 0.066);
            c.Col(C(40, 50, 70)); c.Box(0.006, 0.009, 0.003, 0.003, 0.009, 0.054); c.Box(0.006, -0.006, 0.006, 0.003, 0.009, 0.046);
            c.Col(C(30, 30, 30)); c.Sphere(0.004, -0.021, 0.02, 0.008, 5, 3); c.Sphere(0.004, -0.021, -0.02, 0.008, 5, 3);
            c.Pop();
            var rh = P(0.21, 0.52, 0.24);
            TeacherArms(c, purple, HangL, rh);
            Dictionary(m, P(0.268, 0.60, 0.265));
        }

        /// <summary>Logic gentleman: orange sweater, black-and-white checkered vest and bow tie, a black chess knight.</summary>
        static void TeacherLogic(Model m)
        {
            var orange = SubjectColor(5);
            TeacherBody(m, false, new Outfit { Body = orange, Sleeve = orange, BellyTex = CheckerTex(), ShowButtons = false });
            var c = m.Plain;
            c.Col(Shade(orange, -25)); Band(c, 0.02, 0.73, 0, 0.11, 0.115, 0.025);
            var t = m.Get(CheckerTex());
            t.Col(White);
            for (int s = -1; s <= 1; s += 2) t.Ellipsoid(0.13, 0.745, s * 0.048, 0.022, 0.034, 0.048, 8, 5, -1, 1, 0.5f);
            c.Col(C(20, 20, 20)); c.Sphere(0.148, 0.745, 0, 0.02, 6, 3);
            var rh = P(0.21, 0.50, 0.23);
            TeacherArms(c, orange, HangL, rh);
            ChessKnight(c, P(0.235, 0.535, 0.245), 1.0, C(40, 40, 45), C(90, 90, 100));
        }

        /// <summary>Time and calendar lady: long brown dress with a lace collar, hourglass brooch, a gold pocket watch on a chain.</summary>
        static void TeacherTime(Model m)
        {
            var brown = SubjectColor(6);
            TeacherBody(m, true, new Outfit { Body = brown, Sleeve = brown, Buttons = Gold });
            var c = m.Plain;
            c.Col(brown); c.Cylinder(0.01, 0.06, 0, 0.27, 0.205, 0.40, 16);
            c.Col(Shade(brown, -28)); c.Cylinder(0.01, 0.06, 0, 0.272, 0.262, 0.10, 16, false);
            c.Col(C(250, 248, 240));
            for (int s = -1; s <= 1; s += 2)
            {
                c.Push(Mul(T(0.1, 0.715, s * 0.055), M4.RotX(s * 0.5)));
                c.Ellipsoid(0, 0, 0, 0.03, 0.03, 0.06, 8, 3);
                c.Pop();
            }
            // hourglass brooch
            PushOnBody(c, 0.6, -0.165, 0.004);
            c.Col(Gold); c.Box(0.004, 0.026, 0, 0.01, 0.006, 0.032); c.Box(0.004, -0.026, 0, 0.01, 0.006, 0.032);
            c.Col(C(220, 235, 245)); Cone(c, P(0.006, 0.023, 0), P(0.006, 0.001, 0), 0.012, 0.002, 6);
            c.Col(C(235, 190, 90)); Cone(c, P(0.006, -0.023, 0), P(0.006, -0.008, 0), 0.012, 0.005, 6);
            c.Col(C(220, 235, 245)); Cone(c, P(0.006, -0.008, 0), P(0.006, -0.001, 0), 0.005, 0.002, 6);
            c.Pop();
            var rh = P(0.22, 0.55, 0.25);
            TeacherArms(c, brown, HangL, rh);
            PocketWatch(m, P(0.27, 0.66, 0.30), rh);
        }

        /// <summary>Music and art gentleman: magenta smock with paint spots, floppy black bow, black beret, palette and brush.</summary>
        static void TeacherArt(Model m)
        {
            var mag = SubjectColor(7);
            var head = TeacherBody(m, false, new Outfit { Body = mag, Sleeve = mag, ShowButtons = false });
            var c = m.Plain;
            c.Col(mag); c.Cylinder(0, 0.13, 0, 0.255, 0.205, 0.40, 16);
            var spots = new[] { new[] { 0.22, -0.5 }, new[] { 0.30, 0.4 }, new[] { 0.18, 0.9 }, new[] { 0.34, -1.0 }, new[] { 0.25, 0.05 } };
            var spotCols = new[] { C(60, 120, 230), C(250, 210, 40), C(70, 190, 90), C(250, 250, 250), C(240, 120, 30) };
            for (int k = 0; k < spots.Length; k++)
            {
                double y = spots[k][0], a = spots[k][1], r = 0.255 - 0.05 * (y - 0.13) / 0.27 + 0.002;
                c.Col(spotCols[k]);
                c.Push(Mul(T(Math.Cos(a) * r, y, Math.Sin(a) * r), M4.RotY(-a)));
                c.Ellipsoid(0, 0, 0, 0.006, 0.02 + 0.006 * (k % 2), 0.026, 6, 3);
                c.Pop();
            }
            // floppy artist's bow
            c.Col(C(25, 25, 30));
            for (int s = -1; s <= 1; s += 2)
            {
                c.Push(Mul(T(0.13, 0.725, s * 0.06), M4.RotX(s * 0.4)));
                c.Ellipsoid(0, 0, 0, 0.025, 0.045, 0.065, 8, 4);
                c.Pop();
                Limb(c, P(0.145, 0.71, s * 0.02), P(0.19, 0.6, s * 0.05), 0.017, 6, 3);
            }
            c.Sphere(0.15, 0.725, 0, 0.026, 6, 3);
            Beret(c, head, C(25, 25, 30));
            var lh = P(0.20, 0.50, -0.17);
            var rh = P(0.21, 0.50, 0.24);
            TeacherArms(c, mag, lh, rh);
            Palette(c, P(0.27, 0.54, -0.22), 1.0);
            // brush in the right hand
            c.Col(C(150, 95, 50)); c.Rod(rh + P(-0.02, -0.07, -0.005), rh + P(0.07, 0.19, 0.045), 0.008, 6);
            c.Col(C(200, 200, 210)); c.Rod(rh + P(0.07, 0.19, 0.045), rh + P(0.08, 0.22, 0.05), 0.01, 6);
            c.Col(C(50, 110, 230)); Cone(c, rh + P(0.08, 0.22, 0.05), rh + P(0.095, 0.265, 0.058), 0.011, 0.002, 6);
        }

        /// <summary>Health and sport lady: red tracksuit with white stripes, white headband, silver whistle on a lanyard, a ball.</summary>
        static void TeacherSport(Model m)
        {
            var red = SubjectColor(8);
            var head = TeacherBody(m, true, new Outfit { Body = red, Sleeve = red, Belly = false, ShowButtons = false, Legs = red, Feet = C(248, 248, 248) });
            var c = m.Plain;
            // trainers: red stripes and grey soles
            for (int s = -1; s <= 1; s += 2)
            {
                c.Col(C(150, 150, 155)); c.Ellipsoid(0.07, 0.012, s * 0.10, 0.104, 0.014, 0.073, 8, 2);
                c.Col(red); c.Box(0.08, 0.045, s * 0.165, 0.08, 0.012, 0.006);
            }
            // zip, collar and side stripes
            c.Col(C(235, 235, 240));
            var zip = new List<V3>();
            foreach (double y in new[] { 0.74, 0.66, 0.56, 0.46, 0.36, 0.26, 0.17 }) zip.Add(P(BodyFront(y, 0) + 0.003, y, 0));
            Tube(c, zip.ToArray(), 0.006, 4);
            c.Col(Shade(red, -20)); Band(c, 0.02, 0.745, 0, 0.105, 0.11, 0.03);
            c.Col(White);
            for (int s = -1; s <= 1; s += 2)
            {
                var st = new List<V3>();
                foreach (double y in new[] { 0.70, 0.60, 0.48, 0.36, 0.25, 0.17 })
                {
                    double z = s * 0.205;
                    st.Add(P(BodyFront(y, z) + 0.004, y, z));
                }
                Tube(c, st.ToArray(), 0.009, 4);
                c.Rod(P(0.06, 0.04 + 0.03, s * 0.163), P(0.0, 0.19, s * 0.163), 0.008, 4);
            }
            // white headband
            c.Col(White); Hoop(c, 0.035, 0.955, 0, 0.205, 0.022, 18, 5);
            // lanyard and silver whistle
            var wp = P(BodyFront(0.56, 0.05) + 0.022, 0.56, 0.05);
            c.Col(C(40, 80, 200));
            for (int s = -1; s <= 1; s += 2)
                Tube(c, new[] { P(0.0, 0.78, s * 0.09), P(0.11, 0.73, s * 0.08), P(0.17, 0.64, 0.05 + s * 0.02), wp + P(0, 0.02, 0) }, 0.005, 4);
            c.Col(C(205, 210, 220));
            c.Ellipsoid(wp.X, wp.Y, wp.Z, 0.026, 0.022, 0.022, 8, 4);
            c.Box(wp.X + 0.005, wp.Y + 0.006, wp.Z + 0.03, 0.02, 0.014, 0.04);
            c.Torus(wp.X, wp.Y + 0.022, wp.Z, 0.008, 0.003, 8, 3);
            c.Col(C(40, 40, 50)); c.Box(wp.X + 0.017, wp.Y + 0.013, wp.Z + 0.006, 0.002, 0.008, 0.012);
            var rh = P(0.20, 0.50, 0.24);
            TeacherArms(c, red, HangL, rh);
            c.Col(White);
            foreach (var sh in new[] { ShoulderL, ShoulderR })
            {
                double s = Math.Sign(sh.Z);
                var hand = s < 0 ? HangL : rh;
                c.Rod(sh + P(0, 0.02, s * 0.055), hand + (sh - hand).Normalized() * 0.07 + P(0, 0, s * 0.05), 0.009, 4);
            }
            var ball = m.Get(BallTex());
            ball.Col(White); ball.Ellipsoid(0.245, 0.615, 0.265, 0.105, 0.105, 0.105, 16, 10);
        }

        /// <summary>Science and space gentleman: white lab coat, goggles on the forehead, wild white hair, a flask of green liquid.</summary>
        static void TeacherScience(Model m)
        {
            var coat = C(246, 246, 250);
            var navy = SubjectColor(9);
            var head = TeacherBody(m, false, new Outfit { Body = coat, Sleeve = coat, Buttons = C(190, 190, 200) });
            var c = m.Plain;
            c.Col(coat); c.Cylinder(0, 0.12, 0, 0.24, 0.205, 0.40, 16);
            c.Col(C(228, 228, 236));
            for (int s = -1; s <= 1; s += 2)
            {
                c.Push(Mul(T(BodyFront(0.62, s * 0.11) + 0.004, 0.62, s * 0.11), M4.RotX(s * 0.35)));
                c.Ellipsoid(0, 0, 0, 0.015, 0.085, 0.03, 6, 3);
                c.Pop();
            }
            // navy tie on the belly plate
            c.Col(navy);
            c.Sphere(0.13, 0.725, 0, 0.022, 6, 3);
            foreach (double y in new[] { 0.68, 0.62, 0.56, 0.50 })
                c.Ellipsoid(BellyFront(y, 0) + 0.004, y, 0, 0.007, 0.04, 0.018 + (0.68 - y) * 0.06, 6, 3);
            // breast pocket with pens
            PushOnBody(c, 0.56, -0.17, 0.0);
            c.Col(C(230, 230, 236)); c.Box(0, 0, 0, 0.016, 0.07, 0.07);
            c.Col(C(40, 80, 200)); c.Rod(P(0, 0.02, -0.015), P(0, 0.07, -0.015), 0.006, 5);
            c.Col(C(210, 40, 40)); c.Rod(P(0, 0.02, 0.012), P(0, 0.065, 0.012), 0.006, 5);
            c.Pop();
            // safety goggles pushed up on the forehead
            c.Col(C(70, 75, 85)); Hoop(c, 0.035, 1.0, 0, 0.165, 0.014, 18, 4);
            for (int s = -1; s <= 1; s += 2)
            {
                var d = Dir(s * 0.34, 0.72);
                var p = On(head, s * 0.34, 0.72, 0.19);
                c.Col(C(70, 75, 85)); Cone(c, p - d * 0.01, p + d * 0.022, 0.05, 0.05, 12);
                c.Col(C(165, 225, 240)); Cone(c, p + d * 0.022, p + d * 0.03, 0.042, 0.042, 12);
            }
            // wild white hair tufts
            c.Col(White);
            var tufts = new[] { new[] { 1.25, 0.15 }, new[] { 1.55, 0.45 }, new[] { 1.9, 0.2 }, new[] { 2.3, 0.5 }, new[] { 2.6, 0.15 }, new[] { 2.0, 0.75 } };
            foreach (var tf in tufts)
                for (int s = -1; s <= 1; s += 2)
                {
                    Limb(c, On(head, s * tf[0], tf[1], 0.12), On(head, s * tf[0], tf[1] + 0.1, 0.25), 0.04, 6, 3);
                    Sph(c, On(head, s * tf[0] + s * 0.15, tf[1] - 0.12, 0.2), 0.035, 6, 3);
                }
            Limb(c, On(head, PI, 0.3, 0.12), On(head, PI, 0.45, 0.25), 0.05, 6, 3);
            var rh = P(0.22, 0.56, 0.25);
            TeacherArms(c, coat, HangL, rh);
            Flask(c, P(0.285, 0.535, 0.29), 1.0, C(110, 250, 80));
        }

        // ------------------------------------------------------------------ teacher accessories

        static void BowTie(MeshBuilder c, V3 p, Color col, Color knot)
        {
            c.Col(col);
            for (int s = -1; s <= 1; s += 2) c.Ellipsoid(p.X, p.Y, p.Z + s * 0.045, 0.02, 0.032, 0.045, 6, 4);
            c.Col(knot); c.Sphere(p.X + 0.015, p.Y, p.Z, 0.02, 6, 3);
        }

        /// <summary>Soft beret slightly tilted to the side, with a little stalk.</summary>
        static void Beret(MeshBuilder c, V3 head, Color col)
        {
            c.Col(col);
            c.Push(Mul(T(head.X - 0.01, head.Y + 0.165, head.Z - 0.02), M4.RotX(-0.18), M4.RotZ(0.12)));
            c.Ellipsoid(0, 0, 0, 0.215, 0.065, 0.225, 14, 5);
            c.Rod(P(0, 0.05, 0), P(0, 0.085, 0), 0.01, 5);
            c.Pop();
        }

        /// <summary>Wide straw sun hat with a green band and a pink flower.</summary>
        static void StrawHat(MeshBuilder c, V3 head)
        {
            var straw = C(235, 205, 130);
            c.Push(Mul(T(head.X - 0.01, head.Y + 0.14, head.Z), M4.RotZ(0.14)));
            c.Col(straw); c.Cylinder(0, -0.008, 0, 0.34, 0.33, 0.008, 22);
            c.Col(Shade(straw, -10)); Annulus(c, 0, 0.0085, 0, 0.17, 0.33, 22);
            c.Col(Shade(straw, -18)); c.Cylinder(0, 0, 0, 0.168, 0.152, 0.13, 16);
            c.Col(C(90, 170, 80)); c.Cylinder(0, 0.004, 0, 0.172, 0.167, 0.045, 16, false);
            double a = 1.2;
            var fc = P(Math.Cos(a) * 0.178, 0.028, Math.Sin(a) * 0.178);
            var tan = P(-Math.Sin(a), 0, Math.Cos(a));
            c.Col(C(250, 120, 165));
            for (int k = 0; k < 5; k++)
            {
                double t = 2 * PI * k / 5;
                Sph(c, fc + P(0, Math.Sin(t) * 0.024, 0) + tan * (Math.Cos(t) * 0.024), 0.019, 6, 3);
            }
            c.Col(C(255, 215, 60)); Sph(c, fc + P(Math.Cos(a) * 0.01, 0, Math.Sin(a) * 0.01), 0.014, 6, 3);
            c.Pop();
        }

        /// <summary>Beige pith (safari) helmet with a brown band.</summary>
        static void PithHelmet(MeshBuilder c, V3 head)
        {
            var beige = C(228, 210, 160);
            c.Push(Mul(T(head.X - 0.01, head.Y + 0.13, head.Z), M4.RotZ(0.1)));
            c.Col(beige); c.Ellipsoid(0, 0, 0, 0.215, 0.17, 0.225, 14, 6, 0, 1);
            c.Col(Shade(beige, -14)); c.Ellipsoid(0, 0, 0, 0.31, 0.028, 0.30, 18, 4);
            c.Col(C(140, 110, 70)); c.Cylinder(0, 0, 0, 0.228, 0.222, 0.04, 16, false);
            c.Col(beige); c.Sphere(0, 0.172, 0, 0.022, 6, 3);
            c.Pop();
        }

        /// <summary>Open book: two halves in a shallow V facing +x and up, with lines of text; s scales it.</summary>
        static void OpenBook(Model m, V3 ctr, double s)
        {
            var c = m.Plain;
            m.Push(Mul(T(ctr), M4.RotZ(0.55), S(s, s, s)));
            for (int sd = -1; sd <= 1; sd += 2)
            {
                m.Push(M4.RotY(sd * 0.28));
                c.Col(C(120, 40, 30)); c.Box(-0.006, 0, sd * 0.085, 0.008, 0.25, 0.17);
                c.Col(C(250, 244, 225)); c.Box(0.006, 0, sd * 0.08, 0.016, 0.235, 0.155);
                c.Col(C(120, 115, 110));
                for (int k = 0; k < 8; k++) c.Box(0.0145, 0.09 - k * 0.026, sd * 0.08, 0.002, 0.006, k == 7 ? 0.07 : 0.12);
                m.Pop();
            }
            c.Col(C(200, 40, 40)); c.Box(0.016, 0.02, 0.0, 0.003, 0.2, 0.008);   // ribbon bookmark
            m.Pop();
        }

        /// <summary>White quill pen from the nib to the top end; w = width of the vane.</summary>
        static void Quill(MeshBuilder c, V3 nib, V3 top, double w)
        {
            var d = top - nib;
            c.Col(C(40, 40, 55)); Cone(c, nib, nib + d * 0.08, 0.002, 0.007, 5);
            c.Col(C(235, 228, 210)); c.Rod(nib + d * 0.08, top, 0.006, 5);
            var a = nib + d * 0.3;
            c.Col(C(250, 250, 248));
            c.Push(Mul(T((a + top) * 0.5), AlongY(d)));
            c.Ellipsoid(0, 0, 0, w, (top - a).Length / 2, 0.008, 10, 6);
            c.Pop();
        }

        /// <summary>Rolled map: parchment scroll with a map printed on it, darker ends and a red ribbon.</summary>
        static void Scroll(Model m, V3 from, V3 to, double r)
        {
            var d = (to - from).Normalized();
            var t = m.Get(MapTex());
            t.Col(White); Cone(t, from, to, r, r, 14, false);
            var c = m.Plain;
            c.Col(C(205, 180, 125)); Cone(c, from - d * 0.002, from, r, r, 14); Cone(c, to, to + d * 0.002, r, r, 14);
            c.Col(C(170, 140, 90)); Cone(c, from - d * 0.004, from - d * 0.002, r * 0.5, r * 0.5, 10); Cone(c, to + d * 0.002, to + d * 0.004, r * 0.5, r * 0.5, 10);
            c.Col(C(200, 35, 40));
            var mid = from + (to - from) * 0.55;
            c.Push(Mul(T(mid), AlongY(d), M4.RotZ(PI / 2)));
            c.Torus(0, 0, 0, r + 0.004, 0.008, 14, 4);
            c.Pop();
        }

        /// <summary>Thick red dictionary with "ABC" on the cover facing +x.</summary>
        static void Dictionary(Model m, V3 ctr)
        {
            var c = m.Plain;
            double x = ctr.X, y = ctr.Y, z = ctr.Z;
            var red = C(200, 35, 40);
            c.Col(C(248, 242, 225)); c.Box(x, y, z + 0.005, 0.045, 0.24, 0.17);
            c.Col(red);
            c.Box(x + 0.025, y, z, 0.008, 0.25, 0.18);
            c.Box(x - 0.025, y, z, 0.008, 0.25, 0.18);
            c.Box(x, y, z - 0.087, 0.058, 0.25, 0.008);
            var t = m.Get(DictTex());
            t.Col(White); FaceX(t, x + 0.0292, y, z, 0.18, 0.25);
        }

        /// <summary>Chess knight with its head in profile (facing +z), base at p; s = scale (1 ≈ 0.22 tall).</summary>
        static void ChessKnight(MeshBuilder c, V3 p, double s, Color col, Color hi, bool low = false)
        {
            int q = low ? 8 : 12;
            c.Col(col);
            c.Cylinder(p.X, p.Y, p.Z, 0.062 * s, 0.056 * s, p.Y + 0.022 * s, q);
            c.Cylinder(p.X, p.Y + 0.022 * s, p.Z, 0.05 * s, 0.042 * s, p.Y + 0.04 * s, q);
            c.Col(hi); c.Cylinder(p.X, p.Y + 0.04 * s, p.Z, 0.046 * s, 0.046 * s, p.Y + 0.05 * s, q);
            c.Col(col);
            Limb(c, p + P(0, 0.055, -0.012) * s, p + P(0, 0.14, 0.012) * s, 0.038 * s, low ? 6 : 8, low ? 3 : 4);
            c.Push(Mul(T(p + P(0, 0.16, 0.035) * s), M4.RotX(0.6)));
            c.Ellipsoid(0, 0, 0, 0.031 * s, 0.034 * s, 0.06 * s, low ? 6 : 8, low ? 4 : 5);
            c.Pop();
            c.Ellipsoid(p.X, p.Y + 0.125 * s, p.Z + 0.075 * s, 0.026 * s, 0.022 * s, 0.03 * s, low ? 5 : 6, low ? 2 : 3);
            for (int k = -1; k <= 1; k += 2)
                Cone(c, p + P(k * 0.014, 0.185, 0.0) * s, p + P(k * 0.016, 0.225, -0.012) * s, 0.012 * s, 0.002 * s, 5);
            c.Col(hi);
            for (int k = 0; k < 5; k += low ? 2 : 1) Sph(c, p + P(0, 0.09 + k * 0.025, -0.035 - 0.006 * Math.Sin(k)) * s, 0.014 * s, low ? 4 : 5, low ? 2 : 3);
            if (low) return;
            c.Col(C(230, 230, 230));
            for (int k = -1; k <= 1; k += 2) Sph(c, p + P(k * 0.028, 0.168, 0.03) * s, 0.007 * s, 4, 2);
        }

        /// <summary>Gold pocket watch facing +x (clock face texture) with a chain running to the hand.</summary>
        static void PocketWatch(Model m, V3 ctr, V3 hand)
        {
            var c = m.Plain;
            var gold = C(240, 195, 60);
            c.Col(gold);
            c.Push(Mul(T(ctr), M4.RotZ(-PI / 2)));
            c.Cylinder(0, -0.015, 0, 0.09, 0.09, 0.012, 18);
            c.Pop();
            c.Col(C(255, 220, 100)); c.Torus(ctr.X + 0.012, ctr.Y, ctr.Z, 0.088, 0.01, 18, 5);
            var f = m.Get(ClockFaceTex());
            f.Col(White); DiscX(f, ctr.X + 0.0125, ctr.Y, ctr.Z, 0.08, 20);
            c.Col(gold);
            c.Cylinder(ctr.X, ctr.Y + 0.085, ctr.Z, 0.016, 0.016, ctr.Y + 0.108, 8);
            c.Sphere(ctr.X, ctr.Y + 0.114, ctr.Z, 0.013, 6, 3);
            c.Torus(ctr.X, ctr.Y + 0.137, ctr.Z, 0.02, 0.005, 10, 4);
            Tube(c, new[] { ctr + P(0, 0.155, 0), ctr + P(-0.03, 0.15, -0.06), hand + P(0.0, 0.07, 0.0), hand + P(0.03, -0.02, 0.02), hand + P(0.01, -0.11, -0.02), hand + P(-0.03, -0.07, -0.08) }, 0.005, 4);
        }

        /// <summary>Wooden paint palette facing +x with a thumb hole and colour blobs.</summary>
        static void Palette(MeshBuilder c, V3 ctr, double s)
        {
            c.Push(Mul(T(ctr), M4.RotZ(0.3), S(s, s, s)));
            c.Col(C(215, 170, 110)); c.Ellipsoid(0, 0, 0, 0.012, 0.12, 0.17, 16, 4);
            c.Col(C(195, 150, 95)); c.Ellipsoid(0, -0.05, 0.1, 0.0125, 0.06, 0.07, 10, 3);
            c.Col(C(70, 45, 30)); FlatOval(c, 0.013, -0.02, 0.07, 0.02, 0.022, 0, 8);
            var blobs = new[] { C(230, 40, 40), C(255, 210, 40), C(50, 110, 230), C(70, 190, 80), C(250, 250, 250), C(150, 60, 190), C(250, 140, 40) };
            for (int k = 0; k < blobs.Length; k++)
            {
                double a = 0.5 + k * 0.62;
                c.Col(blobs[k]); c.Ellipsoid(0.012, Math.Sin(a) * 0.085, Math.Cos(a) * 0.12, 0.01, 0.02, 0.024, 6, 3);
            }
            c.Pop();
        }

        /// <summary>Erlenmeyer flask standing at base (glass with coloured liquid and bubbles); s = scale (1 ≈ 0.22 tall).</summary>
        static void Flask(MeshBuilder c, V3 b, double s, Color liquid)
        {
            var glass = C(215, 238, 246);
            c.Col(glass);
            c.Cylinder(b.X, b.Y, b.Z, 0.08 * s, 0.026 * s, b.Y + 0.15 * s, 14);
            c.Cylinder(b.X, b.Y + 0.15 * s, b.Z, 0.026 * s, 0.026 * s, b.Y + 0.205 * s, 10, false);
            c.Cylinder(b.X, b.Y + 0.2 * s, b.Z, 0.032 * s, 0.032 * s, b.Y + 0.215 * s, 10, false);
            c.Col(liquid); c.Cylinder(b.X, b.Y + 0.004 * s, b.Z, 0.082 * s, 0.057 * s, b.Y + 0.07 * s, 14);
            c.Col(Lerp(liquid, White, 0.45));
            Annulus(c, b.X, b.Y + 0.0705 * s, b.Z, 0, 0.056 * s, 14);
            Sph(c, P(b.X, b.Y + 0.24 * s, b.Z), 0.012 * s, 6, 3);
            Sph(c, P(b.X + 0.01 * s, b.Y + 0.28 * s, b.Z - 0.01 * s), 0.009 * s, 6, 3);
            Sph(c, P(b.X - 0.004 * s, b.Y + 0.315 * s, b.Z + 0.006 * s), 0.007 * s, 6, 3);
            c.Col(White); c.Ellipsoid(b.X + 0.05 * s, b.Y + 0.1 * s, b.Z + 0.02 * s, 0.004 * s, 0.03 * s, 0.008 * s, 5, 3);
        }

        // ================================================================== maths

        /// <summary>Geometry solids (cube, pyramid, cylinder, sphere, cone) on a low table with a white cloth.</summary>
        static void MathSolids(Model m)
        {
            double top = Table(m, 0.62, 1.08, 0.5);
            var c = m.Plain;
            c.Col(C(250, 250, 245)); c.Box(0, top + 0.004, 0, 0.56, 0.008, 1.02);
            c.Col(C(70, 120, 210)); c.Box(0.281, top - 0.03, 0, 0.004, 0.07, 1.02);
            double y = top + 0.008;
            c.Col(C(225, 60, 60)); c.Box(0.02, y + 0.09, -0.38, 0.18, 0.18, 0.18);
            c.Col(C(250, 200, 50)); Pyramid(c, 0.0, y, -0.17, 0.11, 0.24);
            c.Col(C(70, 175, 90)); c.Cylinder(0.02, y, 0.03, 0.085, 0.085, y + 0.22, 18);
            c.Col(C(70, 120, 225)); c.Sphere(0.0, y + 0.105, 0.22, 0.105, 18, 10);
            c.Col(C(245, 140, 40)); c.Cylinder(0.02, y, 0.40, 0.09, 0, y + 0.26, 18);
            // little name cards in front of the solids
            c.Col(White);
            foreach (double z in new[] { -0.38, -0.17, 0.03, 0.22, 0.40 })
            {
                c.Push(Mul(T(0.2, y, z), M4.RotZ(0.35)));
                c.Box(0, 0.02, 0, 0.004, 0.04, 0.12);
                c.Pop();
            }
        }

        /// <summary>Giant wooden ruler, protractor and blackboard compass leaning on a felt board stand.</summary>
        static void MathRuler(Model m)
        {
            var w = m.Get(WoodTex());
            var c = m.Plain;
            w.Col(C(190, 135, 80)); w.Box(0.02, 0.025, 0, 0.45, 0.05, 1.08);
            w.Col(C(170, 120, 70)); w.Box(0.22, 0.07, 0, 0.03, 0.04, 1.08);
            m.Push(Mul(T(0.08, 0.05, 0), M4.RotZ(0.12)));
            w.Col(C(160, 110, 65)); w.Box(-0.045, 0.62, 0, 0.03, 1.2, 1.04);
            c.Col(C(70, 120, 90)); c.Box(-0.029, 0.62, 0, 0.004, 1.12, 0.96);
            // the ruler
            w.Col(C(245, 205, 120)); w.Box(-0.014, 0.58, -0.30, 0.024, 1.1, 0.14);
            var rt = m.Get(RulerTex());
            rt.Col(White); FaceX(rt, -0.0015, 0.58, -0.30, 0.14, 1.1);
            // the protractor standing on its flat edge
            var half = new double[19 * 2];
            for (int i = 0; i <= 18; i++)
            {
                half[i * 2] = 0.02 + Math.Sin(PI * i / 18) * 0.29;
                half[i * 2 + 1] = 0.21 + Math.Cos(PI * i / 18) * 0.29;
            }
            c.Col(C(190, 220, 245)); Prism(c, half, -0.026, -0.012, MapFront);
            var pt = m.Get(ProtractorTex());
            pt.Col(White); PolyX(pt, -0.0115, half, 0.02 + 0.145, 0.21, 0.145, 0.29);
            // big blackboard compass
            c.Col(C(150, 100, 55));
            var hinge = P(-0.01, 1.08, -0.06);
            c.Rod(hinge, P(-0.01, 0.58, -0.15), 0.012, 6);
            c.Rod(hinge, P(-0.01, 0.58, 0.02), 0.012, 6);
            c.Col(Metal); c.Sphere(hinge.X, hinge.Y, hinge.Z, 0.025, 8, 4); c.Rod(hinge, hinge + P(0, 0.06, 0), 0.008, 5);
            c.Col(C(240, 240, 240)); Cone(c, P(-0.01, 0.58, 0.02), P(-0.01, 0.53, 0.03), 0.012, 0.006, 6);
            c.Col(C(80, 80, 85)); Cone(c, P(-0.01, 0.58, -0.15), P(-0.01, 0.54, -0.16), 0.008, 0.001, 5);
            c.Col(C(250, 240, 120)); c.Box(-0.01, 0.75, -0.06, 0.012, 0.012, 0.14);
            m.Pop();
            // back struts
            c.Col(C(150, 100, 60));
            for (int s = -1; s <= 1; s += 2) c.Rod(P(-0.07, 0.94, s * 0.42), P(-0.36, 0.0, s * 0.42), 0.018, 6);
        }

        /// <summary>Big standing abacus: wooden frame, seven wires of ten coloured beads.</summary>
        static void MathAbacus(Model m)
        {
            var w = m.Get(WoodTex());
            var c = m.Plain;
            w.Col(C(205, 145, 85));
            for (int s = -1; s <= 1; s += 2)
            {
                w.Box(0, 0.03, s * 0.52, 0.45, 0.06, 0.09);
                w.Box(0, 0.66, s * 0.52, 0.07, 1.2, 0.07);
                c.Col(Gold); c.Sphere(0, 1.29, s * 0.52, 0.04, 8, 4);
            }
            w.Box(0, 1.22, 0, 0.08, 0.08, 1.1);
            w.Box(0, 0.36, 0, 0.08, 0.06, 1.1);
            w.Col(C(180, 120, 70)); w.Box(0, 0.2, 0, 0.04, 0.04, 1.0);
            var rows = new[] { C(230, 50, 50), C(245, 140, 40), C(250, 210, 40), C(80, 180, 70), C(60, 170, 220), C(70, 90, 210), C(170, 80, 200) };
            for (int r = 0; r < rows.Length; r++)
            {
                double y = 0.46 + r * 0.11;
                c.Col(C(190, 190, 200)); c.Rod(P(0, y, -0.49), P(0, y, 0.49), 0.007, 4);
                int left = (r * 3 + 2) % 11;
                for (int k = 0; k < 10; k++)
                {
                    double z = k < left ? 0.44 - k * 0.075 : -0.44 + (9 - k) * 0.075;
                    c.Col(k % 5 == 4 ? Shade(rows[r], -45) : rows[r]);
                    c.Push(Mul(T(0, y, z), M4.RotX(PI / 2)));
                    c.Ellipsoid(0, 0, 0, 0.05, 0.036, 0.05, 6, 2);
                    c.Pop();
                }
            }
        }

        // ================================================================== letters

        /// <summary>Tall bookcase (1.3 high, 0.36 deep, back at x = -0.18) full of colourful books.</summary>
        static void LettersBookcase(Model m)
        {
            var w = m.Get(WoodTex());
            var c = m.Plain;
            const double D = 0.36, W = 1.0, H = 1.3;
            var wood = C(185, 130, 80);
            w.Col(wood);
            for (int s = -1; s <= 1; s += 2) w.Box(0, H / 2, s * (W / 2 - 0.02), D, H, 0.04);
            w.Box(0.005, H - 0.02, 0, D + 0.02, 0.04, W + 0.03);
            w.Col(Shade(wood, -20)); w.Box(0, 0.04, 0, D, 0.08, W - 0.04);
            w.Col(Shade(wood, -45)); w.Box(-D / 2 + 0.01, H / 2, 0, 0.02, H, W - 0.04);
            w.Col(wood);
            double[] shelves = { 0.08, 0.385, 0.69, 0.995 };
            for (int i = 1; i < 4; i++) w.Box(0, shelves[i] - 0.015, 0, D - 0.02, 0.03, W - 0.06);
            var covers = new[] { C(220, 60, 60), C(60, 110, 210), C(70, 170, 80), C(250, 200, 50), C(150, 80, 190), C(240, 130, 40), C(40, 160, 170), C(230, 110, 160), C(120, 80, 50), C(30, 60, 120) };
            var r = new Random(41);
            double fx = D / 2 - 0.03;
            for (int i = 0; i < 4; i++)
            {
                double yb = shelves[i], z = W / 2 - 0.045, end = -W / 2 + 0.045;
                if (i == 2)
                {
                    // a stack of books lying flat
                    for (int k = 0; k < 3; k++)
                    {
                        c.Col(covers[(k * 3 + 1) % covers.Length]); c.Box(fx - 0.12 + k * 0.004, yb + 0.02 + k * 0.04, z - 0.1, 0.24, 0.038, 0.19 - k * 0.01);
                        c.Col(C(248, 242, 225)); c.Box(fx - 0.118 + k * 0.004, yb + 0.02 + k * 0.04, z - 0.1, 0.236, 0.03, 0.185 - k * 0.01);
                    }
                    z -= 0.22;
                }
                while (true)
                {
                    double bw = 0.03 + r.NextDouble() * 0.03, bh = 0.17 + r.NextDouble() * 0.08, bd = 0.2 + r.NextDouble() * 0.06;
                    if (z - bw < end) break;
                    var col = covers[r.Next(covers.Length)];
                    double cz = z - bw / 2;
                    c.Col(col); c.Box(fx - bd / 2, yb + bh / 2, cz, bd, bh, bw - 0.003);
                    if (r.Next(2) == 0) { c.Col(r.Next(3) == 0 ? White : Gold); c.Box(fx + 0.001, yb + bh * 0.78, cz, 0.003, 0.014, bw - 0.008); }
                    z -= bw;
                    if (r.Next(10) == 0) z -= 0.025;
                }
            }
        }

        /// <summary>Old green typewriter with a sheet of paper on a small table; the keys face +x.</summary>
        static void LettersTypewriter(Model m)
        {
            double y = Table(m, 0.6, 0.75, 0.55);
            var c = m.Plain;
            var body = C(45, 85, 70);
            c.Col(Shade(body, -15)); c.Box(-0.02, y + 0.03, 0, 0.36, 0.06, 0.46);
            c.Col(body); c.Box(-0.08, y + 0.11, 0, 0.2, 0.1, 0.44);
            c.Col(Gold); c.Box(0.021, y + 0.13, 0, 0.004, 0.025, 0.14);
            // sloping keyboard
            m.Push(Mul(T(0.1, y + 0.07, 0), M4.RotZ(-0.3)));
            c.Col(body); c.Box(0, 0, 0, 0.2, 0.04, 0.44);
            for (int row = 0; row < 3; row++)
            {
                double lx = 0.06 - row * 0.05;
                c.Col(C(30, 30, 30)); c.Box(lx, 0.025, 0, 0.012, 0.01, 0.4);
                for (int k = 0; k < 9; k++)
                {
                    double lz = -0.17 + k * 0.042 + row * 0.012;
                    c.Col(C(240, 232, 210)); c.Cylinder(lx, 0.03, lz, 0.016, 0.016, 0.048, 8);
                }
            }
            c.Col(C(240, 232, 210)); c.Box(0.105, 0.035, 0, 0.022, 0.014, 0.22);
            m.Pop();
            // carriage with the black platen, knobs and a sheet of paper
            c.Col(C(170, 175, 180)); c.Box(-0.13, y + 0.165, 0, 0.1, 0.02, 0.58);
            c.Push(Mul(T(-0.13, y + 0.2, 0), M4.RotX(PI / 2)));
            c.Col(C(25, 25, 28)); c.Cylinder(0, -0.26, 0, 0.035, 0.035, 0.26, 14);
            c.Cylinder(0, 0.26, 0, 0.03, 0.03, 0.3, 12); c.Cylinder(0, -0.3, 0, 0.03, 0.03, -0.26, 12);
            c.Pop();
            c.Push(Mul(T(-0.135, y + 0.235, 0), M4.RotZ(0.25)));
            c.Col(White); c.Box(0, 0.11, 0, 0.003, 0.22, 0.3);
            c.Col(C(70, 70, 80));
            for (int k = 0; k < 6; k++) c.Box(0.002, 0.18 - k * 0.025, 0.02 - (k % 3) * 0.02, 0.001, 0.006, 0.2 - (k % 3) * 0.04);
            c.Pop();
            // ribbon spools and the return lever
            for (int s = -1; s <= 1; s += 2)
            {
                c.Col(C(40, 40, 40)); c.Cylinder(-0.04, y + 0.16, s * 0.15, 0.04, 0.04, y + 0.175, 12);
                c.Col(C(200, 40, 40)); c.Cylinder(-0.04, y + 0.175, s * 0.15, 0.03, 0.03, y + 0.18, 10);
            }
            c.Col(C(200, 205, 210));
            Tube(c, new[] { P(-0.13, y + 0.2, 0.3), P(-0.1, y + 0.24, 0.31), P(-0.04, y + 0.27, 0.33), P(0.0, y + 0.265, 0.33) }, 0.007, 5);
            // a little stack of paper
            c.Col(White); c.Box(-0.2, y + 0.01, -0.29, 0.16, 0.02, 0.12);
        }

        /// <summary>Marble pedestal with a big blue inkwell, a quill in it and a sheet of parchment.</summary>
        static void LettersInkwell(Model m)
        {
            double top = Pedestal(m, 0.8, 0.22, C(236, 230, 220));
            var c = m.Plain;
            c.Push(Mul(T(0.07, top + 0.003, -0.08), M4.RotY(0.3)));
            c.Col(C(240, 225, 180)); c.Box(0, 0, 0, 0.18, 0.006, 0.22);
            c.Col(C(110, 90, 70));
            for (int k = 0; k < 6; k++) c.Box(-0.06 + k * 0.025, 0.0035, 0, 0.004, 0.001, 0.16);
            c.Pop();
            var ink = C(40, 55, 135);
            double z = 0.06;
            c.Col(ink);
            c.Cylinder(0, top, z, 0.1, 0.1, top + 0.09, 18);
            c.Cylinder(0, top + 0.09, z, 0.1, 0.035, top + 0.13, 18, false);
            c.Cylinder(0, top + 0.13, z, 0.035, 0.035, top + 0.16, 12, false);
            Hoop(c, 0, top + 0.16, z, 0.038, 0.007, 12, 4);
            c.Col(C(10, 10, 20)); Annulus(c, 0, top + 0.155, z, 0, 0.034, 12);
            c.Col(C(150, 180, 240)); c.Ellipsoid(0.07, top + 0.05, z + 0.06, 0.004, 0.03, 0.012, 5, 3);
            c.Col(Gold); c.Box(0.098, top + 0.045, z, 0.006, 0.04, 0.06);
            Quill(c, P(0, top + 0.1, z), P(-0.12, top + 0.53, z + 0.12), 0.07);
        }

        // ================================================================== nature

        /// <summary>Fish facing dir (±1 along z), drawn slightly in front of the tank glass (x).</summary>
        static void Fish(MeshBuilder c, V3 p, double s, int dir, Color body, Color fin, Color? stripe)
        {
            c.Col(body); c.Ellipsoid(p.X, p.Y, p.Z, 0.012, 0.55 * s, s, 8, 4);
            c.Col(fin);
            var t0 = p + P(0, 0, -dir * s * 0.85);
            var tu = p + P(0.002, 0.55 * s, -dir * s * 1.6);
            var td = p + P(0.002, -0.55 * s, -dir * s * 1.6);
            Tri(c, t0, tu, td); Tri(c, t0, td, tu);
            var f0 = p + P(0, 0.45 * s, dir * 0.2 * s);
            var f1 = p + P(0, 0.85 * s, -dir * 0.3 * s);
            var f2 = p + P(0, 0.4 * s, -dir * 0.55 * s);
            Tri(c, f0, f1, f2); Tri(c, f0, f2, f1);
            if (stripe.HasValue)
            {
                c.Col(stripe.Value);
                for (int k = -1; k <= 1; k++)
                    c.Ellipsoid(p.X + 0.002, p.Y, p.Z + k * 0.45 * s, 0.0115, 0.5 * s * Math.Sqrt(1 - Sq(0.45 * k)), 0.08 * s, 4, 2);
            }
            c.Col(White); Sph(c, p + P(0.009, 0.12 * s, dir * 0.55 * s), 0.16 * s, 5, 3);
            c.Col(Pupil); Sph(c, p + P(0.014, 0.12 * s, dir * 0.6 * s), 0.09 * s, 4, 2);
        }

        /// <summary>Fish tank on a wooden cabinet: light blue water, sand, plants, pebbles, bubbles and fish in front of the glass.</summary>
        static void NatureAquarium(Model m)
        {
            var w = m.Get(WoodTex());
            var c = m.Plain;
            w.Col(C(130, 95, 70)); w.Box(0, 0.28, 0, 0.45, 0.56, 1.0);
            w.Col(C(160, 120, 85));
            for (int s = -1; s <= 1; s += 2)
            {
                w.Box(0.226, 0.28, s * 0.24, 0.006, 0.46, 0.44);
                c.Col(Gold); c.Sphere(0.235, 0.3, s * 0.04, 0.014, 6, 3);
            }
            // frame, water, sand, lid
            c.Col(C(30, 30, 35));
            c.Box(0, 0.58, 0, 0.42, 0.04, 0.96);
            c.Box(0, 1.045, 0, 0.42, 0.03, 0.96);
            for (int a = -1; a <= 1; a += 2)
                for (int b = -1; b <= 1; b += 2) c.Box(a * 0.2, 0.81, b * 0.47, 0.02, 0.46, 0.02);
            c.Col(C(130, 200, 236)); c.Box(0, 0.81, 0, 0.39, 0.44, 0.93);
            c.Col(C(205, 232, 246)); c.Box(0, 1.012, 0, 0.392, 0.03, 0.932);
            c.Col(C(232, 208, 150)); c.Box(0, 0.635, 0, 0.394, 0.07, 0.934);
            c.Col(C(28, 28, 32)); c.Box(0, 1.075, 0, 0.44, 0.03, 0.98);
            c.Col(C(250, 245, 200)); c.Box(0.221, 1.075, 0, 0.002, 0.012, 0.7);
            // pebbles
            var r = new Random(7);
            var peb = new[] { C(200, 90, 80), C(240, 240, 235), C(90, 130, 200), C(230, 190, 70), C(150, 150, 150) };
            for (int k = 0; k < 18; k++)
            {
                c.Col(peb[k % peb.Length]);
                c.Ellipsoid(0.198, 0.615 + r.NextDouble() * 0.03, -0.44 + k * 0.052, 0.008, 0.012, 0.016, 4, 2);
            }
            // plants in front of the glass
            var greens = new[] { C(60, 160, 60), C(90, 190, 70), C(40, 130, 70) };
            foreach (double pz in new[] { -0.4, -0.12, 0.3, 0.42 })
                for (int k = 0; k < 5; k++)
                {
                    double h = 0.12 + ((k * 37 + (int)(pz * 100)) % 9) * 0.02;
                    c.Col(greens[k % 3]);
                    c.Push(Mul(T(0.199, 0.64, pz + (k - 2) * 0.018), M4.RotX((k - 2) * 0.15)));
                    c.Ellipsoid(0, h / 2, 0, 0.004, h / 2, 0.014, 4, 2);
                    c.Pop();
                }
            // a little sunken castle
            c.Col(C(150, 145, 140)); c.Box(0.12, 0.72, 0.12, 0.12, 0.12, 0.12);
            for (int s = -1; s <= 1; s += 2) c.Box(0.12, 0.8, 0.12 + s * 0.045, 0.12, 0.04, 0.03);
            c.Col(C(40, 40, 50)); c.Box(0.181, 0.7, 0.12, 0.004, 0.06, 0.04);
            // bubbles from the bubbler
            c.Col(C(225, 245, 255));
            for (int k = 0; k < 8; k++) c.Sphere(0.2, 0.68 + k * 0.042, -0.3 + 0.012 * Math.Sin(k * 1.7), 0.008 + k * 0.0015, 5, 2);
            // fish
            Fish(c, P(0.21, 0.86, 0.05), 0.055, 1, C(250, 130, 30), C(250, 160, 60), White);
            Fish(c, P(0.21, 0.94, -0.2), 0.045, -1, C(250, 215, 40), C(250, 180, 30), null);
            Fish(c, P(0.21, 0.76, -0.05), 0.04, -1, C(60, 110, 230), C(240, 210, 50), null);
            Fish(c, P(0.21, 0.92, 0.32), 0.042, 1, C(230, 60, 80), C(250, 120, 140), null);
            Fish(c, P(0.21, 0.72, 0.36), 0.035, -1, C(250, 130, 30), C(250, 160, 60), White);
        }

        /// <summary>School microscope (white and black) on a small table, shown three-quarter to the class, with a petri dish.</summary>
        static void NatureMicroscope(Model m)
        {
            double y = Table(m, 0.55, 0.6, 0.6);
            var c = m.Plain;
            var white = C(240, 240, 235);
            var black = C(35, 35, 40);
            c.Col(C(220, 235, 245)); c.Cylinder(0.12, y, 0.18, 0.06, 0.06, y + 0.015, 16);
            c.Col(C(120, 200, 90)); Annulus(c, 0.12, y + 0.0155, 0.18, 0, 0.05, 12);
            c.Col(C(250, 250, 245)); c.Box(0.1, y + 0.005, -0.17, 0.2, 0.01, 0.14);
            m.Push(Mul(T(-0.02, y, 0), M4.RotY(-0.9)));
            c.Col(white); SoftBox(c, -0.03, 0.025, 0, 0.11, 0.025, 0.09, 0.4, 12, 6);
            c.Col(C(255, 250, 200)); c.Cylinder(0.02, 0.05, 0, 0.022, 0.022, 0.075, 10);
            c.Push(Mul(T(-0.085, 0.17, 0), M4.RotZ(0.22)));
            c.Col(white); SoftBox(c, 0, 0, 0, 0.03, 0.13, 0.035, 0.4, 10, 8);
            c.Pop();
            c.Col(black); c.Box(0.02, 0.15, 0, 0.12, 0.012, 0.12);
            c.Col(C(170, 220, 240)); c.Box(0.02, 0.158, 0, 0.03, 0.003, 0.075);
            c.Col(C(200, 200, 205));
            for (int s = -1; s <= 1; s += 2) c.Rod(P(-0.02, 0.158, s * 0.04), P(0.04, 0.158, s * 0.03), 0.003, 4);
            c.Col(black);
            for (int s = -1; s <= 1; s += 2)
            {
                c.Push(Mul(T(-0.08, 0.13, s * 0.05), M4.RotX(s * PI / 2)));
                c.Cylinder(0, 0, 0, 0.03, 0.03, 0.02, 12);
                c.Cylinder(0, 0.02, 0, 0.018, 0.018, 0.035, 10);
                c.Pop();
            }
            // nosepiece with objectives and the tilted tube
            c.Col(C(200, 200, 205)); c.Cylinder(0.02, 0.215, 0, 0.045, 0.04, 0.235, 14);
            for (int k = 0; k < 3; k++)
            {
                double a = k * 2 * PI / 3;
                var o = P(0.02 + Math.Cos(a) * 0.022, 0.215, Math.Sin(a) * 0.022);
                Cone(c, o, o + P(Math.Cos(a) * 0.012, -0.04 - k * 0.006, Math.Sin(a) * 0.012), 0.012, 0.007, 8);
            }
            c.Push(Mul(T(0.0, 0.24, 0), M4.RotZ(0.55)));
            c.Col(white); c.Cylinder(0, 0, 0, 0.034, 0.03, 0.12, 12);
            c.Col(black); c.Cylinder(0, 0.12, 0, 0.022, 0.022, 0.17, 10);
            c.Cylinder(0, 0.17, 0, 0.026, 0.026, 0.18, 10);
            c.Pop();
            m.Pop();
        }

        /// <summary>Butterfly pinned flat on a board facing +x; s ≈ half the wing span.</summary>
        static void Butterfly(MeshBuilder c, double x, double cy, double cz, double s, Color outer, Color inner, Color spot)
        {
            for (int sd = -1; sd <= 1; sd += 2)
            {
                c.Col(outer);
                FlatOval(c, x, cy + 0.22 * s, cz + sd * 0.45 * s, 0.36 * s, 0.52 * s, -sd * 0.45, 10);
                FlatOval(c, x, cy - 0.28 * s, cz + sd * 0.3 * s, 0.32 * s, 0.3 * s, sd * 0.3, 8);
                c.Col(inner);
                FlatOval(c, x + 0.001, cy + 0.2 * s, cz + sd * 0.42 * s, 0.27 * s, 0.4 * s, -sd * 0.45, 10);
                FlatOval(c, x + 0.001, cy - 0.27 * s, cz + sd * 0.28 * s, 0.23 * s, 0.22 * s, sd * 0.3, 8);
                c.Col(spot);
                FlatOval(c, x + 0.002, cy + 0.32 * s, cz + sd * 0.62 * s, 0.08 * s, 0.08 * s, 0, 6);
                FlatOval(c, x + 0.002, cy - 0.3 * s, cz + sd * 0.32 * s, 0.07 * s, 0.07 * s, 0, 6);
            }
            c.Col(C(45, 35, 30));
            c.Ellipsoid(x + 0.006, cy - 0.05 * s, cz, 0.008, 0.42 * s, 0.05 * s, 6, 2);
            c.Sphere(x + 0.006, cy + 0.42 * s, cz, 0.06 * s, 4, 2);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                c.Rod(P(x + 0.006, cy + 0.45 * s, cz), P(x + 0.006, cy + 0.8 * s, cz + sd * 0.22 * s), 0.0025, 3);
            }
            c.Col(C(220, 40, 40)); c.Sphere(x + 0.02, cy + 0.1 * s, cz, 0.008, 4, 2);
        }

        /// <summary>Standing display case with nine pinned colourful butterflies, tilted back on a wooden stand.</summary>
        static void NatureButterflies(Model m)
        {
            var w = m.Get(WoodTex());
            var c = m.Plain;
            w.Col(C(110, 75, 45));
            w.Box(0, 0.03, 0, 0.42, 0.06, 0.9);
            for (int s = -1; s <= 1; s += 2) w.Box(-0.03, 0.36, s * 0.36, 0.05, 0.62, 0.05);
            m.Push(Mul(T(0, 0.98, 0), M4.RotZ(0.15)));
            w.Col(C(125, 82, 50)); w.Box(-0.03, 0, 0, 0.03, 0.66, 0.96);
            c.Col(C(242, 236, 215)); c.Box(-0.013, 0, 0, 0.006, 0.6, 0.9);
            w.Col(C(140, 95, 55));
            for (int s = -1; s <= 1; s += 2)
            {
                w.Box(0, s * 0.315, 0, 0.06, 0.03, 0.96);
                w.Box(0, 0, s * 0.465, 0.06, 0.66, 0.03);
            }
            c.Col(C(230, 245, 255)); c.Box(0.029, 0.2, 0.3, 0.002, 0.012, 0.25);
            var wings = new[]
            {
                new[] { C(40, 90, 220), C(80, 170, 250), C(20, 30, 60) },
                new[] { C(30, 25, 25), C(245, 140, 30), C(250, 250, 250) },
                new[] { C(250, 220, 50), C(255, 245, 140), C(240, 120, 30) },
                new[] { C(200, 30, 40), C(240, 80, 70), C(30, 30, 30) },
                new[] { C(40, 150, 90), C(120, 220, 140), C(250, 230, 80) },
                new[] { C(140, 60, 180), C(210, 140, 240), C(255, 230, 90) },
                new[] { C(250, 250, 245), C(240, 240, 230), C(60, 60, 60) },
                new[] { C(240, 110, 160), C(255, 190, 220), C(120, 40, 120) },
                new[] { C(30, 160, 190), C(250, 140, 40), C(25, 25, 40) },
            };
            for (int k = 0; k < 9; k++)
            {
                double cy = 0.18 - (k / 3) * 0.19, cz = 0.28 - (k % 3) * 0.28;
                Butterfly(c, -0.008, cy + 0.02, cz, 0.1, wings[k][0], wings[k][1], wings[k][2]);
                c.Col(White); c.Box(-0.009, cy - 0.07, cz, 0.003, 0.018, 0.08);
            }
            m.Pop();
        }

        static void Pot(MeshBuilder c, double x, double y, double z, double r, double h, Color col)
        {
            c.Col(col); c.Cylinder(x, y, z, r * 0.75, r, y + h - 0.02, 8);
            c.Col(Shade(col, 15)); c.Cylinder(x, y + h - 0.025, z, r * 1.1, r * 1.1, y + h + 0.005, 8, false);
            c.Col(C(80, 55, 35)); Annulus(c, x, y + h, z, 0, r * 1.1, 8);
        }

        /// <summary>Small flower facing +x: five flat petals around a yellow centre.</summary>
        static void Blossom(MeshBuilder c, V3 p, double r, Color petal)
        {
            c.Col(petal);
            for (int k = 0; k < 5; k++)
            {
                double a = k * 2 * PI / 5;
                FlatOval(c, p.X, p.Y + Math.Sin(a) * r, p.Z + Math.Cos(a) * r, r * 0.5, r * 0.8, a, 6);
            }
            c.Col(C(255, 215, 50)); c.Sphere(p.X + 0.004, p.Y, p.Z, r * 0.5, 4, 2);
        }

        /// <summary>Stepped wooden plant shelf with potted plants: a leafy plant, cacti with flowers, a succulent, a fern, ivy and tulips.</summary>
        static void NaturePlants(Model m)
        {
            var w = m.Get(WoodTex());
            var c = m.Plain;
            var lf = m.Get(LeafTex());
            w.Col(C(225, 205, 175));
            for (int s = -1; s <= 1; s += 2)
                Prism(w, new[] { 0.24, 0.0, -0.2, 0.0, -0.2, 1.0, -0.02, 1.0 }, s * 0.5 - 0.015, s * 0.5 + 0.015, MapSide);
            double[] ys = { 0.3, 0.62, 0.94 };
            double[] xs = { 0.06, -0.03, -0.11 };
            double[] ds = { 0.32, 0.26, 0.17 };
            for (int i = 0; i < 3; i++) w.Box(xs[i], ys[i] - 0.015, 0, ds[i], 0.03, 0.97);
            var terra = C(205, 110, 70);
            var stem = C(70, 140, 60);
            // bottom shelf: leafy plant, tall cactus, flowers
            double y = ys[0];
            Pot(c, 0.06, y, -0.3, 0.09, 0.12, terra);
            for (int k = 0; k < 5; k++)
            {
                lf.Col(k < 3 ? C(225, 245, 215) : White);
                m.Push(Mul(T(0.06, y + 0.12, -0.3), M4.RotY(k * 1.25), M4.RotZ(0.5 + (k % 3) * 0.15)));
                lf.Ellipsoid(0, 0.12, 0, 0.012, 0.12, 0.05, 5, 3);
                m.Pop();
            }
            lf.Col(White); lf.Ellipsoid(0.06, y + 0.25, -0.3, 0.1, 0.08, 0.1, 8, 5);
            Pot(c, 0.06, y, 0.02, 0.07, 0.1, C(90, 140, 210));
            c.Col(C(80, 160, 80));
            c.Cylinder(0.06, y + 0.1, 0.02, 0.04, 0.035, y + 0.3, 8, false);
            c.Sphere(0.06, y + 0.3, 0.02, 0.035, 8, 4);
            Limb(c, P(0.06, y + 0.17, 0.05), P(0.06, y + 0.26, 0.09), 0.02, 6, 3);
            Limb(c, P(0.06, y + 0.2, -0.01), P(0.06, y + 0.27, -0.06), 0.018, 6, 3);
            Blossom(c, P(0.09, y + 0.33, 0.02), 0.018, C(255, 120, 170));
            Pot(c, 0.06, y, 0.32, 0.08, 0.11, C(240, 200, 60));
            var petals = new[] { C(240, 60, 70), C(250, 250, 250), C(150, 90, 220) };
            for (int k = 0; k < 3; k++)
            {
                var tip = P(0.07 + (k % 2) * 0.02, y + 0.27 + k * 0.03, 0.32 + (k - 1) * 0.045);
                c.Col(stem); c.Rod(P(0.06, y + 0.11, 0.32), tip, 0.005, 4);
                Blossom(c, tip + P(0.01, 0, 0), 0.022, petals[k]);
            }
            c.Col(stem);
            for (int s = -1; s <= 1; s += 2) c.Ellipsoid(0.07, y + 0.16, 0.32 + s * 0.03, 0.008, 0.05, 0.016, 4, 2);
            // middle shelf: succulent, round cactus, fern
            y = ys[1];
            Pot(c, -0.03, y, -0.28, 0.08, 0.08, C(230, 230, 225));
            c.Col(C(130, 175, 150));
            for (int k = 0; k < 7; k++)
            {
                c.Push(Mul(T(-0.03, y + 0.09, -0.28), M4.RotY(k * 0.9), M4.RotZ(-0.9 + (k % 3) * 0.3)));
                c.Ellipsoid(0.04, 0, 0, 0.045, 0.012, 0.022, 5, 2);
                c.Pop();
            }
            Pot(c, -0.03, y, 0.02, 0.075, 0.09, terra);
            c.Col(C(70, 150, 75)); c.Ellipsoid(-0.03, y + 0.15, 0.02, 0.07, 0.07, 0.07, 10, 5);
            Blossom(c, P(0.02, y + 0.2, 0.02), 0.018, C(250, 90, 60));
            Pot(c, -0.03, y, 0.3, 0.08, 0.1, C(100, 180, 200));
            for (int k = 0; k < 6; k++)
            {
                lf.Col(C(200, 250, 170));
                m.Push(Mul(T(-0.03, y + 0.1, 0.3), M4.RotY(k * 1.05), M4.RotZ(0.8)));
                lf.Ellipsoid(0, 0.1, 0, 0.008, 0.1, 0.035, 5, 3);
                m.Pop();
            }
            // top shelf: trailing ivy and tulips
            y = ys[2];
            Pot(c, -0.1, y, -0.22, 0.07, 0.09, terra);
            foreach (double sz in new[] { -0.26, -0.17 })
            {
                var pts = new List<V3>();
                for (int k = 0; k < 4; k++) pts.Add(P(-0.04 + k * 0.015, y + 0.08 - k * 0.065, sz + Math.Sin(k + sz * 10) * 0.02));
                c.Col(C(60, 120, 50)); Tube(c, pts.ToArray(), 0.004, 3);
                c.Col(C(70, 160, 70));
                for (int k = 1; k < 4; k++) c.Ellipsoid(pts[k].X + 0.01, pts[k].Y, pts[k].Z + 0.01, 0.006, 0.02, 0.02, 4, 2);
            }
            c.Col(C(80, 170, 75)); c.Ellipsoid(-0.1, y + 0.12, -0.22, 0.08, 0.05, 0.08, 8, 4);
            Pot(c, -0.1, y, 0.2, 0.075, 0.09, C(240, 120, 150));
            for (int k = 0; k < 3; k++)
            {
                var tip = P(-0.1 + (k % 2) * 0.02, y + 0.24 + (k % 2) * 0.03, 0.2 + (k - 1) * 0.04);
                c.Col(stem); c.Rod(P(-0.1, y + 0.09, 0.2), tip, 0.005, 4);
                c.Ellipsoid(-0.1, y + 0.14, 0.2 + (k - 1) * 0.05, 0.008, 0.06, 0.016, 4, 2);
                c.Col(k % 2 == 0 ? C(240, 50, 60) : C(250, 200, 40)); c.Ellipsoid(tip.X, tip.Y + 0.02, tip.Z, 0.02, 0.03, 0.02, 5, 3, -1, 0.7);
            }
        }

        // ================================================================== geography and history

        /// <summary>Big floor-standing globe (Ø 0.72) on a wooden stand with a horizon ring and a gold meridian.</summary>
        static void GeoGlobe(Model m)
        {
            var w = m.Get(WoodTex());
            var c = m.Plain;
            const double ringY = 0.78, ringR = 0.42;
            w.Col(C(150, 95, 55));
            for (int k = 0; k < 3; k++)
            {
                double a = k * 2 * PI / 3 + PI / 3, ca = Math.Cos(a), sa = Math.Sin(a);
                Tube(w, new[] { P(ca * 0.41, ringY - 0.012, sa * 0.41), P(ca * 0.45, 0.48, sa * 0.45), P(ca * 0.39, 0.22, sa * 0.39), P(ca * 0.47, 0.035, sa * 0.47) }, 0.024, 6);
                w.Sphere(ca * 0.47, 0.035, sa * 0.47, 0.035, 6, 3);
                Tube(w, new[] { P(ca * 0.39, 0.22, sa * 0.39), P(0, 0.22, 0) }, 0.016, 6);
            }
            w.Cylinder(0, 0.18, 0, 0.05, 0.05, 0.26, 12);
            w.Cylinder(0, 0.26, 0, 0.03, 0.022, 0.425, 10);
            Tube(w, new[] { P(0, 0.42, 0), P(0, 0.405, -0.08), P(0, 0.415, -0.159) }, 0.014, 6);
            // horizon ring with hour marks
            c.Col(C(240, 225, 180)); Annulus(c, 0, ringY + 0.012, 0, ringR - 0.05, ringR + 0.02, 36);
            w.Col(C(130, 80, 45)); w.Cylinder(0, ringY - 0.012, 0, ringR + 0.02, ringR + 0.02, ringY + 0.012, 36, false);
            InnerWall(w, 0, 0, ringR - 0.05, ringY - 0.012, ringY + 0.012, 36);
            w.Push(Mul(T(0, ringY - 0.012, 0), M4.RotX(PI)));
            Annulus(w, 0, 0, 0, ringR - 0.05, ringR + 0.02, 36);
            w.Pop();
            c.Col(C(110, 70, 40));
            for (int k = 0; k < 12; k++)
            {
                double a = k * PI / 6;
                c.Push(Mul(T(Math.Cos(a) * (ringR - 0.005), ringY + 0.0125, Math.Sin(a) * (ringR - 0.005)), M4.RotY(-a)));
                c.Box(0, 0, 0, 0.035, 0.001, 0.005);
                c.Pop();
            }
            m.Push(Mul(T(0, ringY, 0), M4.RotX(0.41)));
            var g = m.Get(GlobeTex());
            g.Col(White); g.Ellipsoid(0, 0, 0, 0.36, 0.36, 0.36, 24, 14);
            c.Col(Gold); c.Torus(0, 0, 0, 0.385, 0.013, 28, 4);
            c.Sphere(0, 0.39, 0, 0.022, 6, 3); c.Sphere(0, -0.39, 0, 0.022, 6, 3);
            c.Rod(P(0, 0.35, 0), P(0, 0.39, 0), 0.008, 5); c.Rod(P(0, -0.39, 0), P(0, -0.35, 0), 0.008, 5);
            m.Pop();
        }

        /// <summary>Table with a 3D relief model: snowy mountains at the back, green valleys, a blue river running to the front.</summary>
        static void GeoRelief(Model m)
        {
            double top = Table(m, 0.8, 1.06, 0.55);
            var c = m.Plain;
            c.Col(C(120, 85, 55)); c.Box(0, top + 0.02, 0, 0.72, 0.04, 0.98);
            double y0 = top + 0.04;
            const int NX = 20, NZ = 26;
            const double X0 = -0.34, X1 = 0.34, Z0 = -0.46, Z1 = 0.46;
            Func<double, double, double> H = (x, z) =>
            {
                double rz = 0.07 * Math.Sin(x * 9) + 0.03;
                double d = Math.Abs(z - rz);
                double h = 0.035 + 0.01 * Math.Sin(x * 23 + z * 17);
                h += 0.21 * Math.Exp(-(Sq(x + 0.2) + Sq(z + 0.27)) / 0.012);
                h += 0.17 * Math.Exp(-(Sq(x + 0.17) + Sq(z - 0.28)) / 0.01);
                h += 0.12 * Math.Exp(-(Sq(x - 0.06) + Sq(z + 0.37)) / 0.007);
                h += 0.07 * Math.Exp(-(Sq(x - 0.16) + Sq(z - 0.33)) / 0.01);
                double k = Math.Min(1, d / 0.13);
                return 0.012 + (h - 0.012) * k * k * (3 - 2 * k);
            };
            var pt = new V3[NX + 1, NZ + 1];
            for (int i = 0; i <= NX; i++)
                for (int j = 0; j <= NZ; j++)
                {
                    double x = X0 + (X1 - X0) * i / NX, z = Z0 + (Z1 - Z0) * j / NZ;
                    pt[i, j] = P(x, y0 + H(x, z), z);
                }
            for (int i = 0; i < NX; i++)
                for (int j = 0; j < NZ; j++)
                {
                    double h = (pt[i, j].Y + pt[i + 1, j].Y + pt[i, j + 1].Y + pt[i + 1, j + 1].Y) / 4 - y0;
                    double hmax = Math.Max(Math.Max(pt[i, j].Y, pt[i + 1, j].Y), Math.Max(pt[i, j + 1].Y, pt[i + 1, j + 1].Y)) - y0;
                    Color col;
                    if (hmax < 0.02) col = C(60, 140, 225);
                    else if (h < 0.05) col = (i + j) % 3 == 0 ? C(120, 190, 80) : C(105, 180, 70);
                    else if (h < 0.09) col = C(80, 145, 60);
                    else if (h < 0.13) col = C(145, 118, 85);
                    else if (h < 0.17) col = C(155, 150, 145);
                    else col = C(248, 250, 252);
                    c.Col(col);
                    FaceUp(c, pt[i, j], pt[i, j + 1], pt[i + 1, j + 1], pt[i + 1, j]);
                }
            // soil sides
            c.Col(C(150, 110, 70));
            for (int i = 0; i < NX; i++)
                foreach (int j in new[] { 0, NZ })
                {
                    V3 a = pt[i, j], b = pt[i + 1, j];
                    Panel(c, P(a.X, y0, a.Z), P(b.X, y0, b.Z), b, a);
                }
            for (int j = 0; j < NZ; j++)
                foreach (int i in new[] { 0, NX })
                {
                    V3 a = pt[i, j], b = pt[i, j + 1];
                    Panel(c, P(a.X, y0, a.Z), P(b.X, y0, b.Z), b, a);
                }
            // little trees, houses and a flag on the highest peak
            var r = new Random(17);
            for (int k = 0; k < 14; k++)
            {
                double x = X0 + 0.05 + r.NextDouble() * 0.6, z = Z0 + 0.05 + r.NextDouble() * 0.82;
                double h = H(x, z);
                if (h < 0.03 || h > 0.08) continue;
                c.Col(C(110, 75, 40)); c.Cylinder(x, y0 + h - 0.005, z, 0.005, 0.005, y0 + h + 0.012, 4, false);
                c.Col(C(40, 110, 50)); c.Cylinder(x, y0 + h + 0.01, z, 0.02, 0, y0 + h + 0.06, 6);
            }
            foreach (var hp in new[] { new[] { 0.2, -0.15 }, new[] { 0.24, 0.2 }, new[] { 0.05, 0.16 } })
            {
                double x = hp[0], z = hp[1], h = y0 + H(x, z);
                c.Col(White); c.Box(x, h + 0.012, z, 0.03, 0.03, 0.035);
                c.Col(C(210, 60, 50)); c.Push(Mul(T(x, h + 0.027, z), M4.RotY(PI / 4))); c.Cylinder(0, 0, 0, 0.032, 0, 0.025, 4); c.Pop();
            }
            double px = -0.2, pz = -0.27, ph = y0 + H(px, pz);
            c.Col(C(90, 90, 90)); c.Rod(P(px, ph - 0.01, pz), P(px, ph + 0.08, pz), 0.003, 4);
            c.Col(C(230, 40, 40)); Panel(c, P(px, ph + 0.05, pz), P(px, ph + 0.05, pz + 0.04), P(px, ph + 0.08, pz + 0.04), P(px, ph + 0.08, pz));
        }

        /// <summary>Flat round rug (Ø 1.6, plus a short fringe) at y = 0.01 with a compass rose; N points to -x (upright for the class).</summary>
        static void GeoRug(Model m)
        {
            var t = m.Get(CompassRoseTex());
            t.Col(White); PolyUp(t, 0.01, Oval(0, 0, 0.8, 0.8, 48), 0, 0, 0.8, 0.8);
            var c = m.Plain;
            c.Col(C(150, 50, 45)); c.Cylinder(0, 0, 0, 0.8, 0.8, 0.01, 48, false);
            c.Col(C(240, 225, 190));
            for (int k = 0; k < 48; k++)
            {
                double a = 2 * PI * (k + 0.5) / 48;
                c.Push(Mul(T(Math.Cos(a) * 0.82, 0.003, Math.Sin(a) * 0.82), M4.RotY(-a)));
                c.Box(0, 0, 0, 0.045, 0.005, 0.012);
                c.Pop();
            }
        }

        /// <summary>Heater shield outline as (y, z) pairs: straight top, sides curving down to a point.</summary>
        static double[] Heater(double cy, double cz, double hh, double hw, int n)
        {
            var pts = new List<double>();
            Func<double, double> f = t => t < 0.35 ? 1 : Math.Cos((t - 0.35) / 0.65 * PI / 2);
            for (int i = 0; i <= n; i++)
            {
                double t = (double)i / n;
                pts.Add(cy + hh - 2 * hh * t); pts.Add(cz - hw * f(t));
            }
            for (int i = n - 1; i >= 0; i--)
            {
                double t = (double)i / n;
                pts.Add(cy + hh - 2 * hh * t); pts.Add(cz + hw * f(t));
            }
            return pts.ToArray();
        }

        /// <summary>Polish knight in armour (≈1.3 with the plume) on a stone plinth: lance with a white-red pennant, shield with the white eagle on red.</summary>
        static void GeoKnight(Model m)
        {
            var c = m.Plain;
            var steel = C(195, 200, 212);
            var dark = C(120, 125, 140);
            var red = C(200, 30, 40);
            var st = m.Get(StoneTex());
            st.Col(C(210, 210, 210)); st.Box(0, 0.04, 0, 0.5, 0.08, 0.6);
            double y0 = 0.08;
            for (int s = -1; s <= 1; s += 2)
            {
                c.Col(steel); c.Ellipsoid(0.04, y0 + 0.03, s * 0.08, 0.09, 0.035, 0.05, 8, 3);
                c.Cylinder(0, y0 + 0.03, s * 0.08, 0.045, 0.055, y0 + 0.3, 10);
                c.Col(dark); c.Sphere(0.01, y0 + 0.31, s * 0.08, 0.055, 8, 4);
                c.Col(steel); c.Cylinder(0, y0 + 0.32, s * 0.085, 0.055, 0.065, y0 + 0.5, 10);
            }
            c.Col(red); c.Cylinder(0, y0 + 0.42, 0, 0.17, 0.14, y0 + 0.62, 16);
            c.Col(C(70, 45, 30)); c.Cylinder(0, y0 + 0.6, 0, 0.147, 0.147, y0 + 0.64, 16, false);
            c.Col(Gold); c.Box(0.147, y0 + 0.62, 0, 0.012, 0.035, 0.04);
            c.Col(steel); c.Ellipsoid(0, y0 + 0.78, 0, 0.13, 0.19, 0.17, 12, 8);
            c.Col(dark); c.Ellipsoid(0.005, y0 + 0.8, 0, 0.128, 0.16, 0.012, 8, 4);
            c.Cylinder(0, y0 + 0.94, 0, 0.09, 0.07, y0 + 1.0, 12);
            for (int s = -1; s <= 1; s += 2)
            {
                c.Col(steel); c.Ellipsoid(0, y0 + 0.92, s * 0.17, 0.09, 0.07, 0.08, 10, 5);
                c.Col(dark); c.Ellipsoid(0, y0 + 0.87, s * 0.19, 0.085, 0.045, 0.075, 10, 3);
            }
            // right arm with the lance and its white-red pennant
            var rs = P(0, y0 + 0.88, 0.2);
            var re = P(0.04, y0 + 0.7, 0.23);
            var rh = P(0.13, y0 + 0.68, 0.23);
            c.Col(steel); Limb(c, rs, re, 0.045, 8, 4); Limb(c, re, rh, 0.04, 8, 4);
            c.Col(dark); c.Sphere(re.X, re.Y, re.Z, 0.045, 8, 4); c.Ellipsoid(rh.X, rh.Y, rh.Z, 0.045, 0.045, 0.048, 8, 4);
            c.Col(C(150, 100, 55)); c.Rod(P(0.13, y0, 0.23), P(0.13, 1.3, 0.23), 0.018, 8);
            c.Col(steel); Cone(c, P(0.13, 1.3, 0.23), P(0.13, 1.4, 0.23), 0.026, 0, 8);
            c.Col(White); Panel(c, P(0.13, 1.24, 0.245), P(0.13, 1.24, 0.43), P(0.13, 1.28, 0.43), P(0.13, 1.28, 0.245));
            c.Col(red); Panel(c, P(0.13, 1.2, 0.245), P(0.13, 1.2, 0.43), P(0.13, 1.24, 0.43), P(0.13, 1.24, 0.245));
            // left arm holding the shield in front
            var ls = P(0, y0 + 0.88, -0.2);
            var lh = P(0.1, y0 + 0.64, -0.2);
            c.Col(steel); Limb(c, ls, lh, 0.045, 8, 4);
            double scy = y0 + 0.6, scz = -0.17;
            c.Col(steel); Prism(c, Heater(scy, scz, 0.225, 0.172, 8), 0.13, 0.15, MapFront);
            var sh = m.Get(ShieldTex());
            sh.Col(White); PolyX(sh, 0.1505, Heater(scy, scz, 0.21, 0.16, 8), scy, scz, 0.21, 0.16);
            // great helm with an eye slit, a gold cross and a red-white plume
            double hy = y0 + 1.0;
            c.Col(steel); c.Cylinder(0.01, hy, 0, 0.1, 0.1, hy + 0.15, 14);
            c.Ellipsoid(0.01, hy + 0.15, 0, 0.1, 0.06, 0.1, 14, 4, 0, 1);
            c.Col(C(20, 20, 25)); c.Box(0.106, hy + 0.095, 0, 0.012, 0.016, 0.13);
            for (int k = 0; k < 6; k++) c.Sphere(0.108, hy + 0.04 + (k / 3) * 0.02, 0.04 + (k % 3) * 0.012, 0.004, 4, 2);
            c.Col(Gold); c.Box(0.109, hy + 0.07, 0, 0.008, 0.11, 0.016); c.Box(0.109, hy + 0.095, 0, 0.008, 0.016, 0.16);
            c.Col(red); Limb(c, P(0.0, hy + 0.2, 0), P(-0.12, hy + 0.26, 0), 0.04, 8, 4);
            c.Col(White); Limb(c, P(-0.02, hy + 0.21, 0.03), P(-0.13, hy + 0.23, 0.04), 0.028, 6, 3);
            Limb(c, P(-0.02, hy + 0.21, -0.03), P(-0.13, hy + 0.23, -0.04), 0.028, 6, 3);
        }

        // ================================================================== English

        /// <summary>Red double-decker London bus model on a table; its long side (with windows) faces +x, the front -z.</summary>
        static void EnglishBus(Model m)
        {
            double top = Table(m, 0.5, 1.05, 0.5);
            var c = m.Plain;
            var red = C(210, 30, 35);
            double y = top + 0.05;
            const double L = 0.86, Wd = 0.22, H = 0.38;
            c.Col(red); c.Box(0, y + H / 2, 0, Wd, H, L);
            c.Col(Shade(red, 15)); c.Box(0, y + H + 0.008, 0, Wd - 0.01, 0.016, L - 0.01);
            var s1 = m.Get(BusSideTex(false));
            var s2 = m.Get(BusSideTex(true));
            var d = m.Get(DestTex());
            s1.Col(White); FaceX(s1, Wd / 2 + 0.0005, y + H / 2, 0, L, H);
            m.Push(M4.RotY(PI));
            s2.Col(White); FaceX(s2, Wd / 2 + 0.0005, y + H / 2, 0, L, H);
            m.Pop();
            // front (-z): windscreens, destination board, lights and number plate
            double fz = -L / 2 - 0.002;
            c.Col(C(40, 55, 80)); c.Box(0, y + 0.13, fz, 0.17, 0.1, 0.004); c.Box(0, y + 0.3, fz, 0.17, 0.08, 0.004);
            c.Col(C(20, 20, 20)); c.Box(0, y + 0.215, fz - 0.001, 0.15, 0.036, 0.004);
            m.Push(M4.RotY(PI / 2));
            d.Col(White); FaceX(d, L / 2 + 0.0035, y + 0.215, 0, 0.14, 0.03);
            m.Pop();
            c.Col(C(255, 250, 210));
            for (int s = -1; s <= 1; s += 2) c.Sphere(s * 0.075, y + 0.05, fz, 0.018, 6, 3);
            c.Col(C(250, 210, 40)); c.Box(0, y + 0.03, fz - 0.001, 0.07, 0.018, 0.004);
            c.Col(C(60, 60, 60)); c.Box(0, y + 0.06, fz - 0.001, 0.08, 0.025, 0.004);
            // rear (+z)
            double bz = L / 2 + 0.002;
            c.Col(C(40, 55, 80)); c.Box(0, y + 0.3, bz, 0.15, 0.07, 0.004); c.Box(0, y + 0.15, bz, 0.15, 0.07, 0.004);
            c.Col(C(250, 60, 50));
            for (int s = -1; s <= 1; s += 2) c.Box(s * 0.09, y + 0.05, bz, 0.025, 0.035, 0.004);
            // wheels
            foreach (double z in new[] { -0.28, 0.27 })
                for (int s = -1; s <= 1; s += 2)
                {
                    c.Push(Mul(T(s * 0.095, y + 0.005, z), M4.RotZ(-PI / 2)));
                    c.Col(C(25, 25, 25)); c.Cylinder(0, -0.025, 0, 0.055, 0.055, 0.025, 14);
                    c.Col(C(200, 200, 205)); c.Cylinder(0, -0.028, 0, 0.026, 0.026, 0.028, 10);
                    c.Pop();
                }
        }

        /// <summary>Big Ben clock tower model ≈1.3 tall with four clock faces.</summary>
        static void EnglishBigBen(Model m)
        {
            var c = m.Plain;
            var stone = C(222, 196, 135);
            c.Col(C(150, 140, 125)); c.Box(0, 0.03, 0, 0.46, 0.06, 0.46);
            var t = m.Get(BigBenTex());
            t.Col(White); t.Box(0, 0.39, 0, 0.26, 0.66, 0.26);
            c.Col(Shade(stone, -15)); c.Box(0, 0.735, 0, 0.3, 0.03, 0.3);
            c.Col(stone); c.Box(0, 0.86, 0, 0.3, 0.22, 0.3);
            c.Col(Shade(stone, -10)); c.Box(0, 0.985, 0, 0.32, 0.03, 0.32);
            c.Col(stone); c.Box(0, 1.03, 0, 0.26, 0.06, 0.26);
            var f = m.Get(ClockFaceTex());
            for (int k = 0; k < 4; k++)
            {
                m.Push(M4.RotY(k * PI / 2));
                c.Col(Gold); c.Box(0.151, 0.86, 0, 0.003, 0.22, 0.22);
                f.Col(White); DiscX(f, 0.1535, 0.86, 0, 0.1, 24);
                c.Col(C(60, 70, 80));
                for (int s = -1; s <= 1; s += 2) c.Box(0.1305, 1.03, s * 0.05, 0.002, 0.045, 0.035);
                c.Col(stone);
                for (int s = -1; s <= 1; s += 2) Cone(c, P(0.14, 1.0, s * 0.14), P(0.14, 1.09, s * 0.14), 0.016, 0, 6);
                m.Pop();
            }
            c.Col(C(70, 85, 95)); Pyramid(c, 0, 1.06, 0, 0.135, 0.13);
            c.Col(stone); c.Box(0, 1.2, 0, 0.05, 0.04, 0.05);
            c.Col(C(70, 85, 95)); Pyramid(c, 0, 1.22, 0, 0.03, 0.05);
            c.Col(Gold); c.Rod(P(0, 1.26, 0), P(0, 1.29, 0), 0.004, 4); c.Sphere(0, 1.295, 0, 0.008, 6, 3);
        }

        /// <summary>Red British telephone box ≈1.3 tall: glazed sides, "TELEPHONE" signs, domed roof with crowns.</summary>
        static void EnglishPhoneBox(Model m)
        {
            var c = m.Plain;
            var red = C(205, 25, 30);
            c.Col(C(160, 155, 150)); c.Box(0, 0.025, 0, 0.56, 0.05, 0.56);
            c.Col(red); c.Box(0, 0.55, 0, 0.5, 1.0, 0.5);
            for (int a = -1; a <= 1; a += 2)
                for (int b = -1; b <= 1; b += 2) c.Box(a * 0.235, 0.6, b * 0.235, 0.05, 1.1, 0.05);
            c.Box(0, 1.09, 0, 0.52, 0.08, 0.52);
            c.Col(Shade(red, -15)); c.Box(0, 1.15, 0, 0.54, 0.04, 0.54);
            c.Col(red); c.Ellipsoid(0, 1.17, 0, 0.27, 0.09, 0.27, 16, 4, 0, 1);
            c.Col(Shade(red, -20)); c.Box(0, 1.265, 0, 0.12, 0.02, 0.12);
            var pane = m.Get(PhoneSideTex());
            var sign = m.Get(PhoneSignTex());
            for (int k = 0; k < 4; k++)
            {
                m.Push(M4.RotY(k * PI / 2));
                pane.Col(White); FaceX(pane, 0.2505, 0.6, 0, 0.38, 0.84);
                sign.Col(White); FaceX(sign, 0.2605, 1.09, 0, 0.38, 0.055);
                c.Col(C(240, 190, 60)); c.Ellipsoid(0.2, 1.22, 0, 0.03, 0.035, 0.05, 8, 4);
                m.Pop();
            }
            c.Col(C(200, 200, 205)); c.Box(0.256, 0.62, -0.15, 0.012, 0.08, 0.016);
        }

        // ================================================================== logic

        /// <summary>Chess piece standing at p; type K Q R B N P; s = 1 for 7 cm squares.</summary>
        static void ChessPiece(MeshBuilder c, char type, V3 p, Color col, double s)
        {
            if (type == 'N')
            {
                ChessKnight(c, p, s * 0.36, col, Shade(col, col.R > 128 ? -40 : 40), true);
                return;
            }
            c.Col(col);
            double x = p.X, y = p.Y, z = p.Z;
            c.Cylinder(x, y, z, 0.026 * s, 0.024 * s, y + 0.01 * s, 6);
            switch (type)
            {
                case 'P':
                    c.Cylinder(x, y + 0.01 * s, z, 0.018 * s, 0.009 * s, y + 0.04 * s, 6);
                    c.Sphere(x, y + 0.05 * s, z, 0.013 * s, 5, 2);
                    break;
                case 'R':
                    c.Cylinder(x, y + 0.01 * s, z, 0.019 * s, 0.016 * s, y + 0.055 * s, 6);
                    c.Cylinder(x, y + 0.055 * s, z, 0.022 * s, 0.022 * s, y + 0.07 * s, 6);
                    for (int k = 0; k < 4; k++)
                    {
                        double a = k * PI / 2;
                        c.Box(x + Math.Cos(a) * 0.015 * s, y + 0.076 * s, z + Math.Sin(a) * 0.015 * s, 0.01 * s, 0.012 * s, 0.01 * s);
                    }
                    break;
                case 'B':
                    c.Cylinder(x, y + 0.01 * s, z, 0.018 * s, 0.008 * s, y + 0.06 * s, 6);
                    c.Ellipsoid(x, y + 0.072 * s, z, 0.013 * s, 0.019 * s, 0.013 * s, 5, 3);
                    c.Sphere(x, y + 0.095 * s, z, 0.005 * s, 4, 2);
                    break;
                case 'Q':
                    c.Cylinder(x, y + 0.01 * s, z, 0.019 * s, 0.009 * s, y + 0.075 * s, 6);
                    c.Cylinder(x, y + 0.075 * s, z, 0.016 * s, 0.016 * s, y + 0.082 * s, 6);
                    for (int k = 0; k < 5; k++) c.Sphere(x + Math.Cos(k * 1.256) * 0.012 * s, y + 0.088 * s, z + Math.Sin(k * 1.256) * 0.012 * s, 0.005 * s, 4, 2);
                    c.Sphere(x, y + 0.093 * s, z, 0.007 * s, 4, 2);
                    break;
                default:
                    c.Cylinder(x, y + 0.01 * s, z, 0.02 * s, 0.01 * s, y + 0.08 * s, 6);
                    c.Cylinder(x, y + 0.08 * s, z, 0.017 * s, 0.017 * s, y + 0.087 * s, 6);
                    c.Ellipsoid(x, y + 0.093 * s, z, 0.014 * s, 0.01 * s, 0.014 * s, 5, 2);
                    c.Box(x, y + 0.11 * s, z, 0.006 * s, 0.024 * s, 0.006 * s);
                    c.Box(x, y + 0.113 * s, z, 0.006 * s, 0.006 * s, 0.02 * s);
                    break;
            }
        }

        /// <summary>Small table with a chessboard and a game in progress (white at -x, black towards the class).</summary>
        static void LogicChess(Model m)
        {
            double top = Table(m, 0.7, 0.7, 0.55);
            var c = m.Plain;
            c.Col(C(100, 60, 30)); c.Box(0, top + 0.015, 0, 0.62, 0.03, 0.62);
            var b = m.Get(ChessboardTex());
            b.Col(White); PolyUp(b, top + 0.0305, new[] { -0.3, -0.3, 0.3, -0.3, 0.3, 0.3, -0.3, 0.3 }, 0, 0, 0.3, 0.3);
            double y = top + 0.031;
            Func<int, int, V3> sq = (f, r) => P(-0.245 + r * 0.07, y, 0.245 - f * 0.07);
            var wc = C(245, 238, 220);
            var bc = C(45, 38, 38);
            var white = new[] { "K40", "Q31", "R00", "N22", "P43", "P51", "P61" };
            var black = new[] { "K47", "B56", "N55", "R77", "P34", "P66" };
            foreach (var s in white) ChessPiece(c, s[0], sq(s[1] - '0', s[2] - '0'), wc, 1);
            foreach (var s in black) ChessPiece(c, s[0], sq(s[1] - '0', s[2] - '0'), bc, 1);
            // captured pieces beside the board
            ChessPiece(c, 'P', P(0.27, top, -0.33), bc, 1);
            ChessPiece(c, 'P', P(-0.27, top, 0.33), wc, 1);
        }

        /// <summary>Big scrambled 3×3 puzzle cube (0.42) on a white pedestal.</summary>
        static void LogicCube(Model m)
        {
            double top = Pedestal(m, 0.62, 0.22, C(245, 245, 245));
            var c = m.Plain;
            const double E = 0.42, q = E / 3;
            m.Push(Mul(T(0, top + E / 2 + 0.002, 0), M4.RotY(0.55)));
            c.Col(C(25, 25, 28)); c.Box(0, 0, 0, E, E, E);
            var pal = new[] { C(250, 250, 250), C(255, 215, 0), C(220, 30, 40), C(255, 130, 0), C(20, 90, 220), C(30, 170, 70) };
            var r = new Random(12);
            double o = E / 2 + 0.002, sz = q * 0.86;
            for (int f = 0; f < 6; f++)
                for (int i = -1; i <= 1; i++)
                    for (int j = -1; j <= 1; j++)
                    {
                        int col = i == 0 && j == 0 ? f : r.Next(6);
                        if (f == 1) continue;
                        c.Col(pal[col]);
                        double a = i * q, bb = j * q;
                        switch (f)
                        {
                            case 0: c.Box(a, o, bb, sz, 0.006, sz); break;
                            case 2: c.Box(o, a, bb, 0.006, sz, sz); break;
                            case 3: c.Box(-o, a, bb, 0.006, sz, sz); break;
                            case 4: c.Box(a, bb, o, sz, sz, 0.006); break;
                            default: c.Box(a, bb, -o, sz, sz, 0.006); break;
                        }
                    }
            m.Pop();
        }

        /// <summary>Table with a slanted board showing the seven coloured tangram pieces arranged into a square.</summary>
        static void LogicTangram(Model m)
        {
            double top = Table(m, 0.65, 0.9, 0.55);
            var w = m.Get(WoodTex());
            var c = m.Plain;
            w.Col(C(150, 100, 60));
            for (int s = -1; s <= 1; s += 2) w.Rod(P(-0.27, top, s * 0.24), P(-0.17, top + 0.345, s * 0.24), 0.014, 6);
            m.Push(Mul(T(-0.04, top + 0.2, 0), M4.RotZ(-0.95)));
            w.Col(C(240, 222, 190)); w.Box(0, -0.015, 0, 0.5, 0.03, 0.62);
            w.Col(C(170, 120, 75)); w.Box(0.255, 0.005, 0, 0.016, 0.04, 0.62);
            var pieces = new[]
            {
                new[] { 0, 0, 1, 0, 0.5, 0.5 }, new[] { 0, 0, 0.5, 0.5, 0, 1 }, new[] { 1, 0.5, 1, 1, 0.5, 1 },
                new[] { 0.5, 0.5, 0.75, 0.25, 0.75, 0.75 }, new[] { 0, 1, 0.25, 0.75, 0.5, 1 },
                new[] { 0.5, 0.5, 0.75, 0.75, 0.5, 1, 0.25, 0.75 }, new[] { 0.75, 0.25, 1, 0, 1, 0.5, 0.75, 0.75 },
            };
            var cols = new[] { C(230, 50, 50), C(50, 110, 220), C(250, 200, 40), C(70, 180, 80), C(160, 80, 200), C(250, 140, 40), C(240, 110, 170) };
            for (int k = 0; k < pieces.Length; k++)
            {
                var pc = pieces[k];
                int n = pc.Length / 2;
                double mu = 0, mv = 0;
                for (int i = 0; i < n; i++) { mu += pc[i * 2]; mv += pc[i * 2 + 1]; }
                mu /= n; mv /= n;
                var xz = new double[n * 2];
                for (int i = 0; i < n; i++)
                {
                    double u = mu + (pc[i * 2] - mu) * 0.94, v = mv + (pc[i * 2 + 1] - mv) * 0.94;
                    xz[i * 2] = (v - 0.5) * 0.42;
                    xz[i * 2 + 1] = (0.5 - u) * 0.42;
                }
                c.Col(cols[k]); Prism(c, xz, 0, 0.018, MapFloor);
            }
            m.Pop();
            // a spare set of pieces lying on the table
            c.Col(C(70, 180, 80)); Prism(c, new[] { 0.2, 0.3, 0.27, 0.36, 0.2, 0.42 }, top, top + 0.018, MapFloor);
            c.Col(C(250, 140, 40)); Prism(c, new[] { 0.16, -0.36, 0.22, -0.3, 0.16, -0.24, 0.1, -0.3 }, top, top + 0.018, MapFloor);
        }

        // ================================================================== time and calendar

        /// <summary>Grandfather clock ≈1.39 tall: clock face, pendulum and weights behind a window, arched top with finials.</summary>
        static void TimeClock(Model m)
        {
            var w = m.Get(WoodTex());
            var c = m.Plain;
            var wood = C(155, 90, 55);
            w.Col(Shade(wood, -25)); w.Box(0, 0.02, 0, 0.4, 0.04, 0.52);
            w.Col(wood); w.Box(0, 0.14, 0, 0.36, 0.2, 0.48);
            w.Col(Shade(wood, -15)); w.Box(0, 0.25, 0, 0.38, 0.03, 0.5);
            w.Col(wood); w.Box(-0.01, 0.6, 0, 0.3, 0.7, 0.38);
            c.Col(C(45, 32, 28)); c.Box(0.141, 0.6, 0, 0.004, 0.52, 0.22);
            c.Col(Gold);
            c.Box(0.143, 0.865, 0, 0.004, 0.012, 0.24); c.Box(0.143, 0.335, 0, 0.004, 0.012, 0.24);
            for (int s = -1; s <= 1; s += 2) c.Box(0.143, 0.6, s * 0.116, 0.004, 0.54, 0.012);
            c.Rod(P(0.147, 0.84, 0), P(0.147, 0.47, 0.03), 0.004, 5);
            c.Push(Mul(T(0.149, 0.45, 0.032), M4.RotZ(-PI / 2)));
            c.Cylinder(0, -0.006, 0, 0.055, 0.055, 0.006, 16);
            c.Pop();
            for (int s = -1; s <= 1; s += 2)
            {
                c.Col(C(200, 160, 70)); c.Rod(P(0.147, 0.84, s * 0.075), P(0.147, 0.72, s * 0.075), 0.002, 3);
                c.Cylinder(0.149, 0.62, s * 0.075, 0.016, 0.016, 0.72, 10);
            }
            w.Col(Shade(wood, -15)); w.Box(0, 0.96, 0, 0.36, 0.03, 0.46);
            w.Col(wood); w.Box(0, 1.11, 0, 0.34, 0.28, 0.44);
            var f = m.Get(ClockFaceTex());
            f.Col(White); DiscX(f, 0.1715, 1.11, 0, 0.12, 32);
            c.Col(Gold); c.Torus(0.172, 1.11, 0, 0.122, 0.008, 32, 4);
            for (int s = -1; s <= 1; s += 2) c.Cylinder(0.16, 0.975, s * 0.195, 0.014, 0.014, 1.245, 8);
            w.Col(Shade(wood, -15)); w.Box(0, 1.26, 0, 0.36, 0.025, 0.46);
            w.Col(wood); Prism(w, Oval(1.27, 0, 0.09, 0.22, 16, -PI / 2, PI / 2), -0.15, 0.15, MapFront);
            c.Col(Gold);
            c.Sphere(0.0, 1.37, 0, 0.02, 8, 4);
            for (int s = -1; s <= 1; s += 2) c.Sphere(0.12, 1.29, s * 0.2, 0.018, 6, 3);
        }

        /// <summary>Big hourglass (wooden frame, glass bulbs with golden sand) on a small table.</summary>
        static void TimeHourglass(Model m)
        {
            double top = Table(m, 0.5, 0.5, 0.5);
            var w = m.Get(WoodTex());
            var c = m.Plain;
            w.Col(C(140, 85, 45));
            w.Cylinder(0, top, 0, 0.17, 0.17, top + 0.035, 20);
            w.Cylinder(0, top + 0.5, 0, 0.17, 0.17, top + 0.535, 20);
            for (int k = 0; k < 3; k++)
            {
                double a = k * 2 * PI / 3 + 0.5, px = Math.Cos(a) * 0.14, pz = Math.Sin(a) * 0.14;
                w.Cylinder(px, top + 0.035, pz, 0.013, 0.013, top + 0.5, 8);
                foreach (double h in new[] { 0.1, 0.2675, 0.435 }) w.Sphere(px, top + h, pz, 0.022, 8, 4);
            }
            var glass = C(215, 235, 245);
            var sand = C(236, 192, 95);
            double g = top + 0.2675;
            c.Col(sand); c.Ellipsoid(0, g - 0.11, 0, 0.115, 0.11, 0.115, 16, 6, -1, 0.15);
            c.Col(glass); c.Ellipsoid(0, g - 0.11, 0, 0.115, 0.11, 0.115, 16, 5, 0.15, 1);
            c.Col(sand); c.Ellipsoid(0, g + 0.11, 0, 0.115, 0.11, 0.115, 16, 3, -1, -0.55);
            c.Col(glass); c.Ellipsoid(0, g + 0.11, 0, 0.115, 0.11, 0.115, 16, 6, -0.55, 1);
            c.Col(sand); c.Cylinder(0, g - 0.02, 0, 0.012, 0.012, g + 0.02, 8, false);
            c.Col(White);
            foreach (double dy in new[] { -0.1, 0.12 }) c.Ellipsoid(0.1, g + dy, 0.045, 0.006, 0.04, 0.012, 5, 3);
        }

        /// <summary>Sundial: bronze dial with Roman numerals and a gnomon on a stone pedestal, tilted towards the class.</summary>
        static void TimeSundial(Model m)
        {
            var s = m.Get(StoneTex());
            var c = m.Plain;
            s.Col(C(215, 210, 200));
            s.Box(0, 0.05, 0, 0.5, 0.1, 0.5);
            s.Cylinder(0, 0.1, 0, 0.17, 0.14, 0.18, 16);
            s.Cylinder(0, 0.18, 0, 0.11, 0.1, 0.68, 16, true, 2);
            s.Cylinder(0, 0.68, 0, 0.13, 0.22, 0.76, 16);
            s.Cylinder(0, 0.76, 0, 0.28, 0.28, 0.79, 24);
            m.Push(Mul(T(0, 0.79, 0), M4.RotZ(-0.22)));
            c.Col(C(175, 125, 65)); c.Cylinder(0, 0, 0, 0.26, 0.26, 0.02, 32);
            var t = m.Get(SundialTex());
            t.Col(White); PolyUp(t, 0.0205, Oval(0, 0, 0.25, 0.25, 32), 0, 0, 0.25, 0.25);
            c.Col(C(120, 80, 40)); Prism(c, new[] { 0.2, 0.0205, -0.18, 0.0205, -0.18, 0.2 }, -0.007, 0.007, MapSide);
            m.Pop();
        }

        // ================================================================== music and art

        /// <summary>Upright piano (keys towards +x, back at x = -0.25) with sheet music and a metronome.</summary>
        static void ArtPiano(Model m)
        {
            var w = m.Get(WoodTex());
            var c = m.Plain;
            var wood = C(110, 62, 40);
            const double W = 1.16;
            w.Col(wood); w.Box(-0.1, 0.56, 0, 0.3, 1.04, W);
            w.Col(Shade(wood, 12)); w.Box(-0.09, 1.075, 0, 0.34, 0.03, W + 0.02);
            w.Col(wood); w.Box(0.065, 0.9, 0, 0.03, 0.3, W - 0.08);
            c.Col(Gold); c.Box(0.081, 1.03, 0, 0.003, 0.008, W - 0.14); c.Box(0.081, 0.77, 0, 0.003, 0.008, W - 0.14);
            w.Col(Shade(wood, 10)); w.Box(0.1, 0.79, 0, 0.04, 0.012, 0.6);
            m.Push(Mul(T(0.095, 0.796, 0), M4.RotZ(0.06)));
            var sm = m.Get(SheetMusicTex());
            sm.Col(White); FaceX(sm, 0.0, 0.11, 0, 0.42, 0.22);
            m.Pop();
            w.Col(wood);
            w.Box(0.12, 0.655, 0, 0.26, 0.05, W);
            for (int s = -1; s <= 1; s += 2) w.Box(0.12, 0.71, s * (W / 2 - 0.03), 0.26, 0.11, 0.06);
            w.Box(0.06, 0.73, 0, 0.04, 0.06, W - 0.12);
            c.Col(Gold); c.Box(0.081, 0.745, 0, 0.002, 0.012, 0.08);
            const int NW = 36;
            double kw = (W - 0.12) / NW, z0 = (W - 0.12) / 2;
            for (int i = 0; i < NW; i++)
            {
                c.Col(C(250, 250, 245)); c.Box(0.17, 0.692, z0 - (i + 0.5) * kw, 0.15, 0.024, kw - 0.003);
                int n = i % 7;
                if (i < NW - 1 && n != 2 && n != 6) { c.Col(C(25, 25, 28)); c.Box(0.14, 0.712, z0 - (i + 1) * kw, 0.09, 0.02, kw * 0.55); }
            }
            w.Col(Shade(wood, 8)); w.Box(0.057, 0.33, 0, 0.015, 0.5, W - 0.2);
            for (int s = -1; s <= 1; s += 2)
            {
                w.Col(wood); w.Box(0.12, 0.03, s * (W / 2 - 0.04), 0.26, 0.06, 0.06);
                w.Cylinder(0.215, 0.06, s * (W / 2 - 0.04), 0.03, 0.024, 0.63, 10);
            }
            c.Col(Gold);
            for (int k = -1; k <= 1; k++) c.Box(0.1, 0.04, k * 0.06, 0.08, 0.012, 0.025);
            // metronome on the lid
            w.Col(C(160, 100, 55));
            w.Push(Mul(T(-0.1, 1.09, 0.38), M4.RotY(PI / 4)));
            w.Cylinder(0, 0, 0, 0.075, 0.025, 0.17, 4);
            w.Pop();
            c.Col(C(220, 220, 225)); c.Rod(P(-0.06, 1.11, 0.38), P(-0.05, 1.25, 0.4), 0.003, 4);
            c.Col(Gold); c.Box(-0.054, 1.2, 0.393, 0.01, 0.02, 0.02);
        }

        /// <summary>Wooden easel with a colourful painting, a brush jar and paint tubes on the ledge.</summary>
        static void ArtEasel(Model m)
        {
            var w = m.Get(WoodTex());
            var c = m.Plain;
            w.Col(C(205, 155, 100));
            for (int s = -1; s <= 1; s += 2) w.Rod(P(0.22, 0, s * 0.34), P(0.0, 1.32, s * 0.08), 0.022, 8);
            w.Rod(P(-0.42, 0, 0), P(-0.01, 1.26, 0), 0.02, 8);
            w.Box(0.0, 1.3, 0, 0.05, 0.05, 0.24);
            w.Box(0.15, 0.55, 0, 0.08, 0.025, 0.78);
            w.Box(0.188, 0.572, 0, 0.012, 0.03, 0.78);
            w.Rod(P(0.178, 0.25, -0.29), P(0.178, 0.25, 0.29), 0.012, 6);
            double a = Math.Atan2(0.22, 1.32);
            m.Push(Mul(T(0.105, 0.885, 0), M4.RotZ(a)));
            c.Col(C(245, 240, 230)); c.Box(0, 0, 0, 0.03, 0.62, 0.74);
            var p = m.Get(PaintingTex());
            p.Col(White); FaceX(p, 0.0155, 0, 0, 0.72, 0.6);
            m.Pop();
            w.Col(C(185, 135, 85)); w.Box(0.05, 1.215, 0, 0.06, 0.04, 0.12);
            // brush jar and paint tubes
            double ly = 0.5625;
            c.Col(C(215, 238, 246)); c.Cylinder(0.15, ly, 0.3, 0.028, 0.028, ly + 0.07, 12);
            c.Col(C(140, 190, 230)); c.Cylinder(0.15, ly + 0.002, 0.3, 0.03, 0.03, ly + 0.04, 12);
            var tips = new[] { C(230, 50, 50), C(250, 200, 40), C(50, 110, 220) };
            for (int k = 0; k < 3; k++)
            {
                var top = P(0.15 + (k - 1) * 0.012, ly + 0.17, 0.3 + (k - 1) * 0.02);
                c.Col(C(150, 95, 50)); c.Rod(P(0.15, ly + 0.02, 0.3), top, 0.005, 4);
                c.Col(tips[k]); Cone(c, top, top + P(0, 0.025, 0), 0.006, 0.001, 5);
            }
            var tubes = new[] { C(230, 50, 50), C(70, 180, 80), C(50, 110, 220), C(250, 200, 40) };
            for (int k = 0; k < 4; k++)
            {
                c.Col(tubes[k]);
                c.Push(Mul(T(0.15, ly + 0.012, -0.3 + k * 0.045), M4.RotZ(PI / 2)));
                c.Cylinder(0, -0.035, 0, 0.011, 0.011, 0.03, 8);
                c.Col(White); c.Cylinder(0, 0.03, 0, 0.006, 0.006, 0.042, 6);
                c.Pop();
            }
        }

        /// <summary>Drum on its axis (tilt leans the top towards -x): shell, chrome hoops, white heads.</summary>
        static void Drum(MeshBuilder c, V3 p, double r, double h, double tilt, Color shell)
        {
            c.Push(Mul(T(p), M4.RotZ(tilt)));
            c.Col(shell); c.Cylinder(0, -h / 2, 0, r, r, h / 2, 12, false);
            c.Col(C(210, 212, 220));
            c.Cylinder(0, h / 2 - 0.012, 0, r + 0.006, r + 0.006, h / 2 + 0.004, 12, false);
            c.Cylinder(0, -h / 2 - 0.004, 0, r + 0.006, r + 0.006, -h / 2 + 0.012, 12, false);
            for (int k = 0; k < 4; k++) c.Box(Math.Cos(k * PI / 2 + 0.4) * (r + 0.006), 0, Math.Sin(k * PI / 2 + 0.4) * (r + 0.006), 0.012, h * 0.5, 0.012);
            c.Col(C(245, 242, 235));
            c.Cylinder(0, h / 2, 0, r, r, h / 2 + 0.003, 12);
            c.Cylinder(0, -h / 2 - 0.003, 0, r, r, -h / 2, 12);
            c.Pop();
        }

        static void Cymbal(MeshBuilder c, V3 p, double r, double tilt)
        {
            c.Push(Mul(T(p), M4.RotZ(tilt)));
            c.Col(C(225, 185, 80)); c.Cylinder(0, 0, 0, r, 0.03, 0.018, 14);
            c.Col(C(240, 205, 110)); c.Ellipsoid(0, 0.018, 0, 0.03, 0.015, 0.03, 8, 3, 0, 1);
            c.Pop();
        }

        static void Tripod(MeshBuilder c, double x, double z, double top, double spread)
        {
            c.Col(C(200, 202, 210));
            c.Rod(P(x, 0.15, z), P(x, top, z), 0.009, 6);
            for (int k = 0; k < 3; k++)
            {
                double a = k * 2 * PI / 3 + 0.3;
                c.Rod(P(x, 0.17, z), P(x + Math.Cos(a) * spread, 0.0, z + Math.Sin(a) * spread), 0.007, 4);
            }
        }

        /// <summary>Small drum set facing the class: bass drum with a logo, two toms, snare, floor tom, hi-hat, crash cymbal and a stool.</summary>
        static void ArtDrums(Model m)
        {
            var c = m.Plain;
            var shell = C(200, 35, 50);
            c.Push(Mul(T(0.05, 0.24, 0), M4.RotZ(-PI / 2)));
            c.Col(shell); c.Cylinder(0, -0.15, 0, 0.23, 0.23, 0.15, 18, false);
            c.Col(C(210, 212, 220)); c.Cylinder(0, -0.165, 0, 0.237, 0.237, -0.145, 18, false); c.Cylinder(0, 0.145, 0, 0.237, 0.237, 0.165, 18, false);
            c.Col(C(245, 242, 235)); c.Cylinder(0, -0.162, 0, 0.228, 0.228, -0.158, 18);
            c.Pop();
            var dh = m.Get(DrumHeadTex());
            dh.Col(White); DiscX(dh, 0.05 + 0.161, 0.24, 0, 0.228, 28);
            c.Col(C(210, 212, 220));
            for (int s = -1; s <= 1; s += 2) c.Rod(P(0.12, 0.12, s * 0.2), P(0.18, 0.0, s * 0.3), 0.008, 5);
            c.Col(C(60, 60, 65)); c.Box(-0.2, 0.01, 0, 0.2, 0.02, 0.07);
            c.Rod(P(-0.15, 0.02, 0), P(-0.115, 0.24, 0), 0.006, 5);
            c.Col(White); c.Sphere(-0.11, 0.24, 0, 0.022, 6, 3);
            for (int s = -1; s <= 1; s += 2)
            {
                c.Col(C(210, 212, 220)); c.Rod(P(0.05, 0.47, 0), P(0.02, 0.53, s * 0.12), 0.008, 5);
                Drum(c, P(0.02, 0.57, s * 0.12), 0.09, 0.09, 0.35, shell);
            }
            Tripod(c, -0.27, -0.16, 0.45, 0.14);
            Drum(c, P(-0.27, 0.5, -0.16), 0.12, 0.065, 0.12, shell);
            c.Col(C(225, 190, 140));
            c.Rod(P(-0.33, 0.545, -0.2), P(-0.16, 0.56, -0.08), 0.006, 5);
            c.Rod(P(-0.33, 0.545, -0.1), P(-0.17, 0.56, -0.22), 0.006, 5);
            Drum(c, P(-0.2, 0.32, 0.36), 0.13, 0.2, 0, shell);
            c.Col(C(200, 202, 210));
            for (int k = 0; k < 3; k++)
            {
                double a = k * 2 * PI / 3;
                c.Rod(P(-0.2 + Math.Cos(a) * 0.14, 0.3, 0.36 + Math.Sin(a) * 0.14), P(-0.2 + Math.Cos(a) * 0.16, 0.0, 0.36 + Math.Sin(a) * 0.16), 0.007, 4);
            }
            Tripod(c, -0.2, -0.42, 0.75, 0.13);
            Cymbal(c, P(-0.2, 0.735, -0.42), 0.13, 0);
            Cymbal(c, P(-0.2, 0.765, -0.42), 0.13, 0);
            Tripod(c, 0.22, 0.42, 0.98, 0.13);
            Cymbal(c, P(0.2, 1.0, 0.41), 0.16, 0.25);
            c.Col(C(30, 30, 30)); c.Cylinder(-0.48, 0.42, 0, 0.12, 0.12, 0.47, 14);
            Tripod(c, -0.48, 0, 0.42, 0.14);
        }

        /// <summary>Acoustic guitar leaning back on a black stand, face towards +x.</summary>
        static void ArtGuitar(Model m)
        {
            var c = m.Plain;
            c.Col(C(35, 35, 40));
            c.Rod(P(-0.25, 0.0, 0), P(-0.13, 0.93, 0), 0.012, 6);
            c.Rod(P(-0.13, 0.93, -0.045), P(-0.13, 0.93, 0.045), 0.01, 5);
            for (int s = -1; s <= 1; s += 2)
            {
                c.Rod(P(-0.13, 0.93, s * 0.045), P(-0.1, 0.97, s * 0.05), 0.008, 5);
                c.Rod(P(0.2, 0.0, s * 0.2), P(0.08, 0.15, s * 0.12), 0.01, 5);
                c.Rod(P(0.08, 0.15, s * 0.12), P(-0.21, 0.32, 0), 0.009, 5);
                c.Rod(P(0.08, 0.15, s * 0.12), P(0.13, 0.2, s * 0.12), 0.01, 5);
            }
            c.Rod(P(0.08, 0.15, -0.12), P(0.08, 0.15, 0.12), 0.01, 5);
            m.Push(Mul(T(0.06, 0.16, 0), M4.RotZ(0.2)));
            var side = C(120, 60, 25);
            var top = C(228, 165, 85);
            c.Col(side);
            Prism(c, Oval(0.21, 0, 0.21, 0.205, 22), -0.05, 0.05, MapFront);
            Prism(c, Oval(0.5, 0, 0.155, 0.15, 20), -0.05, 0.05, MapFront);
            c.Col(top);
            PolyX(c, 0.0505, Oval(0.21, 0, 0.198, 0.193, 22), 0.21, 0, 1, 1);
            PolyX(c, 0.0505, Oval(0.5, 0, 0.143, 0.138, 20), 0.5, 0, 1, 1);
            c.Col(C(30, 20, 15)); DiscX(c, 0.052, 0.42, 0, 0.055, 16);
            c.Col(C(140, 90, 40)); c.Torus(0.052, 0.42, 0, 0.065, 0.006, 16, 3);
            c.Col(C(80, 45, 25)); FlatOval(c, 0.0512, 0.33, -0.08, 0.06, 0.045, 0.3, 10);
            c.Col(C(60, 35, 20)); c.Box(0.056, 0.16, 0, 0.012, 0.025, 0.13);
            c.Col(C(240, 235, 220)); c.Box(0.063, 0.165, 0, 0.003, 0.006, 0.08);
            c.Col(C(160, 100, 50)); c.Box(0.02, 0.83, 0, 0.04, 0.46, 0.05);
            c.Col(C(50, 30, 20)); c.Box(0.046, 0.8, 0, 0.012, 0.5, 0.052);
            c.Col(C(210, 210, 215));
            for (int k = 0; k < 12; k++) c.Box(0.0525, 1.05 - 0.04 * k * (1 - k * 0.02), 0, 0.003, 0.003, 0.052);
            c.Col(C(240, 240, 235));
            foreach (int k in new[] { 3, 5, 7, 9 }) c.Box(0.0525, 1.05 - 0.04 * (k - 0.5) * (1 - (k - 0.5) * 0.02), 0, 0.002, 0.008, 0.008);
            c.Box(0.048, 1.056, 0, 0.012, 0.008, 0.052);
            c.Push(Mul(T(0.018, 1.06, 0), M4.RotZ(0.2)));
            c.Col(C(60, 35, 20)); c.Box(0, 0.07, 0, 0.025, 0.13, 0.075);
            c.Col(C(215, 215, 220));
            for (int k = 0; k < 3; k++)
                for (int s = -1; s <= 1; s += 2)
                {
                    c.Rod(P(0, 0.03 + k * 0.04, s * 0.035), P(0, 0.03 + k * 0.04, s * 0.06), 0.005, 4);
                    c.Sphere(0, 0.03 + k * 0.04, s * 0.066, 0.01, 6, 3);
                }
            c.Pop();
            c.Col(C(235, 230, 215));
            for (int k = 0; k < 6; k++) c.Rod(P(0.062, 0.165, -0.025 + k * 0.01), P(0.055, 1.056, -0.02 + k * 0.008), 0.0015, 3);
            m.Pop();
        }

        // ================================================================== health and sport

        /// <summary>Double-sided net cone (basketball net) with NetTex cells: one per slice, rows down.</summary>
        static void NetCone(MeshBuilder b, double cx, double cz, double yTop, double yBot, double rTop, double rBot, int slices, int rows)
        {
            for (int j = 0; j < slices; j++)
            {
                double a0 = 2 * PI * j / slices, a1 = 2 * PI * (j + 1) / slices;
                V3 t0 = P(cx + Math.Cos(a0) * rTop, yTop, cz + Math.Sin(a0) * rTop), t1 = P(cx + Math.Cos(a1) * rTop, yTop, cz + Math.Sin(a1) * rTop);
                V3 b0 = P(cx + Math.Cos(a0) * rBot, yBot, cz + Math.Sin(a0) * rBot), b1 = P(cx + Math.Cos(a1) * rBot, yBot, cz + Math.Sin(a1) * rBot);
                NetQuad(b, b0, b1, t1, t0, new float[] { j, rows, j + 1, rows, j + 1, 0, j, 0 });
            }
        }

        /// <summary>Mobile basketball hoop ≈1.4 tall: weighted base at -x, pole, backboard, orange rim with a net, a ball on the floor.</summary>
        static void SportHoop(Model m)
        {
            var c = m.Plain;
            var steel = C(60, 62, 72);
            c.Col(C(40, 40, 45)); c.Box(-0.38, 0.08, 0, 0.44, 0.16, 0.6);
            c.Col(C(230, 110, 30)); c.Box(-0.38, 0.161, 0, 0.3, 0.004, 0.06);
            c.Col(C(20, 20, 20));
            for (int s = -1; s <= 1; s += 2)
            {
                c.Push(Mul(T(-0.55, 0.05, s * 0.22), M4.RotX(PI / 2)));
                c.Cylinder(0, -0.02, 0, 0.05, 0.05, 0.02, 10);
                c.Pop();
            }
            c.Col(steel);
            c.Rod(P(-0.4, 0.16, 0), P(-0.4, 1.22, 0), 0.03, 10);
            c.Rod(P(-0.2, 0.16, 0), P(-0.4, 0.6, 0), 0.018, 6);
            c.Rod(P(-0.4, 1.06, 0), P(-0.1, 1.06, 0), 0.018, 6);
            c.Rod(P(-0.4, 1.2, 0), P(-0.1, 1.2, 0), 0.018, 6);
            c.Col(White); c.Box(-0.09, 1.175, 0, 0.02, 0.45, 0.62);
            var bb = m.Get(BackboardTex());
            bb.Col(White); FaceX(bb, -0.0795, 1.175, 0, 0.62, 0.45);
            c.Col(C(240, 110, 30)); Hoop(c, 0.06, 1.0, 0, 0.12, 0.009, 20, 5);
            c.Box(-0.06, 0.995, 0, 0.04, 0.02, 0.08);
            var n = m.Get(NetTex(), true);
            n.Col(White); NetCone(n, 0.06, 0, 0.995, 0.81, 0.118, 0.075, 12, 3);
            var ball = m.Get(BasketTex());
            ball.Col(White); ball.Ellipsoid(0.25, 0.12, 0.3, 0.12, 0.12, 0.12, 16, 10);
        }

        /// <summary>Blue gym mat (0.8 × 1.2) flat on the floor with a red rolled mat and a yellow skipping rope on it.</summary>
        static void SportMat(Model m)
        {
            var c = m.Plain;
            var blue = C(50, 110, 210);
            c.Col(blue); SoftBox(c, 0, 0.03, 0, 0.4, 0.03, 0.6, 0.25, 16, 6);
            c.Col(Shade(blue, -35));
            foreach (double z in new[] { -0.2, 0.2 }) c.Box(0, 0.0605, z, 0.74, 0.002, 0.008);
            for (int s = -1; s <= 1; s += 2)
                foreach (double z in new[] { -0.35, 0.35 }) c.Box(s * 0.398, 0.03, z, 0.008, 0.016, 0.09);
            // rolled mat lying across the far end
            m.Push(Mul(T(-0.2, 0.17, 0), M4.RotX(PI / 2)));
            c.Col(C(230, 70, 60)); c.Cylinder(0, -0.3, 0, 0.11, 0.11, 0.3, 18);
            c.Col(C(30, 30, 30));
            c.Cylinder(0, -0.17, 0, 0.113, 0.113, -0.14, 18, false); c.Cylinder(0, 0.14, 0, 0.113, 0.113, 0.17, 18, false);
            c.Col(C(180, 45, 40));
            foreach (double r in new[] { 0.035, 0.07 }) Annulus(c, 0, 0.301, 0, r, r + 0.008, 16);
            c.Push(M4.RotX(PI));
            foreach (double r in new[] { 0.035, 0.07 }) Annulus(c, 0, 0.301, 0, r, r + 0.008, 16);
            c.Pop();
            m.Pop();
            // skipping rope with wooden handles
            var pts = new V3[22];
            for (int k = 0; k < pts.Length; k++)
            {
                double t = k / (double)(pts.Length - 1);
                pts[k] = P(0.16 + 0.15 * Math.Sin(t * PI * 2.2), 0.067, -0.42 + 0.84 * t);
            }
            c.Col(C(250, 200, 40)); Tube(c, pts, 0.007, 4);
            c.Col(C(190, 140, 90));
            Cone(c, pts[0], pts[0] + (pts[0] - pts[1]).Normalized() * 0.1, 0.014, 0.014, 8);
            int e = pts.Length - 1;
            Cone(c, pts[e], pts[e] + (pts[e] - pts[e - 1]).Normalized() * 0.1, 0.014, 0.014, 8);
        }

        /// <summary>Metal ball rack on wheels: basketballs at the bottom, footballs in the middle, volleyballs on top.</summary>
        static void SportRack(Model m)
        {
            var c = m.Plain;
            c.Col(C(150, 155, 165));
            double[] hs = { 0.12, 0.43, 0.74 };
            for (int a = -1; a <= 1; a += 2)
                for (int b = -1; b <= 1; b += 2)
                {
                    c.Rod(P(a * 0.17, 0.05, b * 0.5), P(a * 0.17, 1.0, b * 0.5), 0.016, 5);
                    c.Sphere(a * 0.17, 1.0, b * 0.5, 0.02, 6, 3);
                }
            foreach (double h in hs)
            {
                for (int a = -1; a <= 1; a += 2) c.Rod(P(a * 0.08, h, -0.5), P(a * 0.08, h, 0.5), 0.011, 5);
                for (int b = -1; b <= 1; b += 2) c.Rod(P(-0.17, h, b * 0.5), P(0.17, h, b * 0.5), 0.011, 5);
            }
            for (int a = -1; a <= 1; a += 2) c.Rod(P(a * 0.17, 1.0, -0.5), P(a * 0.17, 1.0, 0.5), 0.011, 5);
            c.Col(C(30, 30, 30));
            for (int a = -1; a <= 1; a += 2)
                for (int b = -1; b <= 1; b += 2)
                {
                    c.Push(Mul(T(a * 0.17, 0.035, b * 0.5), M4.RotX(PI / 2)));
                    c.Cylinder(0, -0.012, 0, 0.035, 0.035, 0.012, 10);
                    c.Pop();
                }
            var tex = new[] { BasketTex(), BallTex(), VolleyTex() };
            for (int i = 0; i < 3; i++)
            {
                var b = m.Get(tex[i]);
                b.Col(White);
                for (int k = 0; k < 3; k++) b.Ellipsoid(0, hs[i] + 0.0755, -0.33 + k * 0.33, 0.11, 0.11, 0.11, 12, 6);
            }
        }

        // ================================================================== science and space

        /// <summary>White refractor telescope on a tripod, pointing up towards -z (seen from the side by the class).</summary>
        static void ScienceTelescope(Model m)
        {
            var c = m.Plain;
            var navy = SubjectColor(9);
            var apex = P(0, 0.78, 0);
            for (int k = 0; k < 3; k++)
            {
                double a = k * 2 * PI / 3 + PI / 3, ca = Math.Cos(a), sa = Math.Sin(a);
                c.Col(C(175, 180, 190)); c.Rod(apex + P(ca * 0.03, -0.02, sa * 0.03), P(ca * 0.42, 0.02, sa * 0.42), 0.016, 6);
                c.Col(C(30, 30, 30)); c.Sphere(ca * 0.42, 0.02, sa * 0.42, 0.022, 6, 3);
                c.Col(C(120, 125, 135)); c.Rod(P(ca * 0.2, 0.345, sa * 0.2), P(ca * 0.25, 0.35, sa * 0.25), 0.008, 4);
            }
            c.Col(C(60, 60, 70));
            Prism(c, Oval(0, 0, 0.2, 0.2, 3, PI / 3, PI / 3 + 2 * PI), 0.335, 0.35, MapFloor);
            c.Col(C(40, 40, 45)); c.Cylinder(0, 0.75, 0, 0.06, 0.05, 0.84, 12);
            c.Box(0, 0.88, 0, 0.045, 0.08, 0.045);
            var d = P(0.25, 0.55, -0.8).Normalized();
            var ctr = P(0, 0.94, 0);
            V3 a0 = ctr - d * 0.32, a1 = ctr + d * 0.45;
            c.Col(C(245, 245, 250)); Cone(c, a0, a1, 0.065, 0.065, 16);
            c.Col(navy);
            Cone(c, a1 - d * 0.02, a1 + d * 0.12, 0.074, 0.074, 16, false);
            Cone(c, ctr - d * 0.06, ctr - d * 0.01, 0.069, 0.069, 16);
            Cone(c, ctr + d * 0.2, ctr + d * 0.25, 0.069, 0.069, 16);
            c.Col(C(40, 70, 140)); Cone(c, a1 + d * 0.02, a1 + d * 0.024, 0.07, 0.07, 16);
            c.Col(C(40, 40, 45)); c.Rod(P(0, 0.9, 0), ctr - d * 0.03, 0.02, 6);
            var up = (P(0, 1, 0) - d * V3.Dot(P(0, 1, 0), d)).Normalized();
            c.Col(C(30, 30, 35)); Cone(c, a0 - d * 0.07, a0, 0.03, 0.03, 10);
            var e0 = a0 - d * 0.05;
            c.Col(C(70, 70, 75)); Cone(c, e0, e0 + up * 0.08, 0.018, 0.018, 8);
            c.Col(C(20, 20, 20)); Cone(c, e0 + up * 0.08, e0 + up * 0.1, 0.022, 0.022, 8);
            var fs = ctr + up * 0.1;
            c.Col(C(240, 240, 245)); Cone(c, fs - d * 0.08, fs + d * 0.13, 0.018, 0.018, 8);
            c.Col(C(40, 40, 45));
            foreach (double t in new[] { -0.03, 0.08 }) c.Rod(fs + d * t, fs + d * t - up * 0.04, 0.006, 4);
        }

        /// <summary>Volcano model on a table: layered cone, glowing crater, red lava streams and a puff of smoke.</summary>
        static void ScienceVolcano(Model m)
        {
            double top = Table(m, 0.75, 0.75, 0.5);
            var c = m.Plain;
            c.Col(C(110, 175, 80)); c.Box(0, top + 0.01, 0, 0.66, 0.02, 0.66);
            double y = top + 0.02;
            Func<double, double> R = h => h < 0.12 ? 0.3 - 0.1 * h / 0.12 : h < 0.27 ? 0.2 - 0.09 * (h - 0.12) / 0.15 : 0.11 - 0.035 * (h - 0.27) / 0.09;
            c.Col(C(135, 95, 60)); c.Cylinder(0, y, 0, 0.3, 0.2, y + 0.12, 20, false);
            c.Col(C(115, 85, 60)); c.Cylinder(0, y + 0.12, 0, 0.2, 0.11, y + 0.27, 20, false);
            c.Col(C(90, 80, 75)); c.Cylinder(0, y + 0.27, 0, 0.11, 0.075, y + 0.36, 18, false);
            c.Col(C(70, 60, 55)); Hoop(c, 0, y + 0.36, 0, 0.07, 0.01, 18, 4);
            c.Col(C(230, 70, 30)); InnerWall(c, 0, 0, 0.068, y + 0.3, y + 0.36, 16);
            c.Col(C(255, 140, 30)); Annulus(c, 0, y + 0.345, 0, 0, 0.069, 16);
            c.Col(C(255, 200, 60)); c.Ellipsoid(0, y + 0.35, 0, 0.045, 0.025, 0.045, 10, 4);
            var rocks = new Random(5);
            for (int k = 0; k < 10; k++)
            {
                double h = 0.03 + rocks.NextDouble() * 0.28, a = rocks.NextDouble() * 2 * PI, rr = R(h);
                c.Col(C(80, 70, 65)); c.Sphere(Math.Cos(a) * rr, y + h, Math.Sin(a) * rr, 0.018, 5, 2);
            }
            foreach (double a in new[] { 0.15, 1.4, 2.5, -0.9 })
            {
                var pts = new List<V3>();
                for (int k = 0; k <= 6; k++)
                {
                    double h = 0.36 - k * 0.055, aa = a + 0.12 * Math.Sin(k * 1.3 + a), rr = R(h) + 0.008;
                    pts.Add(P(Math.Cos(aa) * rr, y + h, Math.Sin(aa) * rr));
                }
                c.Col(C(235, 60, 25)); Tube(c, pts.ToArray(), 0.02, 5);
                var last = pts[pts.Count - 1];
                c.Col(C(255, 120, 30)); c.Ellipsoid(last.X * 1.08, y + 0.006, last.Z * 1.08, 0.05, 0.012, 0.05, 8, 2);
            }
            c.Col(C(205, 205, 210));
            c.Sphere(0.0, y + 0.45, 0.0, 0.05, 8, 4);
            c.Sphere(-0.03, y + 0.53, 0.03, 0.06, 8, 4);
            c.Sphere(-0.07, y + 0.62, 0.06, 0.07, 8, 4);
            for (int k = 0; k < 6; k++)
            {
                double a = k * 1.05 + 0.4, x = Math.Cos(a) * 0.3, z = Math.Sin(a) * 0.3;
                if (Math.Abs(x) > 0.3 || Math.Abs(z) > 0.3) continue;
                c.Col(C(40, 120, 50)); c.Cylinder(x, y, z, 0.03, 0, y + 0.09, 6);
            }
        }

        /// <summary>Planet mobile: the Sun and eight planets (Earth with a map, Jupiter banded, Saturn with a ring) hanging on
        /// wires from a bar at y = 1.45 (lowest point ≈0.93); nothing reaches the floor although the origin is on the floor.</summary>
        static void SciencePlanets(Model m)
        {
            var c = m.Plain;
            c.Col(C(150, 110, 70)); c.Rod(P(0, 1.45, -0.56), P(0, 1.45, 0.56), 0.012, 8);
            c.Sphere(0, 1.45, -0.56, 0.018, 6, 3); c.Sphere(0, 1.45, 0.56, 0.018, 6, 3);
            c.Col(C(120, 120, 125));
            foreach (double z in new[] { -0.3, 0.3 }) c.Rod(P(0, 1.45, z), P(0, 1.497, z), 0.003, 4);
            // z, y, radius
            var pl = new[]
            {
                new[] { -0.44, 1.17, 0.12 }, new[] { -0.27, 1.3, 0.025 }, new[] { -0.19, 1.18, 0.04 }, new[] { -0.08, 1.08, 0.05 },
                new[] { 0.03, 1.25, 0.035 }, new[] { 0.16, 1.02, 0.085 }, new[] { 0.33, 1.2, 0.065 }, new[] { 0.45, 1.06, 0.045 }, new[] { 0.53, 1.27, 0.04 },
            };
            var cols = new[] { C(255, 200, 40), C(160, 155, 150), C(235, 200, 130), C(0, 0, 0), C(215, 90, 50), C(0, 0, 0), C(232, 208, 145), C(150, 215, 228), C(60, 100, 215) };
            for (int k = 0; k < pl.Length; k++)
            {
                double z = pl[k][0], y = pl[k][1], r = pl[k][2];
                c.Col(C(120, 120, 125)); c.Rod(P(0, 1.45, z), P(0, y + r * 0.95, z), 0.0025, 4);
                if (k == 3) { var g = m.Get(GlobeTex()); g.Col(White); g.Ellipsoid(0, y, z, r, r, r, 14, 8); }
                else if (k == 5) { var j = m.Get(JupiterTex()); j.Col(White); j.Ellipsoid(0, y, z, r, r, r, 16, 9); }
                else { c.Col(cols[k]); c.Ellipsoid(0, y, z, r, r, r, k == 0 ? 14 : r < 0.05 ? 10 : 12, k == 0 ? 8 : r < 0.05 ? 5 : 7); }
            }
            // sun rays and Saturn's ring
            c.Col(C(255, 160, 30));
            for (int k = 0; k < 8; k++)
            {
                double a = k * PI / 4;
                var dir = P(0, Math.Sin(a), Math.Cos(a));
                Cone(c, P(0, 1.17, -0.44) + dir * 0.11, P(0, 1.17, -0.44) + dir * 0.16, 0.025, 0, 5);
            }
            c.Push(Mul(T(0, 1.2, 0.33), M4.RotX(0.45)));
            c.Col(C(215, 190, 135)); Annulus(c, 0, 0.001, 0, 0.085, 0.13, 24);
            c.Push(M4.RotX(PI)); Annulus(c, 0, 0.001, 0, 0.085, 0.13, 24); c.Pop();
            c.Col(C(180, 160, 115)); Annulus(c, 0, 0.0015, 0, 0.105, 0.11, 24);
            c.Pop();
            c.Col(C(200, 200, 200)); c.Sphere(0, 1.1, -0.02, 0.014, 6, 3);   // the Moon near the Earth
            c.Col(C(120, 120, 125)); c.Rod(P(0, 1.45, -0.02), P(0, 1.114, -0.02), 0.002, 3);
        }

        /// <summary>Lab table with a Bunsen burner under a beaker, a test-tube rack, flasks and coloured liquids.</summary>
        static void ScienceLab(Model m)
        {
            var c = m.Plain;
            var glass = C(215, 238, 246);
            c.Col(C(235, 235, 230)); c.Box(-0.02, 0.29, 0, 0.52, 0.58, 1.06);
            for (int row = 0; row < 2; row++)
                for (int s = -1; s <= 1; s += 2)
                {
                    double y = 0.42 - row * 0.24;
                    c.Col(C(200, 205, 212)); c.Box(0.2405, y, s * 0.26, 0.004, 0.2, 0.48);
                    c.Col(C(90, 90, 100)); c.Box(0.247, y + 0.06, s * 0.26, 0.01, 0.015, 0.12);
                }
            c.Col(C(45, 50, 58)); c.Box(0, 0.6, 0, 0.6, 0.04, 1.12);
            double t = 0.62;
            // Bunsen burner, tripod and a beaker of orange liquid
            double bx = 0.05, bz = -0.38;
            c.Col(C(50, 50, 55)); c.Cylinder(bx, t, bz, 0.05, 0.045, t + 0.015, 12);
            c.Col(C(190, 190, 200)); c.Cylinder(bx, t + 0.015, bz, 0.012, 0.012, t + 0.15, 10);
            c.Cylinder(bx, t + 0.04, bz, 0.016, 0.016, t + 0.06, 10);
            c.Col(C(230, 120, 40)); Tube(c, new[] { P(bx - 0.03, t + 0.012, bz), P(bx - 0.12, t + 0.006, bz + 0.03), P(bx - 0.25, t + 0.006, bz + 0.06) }, 0.006, 5);
            c.Col(C(80, 140, 255)); Cone(c, P(bx, t + 0.15, bz), P(bx, t + 0.235, bz), 0.014, 0, 10);
            c.Col(C(180, 235, 255)); Cone(c, P(bx, t + 0.15, bz), P(bx, t + 0.19, bz), 0.008, 0, 8);
            c.Col(C(60, 60, 65));
            for (int k = 0; k < 3; k++)
            {
                double a = k * 2 * PI / 3 + 0.4;
                c.Rod(P(bx + Math.Cos(a) * 0.1, t, bz + Math.Sin(a) * 0.1), P(bx + Math.Cos(a) * 0.07, t + 0.25, bz + Math.Sin(a) * 0.07), 0.005, 4);
            }
            Hoop(c, bx, t + 0.25, bz, 0.07, 0.005, 14, 3);
            c.Col(C(150, 150, 155)); c.Box(bx, t + 0.256, bz, 0.15, 0.004, 0.15);
            c.Col(glass); c.Cylinder(bx, t + 0.258, bz, 0.05, 0.05, t + 0.37, 14, false);
            c.Col(C(255, 140, 40)); c.Cylinder(bx, t + 0.26, bz, 0.052, 0.052, t + 0.32, 14);
            c.Col(C(250, 250, 250));
            for (int k = 0; k < 3; k++) c.Sphere(bx + 0.01 * k, t + 0.39 + k * 0.03, bz - 0.01 * k, 0.015 + k * 0.004, 8, 4);
            // test-tube rack
            c.Col(C(190, 140, 90));
            c.Box(0, t + 0.01, -0.08, 0.1, 0.02, 0.32);
            c.Box(0, t + 0.11, -0.08, 0.1, 0.015, 0.32);
            for (int s = -1; s <= 1; s += 2) c.Box(0, t + 0.06, -0.08 + s * 0.15, 0.1, 0.1, 0.015);
            var liq = new[] { C(230, 40, 50), C(250, 210, 40), C(70, 200, 80), C(50, 120, 230), C(170, 70, 210) };
            for (int k = 0; k < 5; k++)
            {
                double z = -0.08 + (k - 2) * 0.055;
                c.Col(glass); c.Cylinder(0, t + 0.03, z, 0.014, 0.014, t + 0.19, 10, false);
                c.Col(liq[k]); c.Cylinder(0, t + 0.025, z, 0.0155, 0.0155, t + 0.09 + k * 0.012, 10);
            }
            Flask(c, P(0.1, t, 0.12), 1.0, C(170, 70, 220));
            // round flask with green liquid on a retort stand
            c.Col(C(60, 60, 65));
            c.Box(-0.12, t + 0.008, 0.36, 0.2, 0.016, 0.14);
            c.Rod(P(-0.17, t, 0.36), P(-0.17, t + 0.45, 0.36), 0.008, 6);
            c.Rod(P(-0.17, t + 0.33, 0.36), P(-0.02, t + 0.33, 0.36), 0.006, 5);
            Hoop(c, -0.02, t + 0.33, 0.36, 0.022, 0.005, 10, 3);
            double fy = t + 0.1;
            c.Col(C(60, 210, 90)); c.Ellipsoid(-0.02, fy, 0.36, 0.087, 0.085, 0.087, 16, 5, -1, -0.05);
            c.Col(glass); c.Ellipsoid(-0.02, fy, 0.36, 0.085, 0.085, 0.085, 16, 5, -0.05, 1);
            c.Cylinder(-0.02, fy + 0.07, 0.36, 0.018, 0.018, t + 0.36, 10, false);
            // beaker of blue liquid with scale marks
            c.Col(glass); c.Cylinder(0.16, t, 0.32, 0.045, 0.045, t + 0.11, 14, false);
            c.Col(C(50, 120, 230)); c.Cylinder(0.16, t + 0.002, 0.32, 0.047, 0.047, t + 0.07, 14);
            c.Col(White);
            for (int k = 0; k < 4; k++) c.Box(0.206, t + 0.02 + k * 0.022, 0.32, 0.002, 0.003, 0.02);
        }

        // ================================================================== subject textures

        static Tex clockTex, dictTex, checkerTex, chessTex, rulerTex, protractorTex, roseTex, shieldTex, destTex, phoneTex, phoneSignTex,
            bigBenTex, sundialTex, sheetTex, paintingTex, drumTex, backboardTex, basketTex, volleyTex, jupiterTex, mapTex;
        static readonly Tex[] busTex = new Tex[2];

        static void Text(Graphics g, string s, string font, float size, FontStyle st, Color col, float cx, float cy)
        {
            using (var f = new Font(font, size, st, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(col))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(s, f, b, cx, cy, fmt);
        }

        /// <summary>Clock face: gold rim, numbers 1-12, minute ticks, hands at ten past ten.</summary>
        static Tex ClockFaceTex() => clockTex ?? (clockTex = Paint(256, 256, false, g =>
        {
            FillR(g, C(225, 185, 70), 0, 0, 256, 256);
            FillE(g, C(250, 246, 232), 12, 12, 232, 232);
            using (var p = new Pen(C(60, 45, 30), 3)) g.DrawEllipse(p, 16, 16, 224, 224);
            for (int k = 0; k < 60; k++)
            {
                double a = k * PI / 30;
                float sx = (float)Math.Sin(a), sy = (float)-Math.Cos(a), r0 = k % 5 == 0 ? 92 : 100;
                Line(g, C(40, 35, 30), k % 5 == 0 ? 4 : 1.5f, 128 + sx * r0, 128 + sy * r0, 128 + sx * 106, 128 + sy * 106);
            }
            for (int k = 1; k <= 12; k++)
            {
                double a = k * PI / 6;
                Text(g, k.ToString(), "Georgia", 26, FontStyle.Bold, C(40, 35, 30), 128 + (float)Math.Sin(a) * 74, 129 - (float)Math.Cos(a) * 74);
            }
            Func<double, float, PointF> hand = (deg, len) => new PointF(128 + (float)Math.Sin(deg * PI / 180) * len, 128 - (float)Math.Cos(deg * PI / 180) * len);
            var hp = hand(305, 50);
            var mp = hand(60, 80);
            var sp = hand(200, 88);
            Line(g, C(30, 30, 40), 8, 128, 128, hp.X, hp.Y);
            Line(g, C(30, 30, 40), 5, 128, 128, mp.X, mp.Y);
            Line(g, C(200, 40, 40), 2, 128, 128, sp.X, sp.Y);
            FillE(g, C(30, 30, 40), 120, 120, 16, 16);
        }));

        /// <summary>Red dictionary cover with a gold "ABC", the word DICTIONARY and a small British flag.</summary>
        static Tex DictTex() => dictTex ?? (dictTex = Paint(128, 180, false, g =>
        {
            using (var br = new LinearGradientBrush(new RectangleF(-1, 0, 130, 180), C(220, 50, 55), C(170, 25, 35), 0f)) g.FillRectangle(br, 0, 0, 128, 180);
            using (var p = new Pen(C(240, 200, 80), 3)) g.DrawRectangle(p, 9, 9, 110, 162);
            Text(g, "ABC", "Arial Black", 40, FontStyle.Bold, C(90, 10, 15), 66, 66);
            Text(g, "ABC", "Arial Black", 40, FontStyle.Bold, C(255, 215, 80), 64, 64);
            Text(g, "DICTIONARY", "Arial", 13, FontStyle.Bold, C(255, 230, 150), 64, 108);
            FillR(g, C(30, 50, 140), 40, 128, 48, 30);
            Line(g, White, 6, 40, 128, 88, 158); Line(g, White, 6, 88, 128, 40, 158);
            Line(g, C(210, 30, 40), 2, 40, 128, 88, 158); Line(g, C(210, 30, 40), 2, 88, 128, 40, 158);
            FillR(g, White, 40, 139, 48, 8); FillR(g, White, 60, 128, 8, 30);
            FillR(g, C(210, 30, 40), 40, 141, 48, 4); FillR(g, C(210, 30, 40), 62, 128, 4, 30);
        }));

        static Tex CheckerTex() => checkerTex ?? (checkerTex = PerPixel(64, 64, (u, v) => ((int)(u * 8) + (int)(v * 8)) % 2 == 0 ? C(245, 245, 245) : C(30, 30, 32)));

        /// <summary>Chessboard 8×8 with a wooden border (board 0.6 wide, border 0.02).</summary>
        static Tex ChessboardTex() => chessTex ?? (chessTex = Paint(256, 256, false, g =>
        {
            FillR(g, C(110, 65, 30), 0, 0, 256, 256);
            float b = 256 * 0.02f / 0.6f, s = (256 - 2 * b) / 8;
            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 8; j++) FillR(g, (i + j) % 2 == 0 ? C(240, 220, 180) : C(150, 95, 50), b + i * s, b + j * s, s + 0.5f, s + 0.5f);
            using (var p = new Pen(C(230, 190, 90), 1.5f)) g.DrawRectangle(p, b - 1, b - 1, 256 - 2 * b + 2, 256 - 2 * b + 2);
        }));

        /// <summary>Vertical wooden ruler face: 100 marks and the numbers 0-10 from the bottom.</summary>
        static Tex RulerTex() => rulerTex ?? (rulerTex = Paint(64, 512, false, g =>
        {
            using (var br = new LinearGradientBrush(new RectangleF(-1, 0, 66, 512), C(250, 218, 135), C(232, 188, 100), 0f)) g.FillRectangle(br, 0, 0, 64, 512);
            var r = new Random(3);
            for (int k = 0; k < 10; k++) { float x = Rf(r) * 64; Line(g, CA(50, 160, 110, 50), 1, x, 0, x + (Rf(r) - 0.5f) * 6, 512); }
            const float K = 512f / 1.1f;
            for (int i = 0; i <= 100; i++)
            {
                float y = 512 - (0.05f + i / 100f) * K;
                float len = i % 10 == 0 ? 26 : i % 5 == 0 ? 18 : 10;
                Line(g, C(60, 35, 15), i % 10 == 0 ? 2.2f : 1.2f, 64 - len, y, 64, y);
                Line(g, C(60, 35, 15), i % 10 == 0 ? 2.2f : 1.2f, 0, y, i % 10 == 0 ? 8 : 4, y);
                if (i % 10 == 0) Text(g, (i / 10).ToString(), "Arial", 18, FontStyle.Bold, C(60, 35, 15), 24, y);
            }
        }));

        /// <summary>Protractor half disc (base along the bottom edge, centre at the bottom middle): degree marks and numbers.</summary>
        static Tex ProtractorTex() => protractorTex ?? (protractorTex = Paint(256, 128, false, g =>
        {
            FillR(g, C(200, 228, 250), 0, 0, 256, 128);
            const float cx = 128, cy = 128, R = 124;
            using (var b = new SolidBrush(C(225, 240, 252))) g.FillPie(b, cx - 54, cy - 54, 108, 108, 180, 180);
            var ink = C(30, 60, 120);
            for (int d = 0; d <= 180; d++)
            {
                double a = d * PI / 180;
                float len = d % 10 == 0 ? 16 : d % 5 == 0 ? 10 : 6;
                float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
                Line(g, ink, d % 10 == 0 ? 1.6f : 0.9f, cx + ca * R, cy - sa * R, cx + ca * (R - len), cy - sa * (R - len));
                if (d % 20 == 0 && d > 0 && d < 180) Text(g, d.ToString(), "Arial", 11, FontStyle.Bold, ink, cx + ca * (R - 28), cy - sa * (R - 28));
            }
            using (var p = new Pen(ink, 1.2f)) g.DrawArc(p, cx - 84, cy - 84, 168, 168, 180, 180);
            Line(g, ink, 1.5f, 4, 126, 252, 126);
            Line(g, ink, 1.5f, cx, 112, cx, 127);
        }));

        /// <summary>Compass rose rug: red border, cream field, eight-pointed star; N at the top of the image.</summary>
        static Tex CompassRoseTex() => roseTex ?? (roseTex = Paint(512, 512, false, g =>
        {
            const float c = 256;
            FillR(g, C(150, 50, 45), 0, 0, 512, 512);
            FillE(g, C(235, 215, 165), 30, 30, 452, 452);
            for (int k = 0; k < 36; k++)
            {
                double a = k * PI / 18;
                float x = c + (float)Math.Sin(a) * 216, y = c - (float)Math.Cos(a) * 216;
                FillPoly(g, k % 2 == 0 ? C(40, 70, 130) : C(200, 150, 50), x, y - 7, x + 7, y, x, y + 7, x - 7, y);
            }
            FillE(g, C(245, 236, 210), 50, 50, 412, 412);
            using (var p = new Pen(C(150, 50, 45), 3)) g.DrawEllipse(p, 54, 54, 404, 404);
            using (var p = new Pen(C(180, 150, 100), 1.5f)) { g.DrawEllipse(p, 106, 106, 300, 300); g.DrawEllipse(p, 166, 166, 180, 180); }
            for (int pass = 0; pass < 2; pass++)
                for (int k = pass; k < 8; k += 2)
                {
                    double a = k * PI / 4;
                    float len = k % 2 == 0 ? 165 : 105, w = k % 2 == 0 ? 30 : 22;
                    float tx = c + (float)Math.Sin(a) * len, ty = c - (float)Math.Cos(a) * len;
                    float lx = c + (float)Math.Sin(a - PI / 4) * w, ly = c - (float)Math.Cos(a - PI / 4) * w;
                    float rx = c + (float)Math.Sin(a + PI / 4) * w, ry = c - (float)Math.Cos(a + PI / 4) * w;
                    Color dark, light;
                    if (k == 0) { dark = C(180, 30, 30); light = C(240, 90, 80); }
                    else if (k % 2 == 0) { dark = C(35, 55, 110); light = C(95, 135, 205); }
                    else { dark = C(190, 140, 35); light = C(245, 205, 95); }
                    FillPoly(g, dark, c, c, tx, ty, lx, ly);
                    FillPoly(g, light, c, c, tx, ty, rx, ry);
                }
            FillE(g, C(200, 150, 40), c - 16, c - 16, 32, 32);
            FillE(g, C(150, 50, 45), c - 7, c - 7, 14, 14);
            var letters = new[] { "N", "E", "S", "W" };
            for (int k = 0; k < 4; k++)
            {
                double a = k * PI / 2;
                Text(g, letters[k], "Georgia", 40, FontStyle.Bold, k == 0 ? C(180, 30, 30) : C(40, 50, 90), c + (float)Math.Sin(a) * 186, c - (float)Math.Cos(a) * 186);
            }
        }));

        /// <summary>Shield field: a crowned white eagle (Polish coat of arms style) on red.</summary>
        static Tex ShieldTex() => shieldTex ?? (shieldTex = Paint(160, 208, false, g =>
        {
            var red = C(205, 30, 40);
            var wh = C(250, 250, 250);
            var gold = C(245, 200, 60);
            FillR(g, red, 0, 0, 160, 208);
            for (int s = -1; s <= 1; s += 2)
            {
                Func<float, float> X = x => 80 + s * x;
                FillPoly(g, wh, X(8), 70, X(52), 42, X(64), 52, X(58), 62, X(70), 66, X(62), 78, X(70), 84, X(60), 96, X(66), 106, X(50), 112, X(36), 110, X(14), 100);
                Line(g, red, 1.6f, X(26), 102, X(54), 60);
                Line(g, red, 1.6f, X(40), 108, X(60), 80);
                Line(g, red, 1.6f, X(16), 90, X(44), 54);
                Line(g, wh, 7, X(10), 122, X(22), 140);
                Line(g, gold, 2.5f, X(22), 140, X(14), 150); Line(g, gold, 2.5f, X(22), 140, X(20), 152); Line(g, gold, 2.5f, X(22), 140, X(28), 150);
            }
            FillE(g, wh, 64, 60, 32, 66);
            FillPoly(g, wh, 70, 120, 90, 120, 104, 158, 80, 148, 56, 158);
            Line(g, wh, 7, 72, 120, 70, 132); Line(g, wh, 7, 88, 120, 90, 132);
            FillPoly(g, wh, 70, 64, 90, 64, 92, 76, 68, 76);
            FillE(g, wh, 66, 42, 26, 26);
            FillPoly(g, gold, 68, 51, 54, 57, 66, 61);
            FillE(g, C(30, 30, 30), 71, 49, 4, 4);
            FillPoly(g, gold, 66, 46, 66, 33, 71, 39, 76, 29, 81, 39, 86, 33, 86, 46);
        }));

        /// <summary>Side of the double-decker: two rows of windows, a door, "LONDON" on the band. mirror = layout for the other side (front on the left).</summary>
        static Tex BusSideTex(bool mirror)
        {
            int i = mirror ? 1 : 0;
            if (busTex[i] != null) return busTex[i];
            return busTex[i] = Paint(512, 226, false, g =>
            {
                Func<float, float> X = x => mirror ? 512 - x : x;
                Action<Color, float, float, float, float> Rect = (col, x, y, w, h) => FillR(g, col, mirror ? 512 - x - w : x, y, w, h);
                var red = C(210, 30, 35);
                var glass = C(45, 60, 90);
                FillR(g, red, 0, 0, 512, 226);
                for (int k = 0; k < 7; k++)
                {
                    Rect(C(250, 245, 230), 14 + k * 69, 20, 67, 58);
                    Rect(glass, 16 + k * 69, 22, 63, 54);
                    Rect(C(95, 120, 160), 20 + k * 69, 26, 14, 46);
                }
                Rect(C(245, 230, 190), 0, 88, 512, 22);
                Text(g, "LONDON", "Arial Black", 17, FontStyle.Bold, red, X(170), 99);
                FillE(g, red, X(440) - 10, 89, 20, 20);
                FillE(g, C(245, 230, 190), X(440) - 6, 93, 12, 12);
                FillR(g, C(30, 50, 140), X(440) - 16, 96, 32, 6);
                for (int k = 0; k < 4; k++)
                {
                    Rect(C(250, 245, 230), 14 + k * 69, 116, 67, 56);
                    Rect(glass, 16 + k * 69, 118, 63, 52);
                    Rect(C(95, 120, 160), 20 + k * 69, 122, 14, 44);
                }
                Rect(C(40, 50, 70), 300, 114, 52, 98);
                Rect(C(70, 85, 110), 304, 118, 20, 60);
                Rect(C(70, 85, 110), 328, 118, 20, 60);
                Rect(C(250, 200, 40), 325, 116, 2, 92);
                Rect(glass, 362, 118, 56, 52);
                Rect(glass, 428, 118, 70, 52);
                Rect(C(150, 20, 25), 0, 210, 512, 16);
                foreach (float wx in new[] { 95f, 423f }) FillE(g, C(25, 25, 25), X(wx) - 40, 226 - 40, 80, 80);
            });
        }

        static Tex DestTex() => destTex ?? (destTex = Paint(128, 28, false, g =>
        {
            FillR(g, C(15, 15, 15), 0, 0, 128, 28);
            Text(g, "11", "Arial", 18, FontStyle.Bold, C(255, 210, 60), 16, 14);
            Text(g, "LONDON", "Arial", 16, FontStyle.Bold, C(255, 210, 60), 76, 14);
        }));

        /// <summary>Glazed side of the telephone box: red frame with 3 × 8 panes and a solid bottom panel.</summary>
        static Tex PhoneSideTex() => phoneTex ?? (phoneTex = Paint(128, 284, false, g =>
        {
            var red = C(205, 25, 30);
            FillR(g, red, 0, 0, 128, 284);
            const float x0 = 14, y0 = 12, w = 100, h = 226;
            float pw = (w - 8) / 3, ph = (h - 28) / 8;
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 3; c++)
                {
                    float x = x0 + c * (pw + 4), y = y0 + r * (ph + 4);
                    using (var br = new LinearGradientBrush(new RectangleF(x - 1, y - 1, pw + 2, ph + 2), C(230, 238, 242), C(160, 185, 200), 45f)) g.FillRectangle(br, x, y, pw, ph);
                    Line(g, CA(160, 255, 255, 255), 1.5f, x + 3, y + ph - 4, x + pw * 0.5f, y + 3);
                }
            FillR(g, Shade(red, -18), 14, 246, 100, 30);
            using (var p = new Pen(Shade(red, 18), 2)) g.DrawRectangle(p, 18, 250, 92, 22);
        }));

        static Tex PhoneSignTex() => phoneSignTex ?? (phoneSignTex = Paint(256, 36, false, g =>
        {
            FillR(g, C(30, 30, 30), 0, 0, 256, 36);
            FillR(g, C(248, 248, 240), 4, 4, 248, 28);
            Text(g, "TELEPHONE", "Arial", 24, FontStyle.Bold, C(20, 20, 20), 128, 18);
        }));

        /// <summary>Sandstone tower shaft with rows of Gothic windows and string courses.</summary>
        static Tex BigBenTex() => bigBenTex ?? (bigBenTex = Paint(128, 324, false, g =>
        {
            using (var br = new LinearGradientBrush(new RectangleF(-1, 0, 130, 324), C(228, 204, 145), C(205, 180, 120), 0f)) g.FillRectangle(br, 0, 0, 128, 324);
            FillR(g, C(190, 165, 110), 0, 0, 10, 324);
            FillR(g, C(190, 165, 110), 118, 0, 10, 324);
            for (int row = 0; row < 4; row++)
            {
                FillR(g, C(185, 160, 105), 0, row * 81, 128, 5);
                for (int col = 0; col < 3; col++) GothicWindow(g, 22 + col * 31, 14 + row * 81, 21, 58);
            }
        }));

        /// <summary>Sundial plate: hour lines with Roman numerals over the top half (XII at the top), a little sun below.</summary>
        static Tex SundialTex() => sundialTex ?? (sundialTex = Paint(256, 256, false, g =>
        {
            FillR(g, C(175, 125, 65), 0, 0, 256, 256);
            FillE(g, C(240, 225, 185), 8, 8, 240, 240);
            using (var p = new Pen(C(120, 80, 40), 3)) g.DrawEllipse(p, 14, 14, 228, 228);
            string[] roman = { "XII", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI" };
            for (int h = 6; h <= 18; h++)
            {
                double a = (h - 12) * 15 * PI / 180;
                float sx = (float)Math.Sin(a), sy = (float)-Math.Cos(a);
                Line(g, C(120, 80, 40), 1.5f, 128 + sx * 30, 128 + sy * 30, 128 + sx * 88, 128 + sy * 88);
                Text(g, roman[h % 12], "Times New Roman", 15, FontStyle.Bold, C(90, 55, 25), 128 + sx * 103, 128 + sy * 103);
            }
            FillE(g, C(245, 195, 60), 104, 160, 48, 48);
            for (int k = 0; k < 12; k++)
            {
                double a = k * PI / 6;
                Line(g, C(245, 195, 60), 3, 128 + (float)Math.Cos(a) * 28, 184 + (float)Math.Sin(a) * 28, 128 + (float)Math.Cos(a) * 38, 184 + (float)Math.Sin(a) * 38);
            }
            FillE(g, C(120, 80, 40), 117, 177, 5, 5); FillE(g, C(120, 80, 40), 134, 177, 5, 5);
            using (var p = new Pen(C(120, 80, 40), 2)) g.DrawArc(p, 118, 182, 20, 12, 20, 140);
        }));

        /// <summary>Open sheet music: two pages of staves with notes.</summary>
        static Tex SheetMusicTex() => sheetTex ?? (sheetTex = Paint(256, 134, false, g =>
        {
            FillR(g, C(250, 248, 238), 0, 0, 256, 134);
            Line(g, C(190, 185, 170), 1.5f, 128, 0, 128, 134);
            var r = new Random(9);
            var ink = C(30, 30, 35);
            for (int pg = 0; pg < 2; pg++)
            {
                float x0 = 10 + pg * 128;
                for (int s = 0; s < 4; s++)
                {
                    float y = 18 + s * 29;
                    for (int k = 0; k < 5; k++) Line(g, C(90, 90, 95), 0.8f, x0, y + k * 4, x0 + 108, y + k * 4);
                    Line(g, ink, 1, x0, y, x0, y + 16);
                    for (int n = 0; n < 7; n++)
                    {
                        float nx = x0 + 14 + n * 13.5f, ny = y + r.Next(9) * 2 - 2;
                        FillE(g, ink, nx - 3, ny - 2, 6, 4.5f);
                        Line(g, ink, 1, nx + 2.6f, ny, nx + 2.6f, ny - 12);
                    }
                }
            }
        }));

        /// <summary>A child's colourful painting: sky, sun, rainbow, hills, a house, a tree and flowers.</summary>
        static Tex PaintingTex() => paintingTex ?? (paintingTex = Paint(256, 213, false, g =>
        {
            using (var br = new LinearGradientBrush(new RectangleF(0, -1, 256, 215), C(110, 185, 250), C(205, 232, 255), 90f)) g.FillRectangle(br, 0, 0, 256, 213);
            var rb = new[] { C(230, 50, 50), C(250, 140, 40), C(250, 220, 50), C(80, 190, 80), C(60, 120, 230), C(150, 80, 200) };
            for (int k = 0; k < rb.Length; k++)
                using (var p = new Pen(rb[k], 7)) g.DrawArc(p, 20 + k * 7, 50 + k * 7, 170 - k * 14, 170 - k * 14, 180, 180);
            FillE(g, C(255, 225, 60), 196, 14, 44, 44);
            for (int k = 0; k < 12; k++)
            {
                double a = k * PI / 6;
                Line(g, C(255, 200, 40), 3, 218 + (float)Math.Cos(a) * 27, 36 + (float)Math.Sin(a) * 27, 218 + (float)Math.Cos(a) * 36, 36 + (float)Math.Sin(a) * 36);
            }
            FillE(g, White, 30, 20, 40, 18); FillE(g, White, 50, 14, 40, 22); FillE(g, White, 120, 30, 46, 18);
            FillE(g, C(95, 185, 75), -40, 125, 210, 150);
            FillE(g, C(70, 160, 60), 100, 135, 230, 140);
            FillR(g, C(250, 230, 170), 40, 122, 62, 50);
            FillPoly(g, C(220, 60, 50), 32, 124, 71, 92, 110, 124);
            FillR(g, C(140, 90, 50), 62, 146, 16, 26);
            FillR(g, C(120, 190, 250), 84, 132, 14, 14);
            Line(g, White, 1.5f, 91, 132, 91, 146); Line(g, White, 1.5f, 84, 139, 98, 139);
            FillR(g, C(130, 85, 45), 185, 118, 10, 46);
            FillE(g, C(60, 150, 60), 160, 78, 60, 54);
            FillE(g, C(230, 60, 60), 172, 92, 8, 8); FillE(g, C(230, 60, 60), 196, 100, 8, 8);
            var fl = new[] { C(240, 70, 120), C(250, 210, 40), C(160, 90, 220), C(250, 250, 250), C(250, 120, 40) };
            for (int k = 0; k < 10; k++)
            {
                float x = 14 + k * 24, y = 186 + (k % 3) * 6;
                Line(g, C(40, 120, 40), 2, x, y, x, y + 16);
                FillE(g, fl[k % fl.Length], x - 6, y - 6, 12, 12);
                FillE(g, C(255, 230, 80), x - 2.5f, y - 2.5f, 5, 5);
            }
        }));

        /// <summary>Bass drum front head: a blue ring, a yellow star and "SP 2".</summary>
        static Tex DrumHeadTex() => drumTex ?? (drumTex = Paint(256, 256, false, g =>
        {
            FillR(g, C(245, 242, 235), 0, 0, 256, 256);
            FillE(g, C(30, 70, 180), 40, 40, 176, 176);
            FillE(g, C(245, 242, 235), 52, 52, 152, 152);
            using (var b = new SolidBrush(C(250, 200, 40))) g.FillPolygon(b, Art.StarPoints(128, 112, 52, 22));
            Text(g, "SP 2", "Arial Black", 30, FontStyle.Bold, C(200, 35, 50), 128, 172);
        }));

        /// <summary>Basketball backboard: orange border and the shooting square just above the rim.</summary>
        static Tex BackboardTex() => backboardTex ?? (backboardTex = Paint(256, 186, false, g =>
        {
            FillR(g, C(250, 250, 250), 0, 0, 256, 186);
            using (var p = new Pen(C(230, 90, 30), 8)) g.DrawRectangle(p, 6, 6, 244, 174);
            using (var p = new Pen(C(230, 90, 30), 5)) g.DrawRectangle(p, 77, 96, 102, 66);
        }));

        static Tex BasketTex() => basketTex ?? (basketTex = PerPixel(256, 128, (u, v) =>
        {
            double phi = 2 * PI * u, th = (0.5 - v) * PI;
            double x = Math.Cos(th) * Math.Cos(phi), y = Math.Sin(th), z = Math.Cos(th) * Math.Sin(phi);
            const double w = 0.03;
            if (Math.Abs(z) < w || Math.Abs(y) < w || Math.Abs(Math.Abs(x) - 0.72) < w * 0.9) return C(35, 25, 20);
            int h = (int)(u * 256) * 7 + (int)(v * 128) * 13;
            return (h ^ (h >> 3)) % 5 == 0 ? C(205, 95, 35) : C(228, 112, 40);
        }));

        static Tex VolleyTex() => volleyTex ?? (volleyTex = PerPixel(256, 128, (u, v) =>
        {
            double phi = 2 * PI * u, th = (0.5 - v) * PI;
            var d = new[] { Math.Cos(th) * Math.Cos(phi), Math.Sin(th), Math.Cos(th) * Math.Sin(phi) };
            int k = 0;
            for (int i = 1; i < 3; i++) if (Math.Abs(d[i]) > Math.Abs(d[k])) k = i;
            double s = d[(k + 1) % 3] / Math.Abs(d[k]), t = d[(k + 2) % 3] / Math.Abs(d[k]);
            double e = Math.Min(Math.Abs(s - 1 / 3.0), Math.Abs(s + 1 / 3.0));
            if (e < 0.035 || 1 - Math.Max(Math.Abs(s), Math.Abs(t)) < 0.02) return C(70, 70, 90);
            if (Math.Abs(s) < 1 / 3.0) return C(250, 250, 245);
            return k == 1 ? C(40, 90, 200) : C(250, 205, 40);
        }));

        static Tex JupiterTex() => jupiterTex ?? (jupiterTex = PerPixel(256, 128, (u, v) =>
        {
            double b = v + 0.015 * Math.Sin(u * 2 * PI * 4 + v * 30);
            var col = Lerp(C(238, 218, 182), C(185, 125, 85), 0.5 + 0.5 * Math.Sin(b * 40));
            double du = (u - 0.3) / 0.07, dv = (v - 0.64) / 0.045, q = du * du + dv * dv;
            return q < 1 ? Lerp(C(205, 90, 60), col, q) : col;
        }));

        /// <summary>Treasure-style map on parchment (wraps around the rolled scroll).</summary>
        static Tex MapTex() => mapTex ?? (mapTex = Paint(256, 128, false, g =>
        {
            FillR(g, C(238, 220, 170), 0, 0, 256, 128);
            var r = new Random(13);
            for (int k = 0; k < 30; k++) FillE(g, CA(40, 160, 120, 60), Rf(r) * 256, Rf(r) * 128, 6 + Rf(r) * 10, 4 + Rf(r) * 6);
            FillE(g, C(160, 200, 220), 14, 12, 120, 74);
            FillE(g, C(195, 210, 140), 46, 28, 54, 38);
            using (var p = new Pen(C(120, 85, 50), 1.5f)) g.DrawEllipse(p, 46, 28, 54, 38);
            FillE(g, C(160, 200, 220), 150, 66, 96, 54);
            for (int k = 0; k < 4; k++) FillPoly(g, C(140, 100, 60), 160 + k * 18, 40, 168 + k * 18, 22, 176 + k * 18, 40);
            using (var p = new Pen(C(200, 40, 40), 2) { DashStyle = DashStyle.Dash })
                g.DrawCurve(p, new[] { new PointF(70, 50), new PointF(120, 95), new PointF(170, 52), new PointF(210, 35) });
            Line(g, C(200, 40, 40), 3, 204, 29, 216, 41); Line(g, C(200, 40, 40), 3, 216, 29, 204, 41);
            Line(g, C(90, 60, 40), 2, 236, 112, 236, 92);
            FillPoly(g, C(90, 60, 40), 231, 96, 236, 86, 241, 96);
        }));
    }
}
