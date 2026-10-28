using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace LazyTurtle
{
    /// <summary>One draw call of a model: geometry sharing one texture (null = vertex colour only).</summary>
    sealed class ModelPart
    {
        public MeshBuilder Mesh;
        public Tex Tex;
        public bool AlphaTest;
    }

    /// <summary>
    /// Procedural low-poly 3D models (characters, furniture, garden and playground objects) and the
    /// textures they use, all painted in code. Every model faces +X, y is up, 1 unit = 1 map cell and
    /// the origin is the centre of the model's footprint on the floor unless noted otherwise.
    /// </summary>
    static partial class Models
    {
        const double PI = Math.PI;

        static readonly Color Skin = C(125, 190, 95), SkinDark = C(75, 140, 60);
        static readonly Color OldSkin = C(130, 180, 105), OldSkinDark = C(85, 135, 70);
        static readonly Color Cream = C(240, 225, 170), RimGold = C(235, 210, 120);
        static readonly Color Gold = C(255, 205, 60), GoldDark = C(205, 150, 30);
        static readonly Color White = C(250, 250, 250), Pupil = C(35, 28, 30), Pink = C(255, 150, 165);
        static readonly Color Metal = C(125, 128, 138), Wood = C(150, 100, 55);

        /// <summary>Returns the parts of a model, or null for an unknown key. Deterministic (may be rebuilt after a language change).</summary>
        public static List<ModelPart> Build(string key, Art art)
        {
            if (key == null) return null;
            var m = new Model();
            int n;
            switch (key)
            {
                case "player": Player(m); break;
                case "leg": Leg(m); break;
                case "master": Master(m); break;
                case "tdesk": TeacherDesk(m); break;
                case "gift": Gift(m); break;
                case "bench": Bench(m); break;
                case "plant": Plant(m); break;
                case "globe": Globe(m); break;
                case "coin": Coin(m); break;
                case "fountain": Fountain(m); break;
                case "peacock": Peacock(m); break;
                case "peacockfan": PeacockFan(m); break;
                case "swan": Swan(m); break;
                case "goal": Goal(m); break;
                case "ball": m.Get(BallTex()).Ellipsoid(0, 0, 0, 0.13, 0.13, 0.13, 20, 12); break;
                case "pitch": Pitch(m); break;
                case "backdrop": Backdrop(m); break;
                case "sign": Sign(m); break;
                default:
                    if (Indexed(key, "teacher", 10, out n)) Teacher(m, n);
                    else if (Indexed(key, "kid", 3, out n)) Kid(m, n);
                    else if (Indexed(key, "desk", 4, out n)) Desk(m, n);
                    else if (Indexed(key, "tree", 3, out n)) Tree(m, n);
                    else if (!BuildArtifact(m, key)) return null;
                    break;
            }
            return m.Result();
        }

        static bool Indexed(string key, string prefix, int count, out int n)
        {
            n = -1;
            return key.Length == prefix.Length + 1 && key.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(key.Substring(prefix.Length), out n) && n >= 0 && n < count;
        }

        /// <summary>Collects geometry per texture; Push/Pop apply to every part (also to parts created later).</summary>
        sealed class Model
        {
            readonly List<ModelPart> parts = new List<ModelPart>();
            readonly List<float[]> stack = new List<float[]>();

            public MeshBuilder Get(Tex tex, bool alphaTest = false)
            {
                foreach (var p in parts)
                    if (p.Tex == tex && p.AlphaTest == alphaTest) return p.Mesh;
                var b = new MeshBuilder();
                foreach (var t in stack) b.Push(t);
                parts.Add(new ModelPart { Mesh = b, Tex = tex, AlphaTest = alphaTest });
                return b;
            }

            /// <summary>The untextured (vertex colour only) part.</summary>
            public MeshBuilder Plain => Get(null);

            public void Push(float[] t)
            {
                stack.Add(t);
                foreach (var p in parts) p.Mesh.Push(t);
            }

            public void Pop()
            {
                stack.RemoveAt(stack.Count - 1);
                foreach (var p in parts) p.Mesh.Pop();
            }

            public List<ModelPart> Result() => parts.FindAll(p => p.Mesh.Vertices > 0);
        }

        // ================================================================== geometry helpers

        static Color C(int r, int g, int b) => Art.C(r, g, b);
        static Color Shade(Color c, int d) => Art.Shade(c, d);
        static V3 P(double x, double y, double z) => new V3(x, y, z);
        static float[] T(double x, double y, double z) => M4.Translate((float)x, (float)y, (float)z);
        static float[] T(V3 p) => M4.Translate(p.X, p.Y, p.Z);
        static float[] S(double x, double y, double z) => M4.Scale((float)x, (float)y, (float)z);
        static float[] Mul(float[] a, float[] b) => M4.Mul(a, b);
        static float[] Mul(float[] a, float[] b, float[] c) => M4.Mul(M4.Mul(a, b), c);

        /// <summary>Unit direction from a head centre: az turns towards +z, el up.</summary>
        static V3 Dir(double az, double el) => new V3(Math.Cos(el) * Math.Cos(az), Math.Sin(el), Math.Cos(el) * Math.Sin(az));
        static V3 On(V3 c, double az, double el, double d) => c + Dir(az, el) * d;

        /// <summary>Rotation taking +y onto dir.</summary>
        static float[] AlongY(V3 dir)
        {
            dir = dir.Normalized();
            var axis = V3.Cross(new V3(0, 1, 0), dir);
            double angle = Math.Acos(Math.Max(-1, Math.Min(1, dir.Y)));
            if (axis.Length > 1e-5f) return MeshBuilder.AxisAngle(axis.Normalized(), angle);
            return dir.Y < 0 ? M4.RotX(PI) : M4.Identity();
        }

        static void Sph(MeshBuilder b, V3 p, double r, int slices = 8, int stacks = 4) => b.Ellipsoid(p.X, p.Y, p.Z, r, r, r, slices, stacks);

        /// <summary>Round ellipsoid stretched between two points (arms, legs, necks).</summary>
        static void Limb(MeshBuilder b, V3 from, V3 to, double r, int slices = 8, int stacks = 5)
        {
            var d = to - from;
            b.Push(Mul(T((from + to) * 0.5), AlongY(d)));
            b.Ellipsoid(0, 0, 0, r, d.Length / 2 + r * 0.3, r, slices, stacks);
            b.Pop();
        }

        /// <summary>Truncated cone between two points.</summary>
        static void Cone(MeshBuilder b, V3 from, V3 to, double r0, double r1, int slices = 8, bool caps = true, float uRepeat = 1)
        {
            var d = to - from;
            b.Push(Mul(T(from), AlongY(d)));
            b.Cylinder(0, 0, 0, r0, r1, d.Length, slices, caps, uRepeat);
            b.Pop();
        }

        /// <summary>A bent tube through the points (smiles, straps, chains, water jets).</summary>
        static void Tube(MeshBuilder b, V3[] pts, double r, int slices = 5)
        {
            for (int i = 0; i + 1 < pts.Length; i++) Cone(b, pts[i], pts[i + 1], r, r, slices, false);
            for (int i = 1; i + 1 < pts.Length; i++) Sph(b, pts[i], r, slices, 2);
        }

        static void Tri(MeshBuilder b, V3 p0, V3 p1, V3 p2, float u0 = 0, float v0 = 0, float u1 = 0, float v1 = 0, float u2 = 0, float v2 = 0)
        {
            var n = V3.Cross(p1 - p0, p2 - p0).Normalized();
            b.Vertex(p0, n, u0, v0); b.Vertex(p1, n, u1, v1); b.Vertex(p2, n, u2, v2);
        }

        /// <summary>Quad with explicit uv per corner (counter-clockwise seen from the front).</summary>
        static void QuadUV(MeshBuilder b, V3 p0, V3 p1, V3 p2, V3 p3, float[] uv)
        {
            Tri(b, p0, p1, p2, uv[0], uv[1], uv[2], uv[3], uv[4], uv[5]);
            Tri(b, p0, p2, p3, uv[0], uv[1], uv[4], uv[5], uv[6], uv[7]);
        }

        /// <summary>Flat horizontal ring (or disc when rIn = 0) facing up.</summary>
        static void Annulus(MeshBuilder b, double cx, double y, double cz, double rIn, double rOut, int slices, float uRepeat = 1)
        {
            var up = new V3(0, 1, 0);
            for (int j = 0; j < slices; j++)
            {
                double a0 = 2 * PI * j / slices, a1 = 2 * PI * (j + 1) / slices;
                var i0 = P(cx + Math.Cos(a0) * rIn, y, cz + Math.Sin(a0) * rIn);
                var i1 = P(cx + Math.Cos(a1) * rIn, y, cz + Math.Sin(a1) * rIn);
                var o0 = P(cx + Math.Cos(a0) * rOut, y, cz + Math.Sin(a0) * rOut);
                var o1 = P(cx + Math.Cos(a1) * rOut, y, cz + Math.Sin(a1) * rOut);
                float u0 = (float)j / slices * uRepeat, u1 = (float)(j + 1) / slices * uRepeat;
                b.Vertex(i0, up, u0, 0); b.Vertex(o1, up, u1, 1); b.Vertex(o0, up, u0, 1);
                b.Vertex(i0, up, u0, 0); b.Vertex(i1, up, u1, 0); b.Vertex(o1, up, u1, 1);
            }
        }

        /// <summary>Inside wall of a vertical cylinder (normals pointing to the axis).</summary>
        static void InnerWall(MeshBuilder b, double cx, double cz, double r, double y0, double y1, int slices, float uRepeat = 1)
        {
            for (int j = 0; j < slices; j++)
            {
                double p0 = 2 * PI * j / slices, p1 = 2 * PI * (j + 1) / slices;
                var a = P(cx + Math.Cos(p0) * r, y0, cz + Math.Sin(p0) * r);
                var bb = P(cx + Math.Cos(p1) * r, y0, cz + Math.Sin(p1) * r);
                var c = P(cx + Math.Cos(p1) * r, y1, cz + Math.Sin(p1) * r);
                var d = P(cx + Math.Cos(p0) * r, y1, cz + Math.Sin(p0) * r);
                var n0 = P(-Math.Cos(p0), 0, -Math.Sin(p0));
                var n1 = P(-Math.Cos(p1), 0, -Math.Sin(p1));
                float u0 = (float)j / slices * uRepeat, u1 = (float)(j + 1) / slices * uRepeat;
                b.Vertex(a, n0, u0, 1); b.Vertex(bb, n1, u1, 1); b.Vertex(c, n1, u1, 0);
                b.Vertex(a, n0, u0, 1); b.Vertex(c, n1, u1, 0); b.Vertex(d, n0, u0, 0);
            }
        }

        /// <summary>Rounded box (superellipsoid): flat faces with soft edges, e.g. backpacks.</summary>
        static void SoftBox(MeshBuilder b, double cx, double cy, double cz, double rx, double ry, double rz, double e = 0.35, int slices = 16, int stacks = 10)
        {
            Func<double, double, V3[]> pt = (t, p) =>
            {
                double ct = Math.Cos(t), st = Math.Sin(t), cp = Math.Cos(p), sp = Math.Sin(p);
                double x = rx * Pw(ct * cp, e), y = ry * Pw(st, e), z = rz * Pw(ct * sp, e);
                double k = 2 / e - 1;
                var n = new V3(Pw(x / rx, k) / rx, Pw(y / ry, k) / ry, Pw(z / rz, k) / rz).Normalized();
                return new[] { P(cx + x, cy + y, cz + z), n };
            };
            for (int i = 0; i < stacks; i++)
            {
                double t0 = -PI / 2 + PI * i / stacks, t1 = -PI / 2 + PI * (i + 1) / stacks;
                for (int j = 0; j < slices; j++)
                {
                    double p0 = 2 * PI * j / slices, p1 = 2 * PI * (j + 1) / slices;
                    var q0 = pt(t0, p0); var q1 = pt(t0, p1); var q2 = pt(t1, p1); var q3 = pt(t1, p0);
                    float u0 = (float)j / slices, u1 = (float)(j + 1) / slices;
                    float v0 = (float)(0.5 - t0 / PI), v1 = (float)(0.5 - t1 / PI);
                    b.Vertex(q0[0], q0[1], u0, v0); b.Vertex(q2[0], q2[1], u1, v1); b.Vertex(q1[0], q1[1], u1, v0);
                    b.Vertex(q0[0], q0[1], u0, v0); b.Vertex(q3[0], q3[1], u0, v1); b.Vertex(q2[0], q2[1], u1, v1);
                }
            }
        }

        static double Pw(double v, double e) => Math.Sign(v) * Math.Pow(Math.Abs(v), e);

        /// <summary>Five-pointed star prism in the y-z plane between x0 and x1 (faces ±x).</summary>
        static void StarX(MeshBuilder b, double cy, double cz, double outer, double inner, double x0, double x1)
        {
            var front = new V3[10];
            var back = new V3[10];
            for (int i = 0; i < 10; i++)
            {
                double a = i * PI / 5, r = i % 2 == 0 ? outer : inner;
                front[i] = P(x1, cy + Math.Cos(a) * r, cz + Math.Sin(a) * r);
                back[i] = P(x0, cy + Math.Cos(a) * r, cz + Math.Sin(a) * r);
            }
            V3 cf = P(x1, cy, cz), cb = P(x0, cy, cz);
            for (int i = 0; i < 10; i++)
            {
                int j = (i + 1) % 10;
                Tri(b, cf, front[i], front[j]);
                Tri(b, cb, back[j], back[i]);
                b.Quad(back[i], back[j], front[j], front[i]);
            }
        }

        // ================================================================== face helpers

        static void Eye(MeshBuilder b, V3 p, double r, V3 n)
        {
            b.Col(White); Sph(b, p, r, 8, 4);
            var f = (n + new V3(1.2, 0, 0)).Normalized();
            var pc = p + f * (r * 0.6);
            b.Col(Pupil); Sph(b, pc, r * 0.52, 6, 4);
            var hl = pc + (f + new V3(0, 0.8, 0)).Normalized() * (r * 0.44);
            b.Col(White); Sph(b, hl, r * 0.17, 4, 2);
        }

        static void Eyes(MeshBuilder b, V3 head, double R, double az, double el, double r)
        {
            for (int s = -1; s <= 1; s += 2)
                Eye(b, On(head, s * az, el, R - r * 0.45), r, Dir(s * az, el));
        }

        /// <summary>Round glasses: two rims, a bridge and temples going back to the sides of the head.</summary>
        static void Glasses(MeshBuilder b, V3 head, double R, double az, double el, double rim, double tube, double eyeR, Color col)
        {
            b.Col(col);
            var inner = new V3[2];
            var outer = new V3[2];
            for (int k = 0; k < 2; k++)
            {
                int s = k == 0 ? -1 : 1;
                double a = s * az;
                var c = On(head, a, el, R + eyeR * 0.85);
                b.Push(Mul(T(c), M4.RotY(-a)));
                b.Torus(0, 0, 0, rim, tube, 10, 4);
                b.Pop();
                var t = P(-Math.Sin(a), 0, Math.Cos(a)) * rim;
                inner[k] = s > 0 ? c - t : c + t;
                outer[k] = s > 0 ? c + t : c - t;
                Cone(b, outer[k], On(head, s * 1.45, el + 0.05, R * 0.97), tube * 0.8, tube * 0.8, 4, false);
            }
            Cone(b, inner[0], inner[1], tube * 0.9, tube * 0.9, 4, false);
        }

        /// <summary>White bushy eyebrows: three fluffy puffs per side.</summary>
        static void Brows(MeshBuilder b, V3 head, double R, double el)
        {
            double sc = R / 0.19;
            b.Col(White);
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 3; k++)
                {
                    var p = On(head, s * (0.2 + 0.17 * k), el + (k == 1 ? 0.04 : 0), R + 0.01 * sc);
                    b.Ellipsoid(p.X, p.Y, p.Z, 0.03 * sc, 0.024 * sc, 0.036 * sc, 5, 3);
                }
        }

        static void Smile(MeshBuilder b, V3 head, double R, double el, double w, double tube, Color col)
        {
            b.Col(col);
            var pts = new V3[5];
            for (int k = 0; k < 5; k++)
            {
                double az = -w + 2 * w * k / 4;
                pts[k] = On(head, az, el - 0.08 * (1 - az * az / (w * w)), R + 0.002);
            }
            Tube(b, pts, tube, 5);
        }

        static void Cheeks(MeshBuilder b, V3 head, double R, double az, double el, double cr)
        {
            b.Col(Pink);
            for (int s = -1; s <= 1; s += 2) Sph(b, On(head, s * az, el, R - cr * 0.55), cr, 6, 3);
        }

        /// <summary>Hex-plated shell on the back of an upright turtle, with a golden rim.</summary>
        static void BackShell(Model m, V3 c, double vert, double depth, double width, Color tint)
        {
            var b = m.Get(ShellTex());
            b.Col(tint);
            b.Push(Mul(T(c), M4.RotZ(PI / 2)));   // pole (the central plate) points to -x
            b.Ellipsoid(0, 0, 0, vert, depth, width, 12, 8);
            b.Pop();
            var p = m.Plain;
            p.Col(RimGold);
            p.Push(Mul(T(c), S(0.3, vert, width)));
            p.Torus(0, 0, 0, 1, 0.075, 12, 4);
            p.Pop();
        }

        // ================================================================== turtles

        /// <summary>The player's turtle (legs are drawn separately): shell, head with a red cap and a yellow backpack.</summary>
        static void Player(Model m)
        {
            m.Get(ShellTex()).Ellipsoid(0, 0.24, 0, 0.30, 0.20, 0.26, 24, 10, -0.3, 1);
            var c = m.Plain;
            c.Col(RimGold); c.Ellipsoid(0, 0.18, 0, 0.305, 0.035, 0.265, 24, 3);
            c.Col(Cream); c.Ellipsoid(0, 0.165, 0, 0.29, 0.065, 0.25, 16, 5);
            // tail, neck, head
            c.Col(Skin);
            c.Ellipsoid(-0.32, 0.17, 0, 0.06, 0.03, 0.035, 8, 4);
            c.Ellipsoid(0.27, 0.22, 0, 0.09, 0.065, 0.07, 8, 4);
            var head = P(0.36, 0.27, 0);
            c.Ellipsoid(head.X, head.Y, head.Z, 0.12, 0.115, 0.12, 16, 9);
            Eyes(c, head, 0.12, 0.42, -0.06, 0.036);
            Cheeks(c, head, 0.12, 0.85, -0.2, 0.026);
            Smile(c, head, 0.12, -0.38, 0.3, 0.007, C(60, 40, 30));
            // red baseball cap
            c.Col(C(225, 50, 45)); c.Ellipsoid(0.355, 0.30, 0, 0.125, 0.10, 0.125, 16, 5, 0, 1);
            c.Col(C(190, 35, 35)); c.Ellipsoid(0.465, 0.305, 0, 0.09, 0.012, 0.10, 12, 3);
            c.Col(White);
            c.Sphere(0.355, 0.40, 0, 0.018, 6, 3);
            c.Ellipsoid(0.462, 0.345, 0, 0.012, 0.025, 0.04, 6, 3);
            // backpack lying on the back of the shell
            m.Push(Mul(T(-0.17, 0.43, 0), M4.RotZ(0.5)));
            c.Col(C(250, 200, 50)); SoftBox(c, 0, 0, 0, 0.10, 0.045, 0.11, 0.35, 12, 6);
            c.Col(C(240, 160, 30)); SoftBox(c, -0.01, 0.042, 0, 0.065, 0.02, 0.075, 0.35, 8, 4);
            c.Col(C(230, 55, 55)); c.Sphere(0, 0.064, 0, 0.013, 6, 3);
            m.Pop();
            // straps over the shell to the shoulders
            c.Col(C(200, 130, 20));
            for (int s = -1; s <= 1; s += 2)
                Tube(c, new[] { P(-0.085, 0.49, s * 0.07), P(0.08, 0.437, s * 0.075), P(0.22, 0.372, s * 0.085), P(0.25, 0.30, s * 0.10) }, 0.012, 5);
        }

        static void Leg(Model m)
        {
            var c = m.Plain;
            c.Col(Skin); c.Ellipsoid(0, 0, 0, 0.085, 0.06, 0.065, 12, 6);
            c.Col(Cream);
            for (int k = -1; k <= 1; k++) c.Sphere(0.07, -0.025, k * 0.03, 0.016, 6, 3);
        }

        /// <summary>The wise Master Turtle ≈1.35 tall: star robe, long beard, mortarboard, star staff, medallion.</summary>
        static void Master(Model m)
        {
            var skin = C(140, 175, 110);
            var robe = m.Get(RobeTex());
            robe.Cylinder(0, 0, 0, 0.34, 0.17, 0.88, 24, true, 2);
            robe.Ellipsoid(0, 0.86, 0, 0.19, 0.11, 0.22, 14, 5, -0.2, 1);
            var c = m.Plain;
            c.Col(Gold);
            c.Push(Mul(T(0, 0.03, 0), M4.RotZ(PI / 2)));
            c.Torus(0, 0, 0, 0.335, 0.022, 20, 4);
            c.Pop();
            BackShell(m, P(-0.17, 0.62, 0), 0.40, 0.15, 0.32, C(190, 185, 150));
            // sleeves and hands; the right hand holds the staff
            var sleeve = C(120, 60, 180);
            c.Col(sleeve);
            Limb(c, P(0, 0.88, -0.2), P(0.10, 0.62, -0.27), 0.075, 8, 4);
            Limb(c, P(0, 0.88, 0.2), P(0.12, 0.66, 0.28), 0.075, 8, 4);
            c.Col(skin);
            c.Sphere(0.12, 0.58, -0.27, 0.055, 8, 4);
            c.Sphere(0.16, 0.63, 0.30, 0.055, 8, 4);
            c.Col(C(130, 85, 40)); c.Rod(P(0.16, 0, 0.30), P(0.16, 1.42, 0.30), 0.022, 8);
            c.Col(Gold); StarX(c, 1.52, 0.30, 0.11, 0.045, 0.14, 0.18);
            // head
            var head = P(0.05, 1.10, 0);
            c.Col(skin); c.Ellipsoid(head.X, head.Y, head.Z, 0.19, 0.18, 0.19, 14, 8);
            Eyes(c, head, 0.19, 0.38, 0.12, 0.045);
            Glasses(c, head, 0.19, 0.38, 0.12, 0.068, 0.01, 0.045, C(225, 180, 50));
            Brows(c, head, 0.19, 0.58);
            Cheeks(c, head, 0.19, 0.8, -0.12, 0.038);
            // long white beard hanging in front of the robe, moustache
            var beard = new[] { new[] { 0.17, 0.99, 0.10, 0.085, 0.14 }, new[] { 0.21, 0.87, 0.085, 0.085, 0.11 }, new[] { 0.24, 0.76, 0.07, 0.08, 0.08 }, new[] { 0.26, 0.665, 0.045, 0.06, 0.05 } };
            for (int k = 0; k < beard.Length; k++)
            {
                var q = beard[k];
                c.Col(k % 2 == 0 ? C(248, 248, 252) : C(232, 232, 240));
                c.Ellipsoid(q[0], q[1], 0, q[2], q[3], q[4], k < 2 ? 10 : 8, k < 2 ? 5 : 4);
            }
            c.Col(White);
            for (int s = -1; s <= 1; s += 2)
            {
                c.Push(Mul(T(On(head, s * 0.2, -0.22, 0.19)), M4.RotX(s * 0.3)));
                c.Ellipsoid(0, 0, 0, 0.035, 0.028, 0.07, 6, 4);
                c.Pop();
            }
            // graduation cap with a golden tassel
            c.Col(C(35, 30, 45));
            c.Cylinder(0.04, 1.20, 0, 0.165, 0.16, 1.31, 16);
            c.Push(Mul(T(0.04, 1.32, 0), M4.RotY(PI / 4)));
            c.Box(0, 0, 0, 0.42, 0.022, 0.42);
            c.Pop();
            c.Col(Gold);
            c.Sphere(0.04, 1.335, 0, 0.02, 6, 3);
            Tube(c, new[] { P(0.04, 1.335, 0), P(0.04, 1.335, -0.28), P(0.04, 1.20, -0.29) }, 0.007, 5);
            c.Ellipsoid(0.04, 1.17, -0.29, 0.025, 0.05, 0.025, 6, 3);
            // golden chain and medallion on the robe
            Func<double, double> robeR = y => 0.34 - 0.17 * y / 0.88;
            for (int s = -1; s <= 1; s += 2)
            {
                var pts = new V3[4];
                for (int k = 0; k < 4; k++)
                {
                    double y = 0.84 - 0.11 * k, a = s * (0.6 - 0.18 * k), r = robeR(y) + 0.01;
                    pts[k] = P(Math.Cos(a) * r, y, Math.Sin(a) * r);
                }
                Tube(c, pts, 0.007, 4);
            }
            double mr = robeR(0.45);
            c.Push(Mul(T(mr + 0.005, 0.45, 0), M4.RotZ(-PI / 2)));
            c.Cylinder(0, 0, 0, 0.06, 0.06, 0.018, 16);
            c.Pop();
            c.Col(C(200, 140, 20)); StarX(c, 0.45, 0, 0.04, 0.017, mr + 0.02, mr + 0.03);
        }

        static readonly Color[] KidShirts = { C(240, 90, 90), C(80, 150, 240), C(250, 180, 50) };
        static readonly Color[] KidPacks = { C(250, 200, 50), C(240, 110, 60), C(90, 170, 230) };

        /// <summary>Small classmate turtle ≈0.6 tall with a shirt and a backpack.</summary>
        static void Kid(Model m, int v)
        {
            var c = m.Plain;
            for (int s = -1; s <= 1; s += 2)
            {
                c.Col(SkinDark); c.Ellipsoid(0.04, 0.03, s * 0.065, 0.07, 0.03, 0.05, 8, 3);
                c.Col(Skin); c.Ellipsoid(0, 0.08, s * 0.065, 0.045, 0.06, 0.045, 6, 3);
            }
            BackShell(m, P(-0.09, 0.26, 0), 0.19, 0.08, 0.17, C(255, 255, 255));
            c.Col(KidPacks[v]); SoftBox(c, -0.20, 0.27, 0, 0.045, 0.09, 0.10, 0.35, 10, 6);
            c.Col(KidShirts[v]); c.Ellipsoid(0, 0.25, 0, 0.12, 0.16, 0.14, 12, 7);
            // backpack straps following the shirt
            c.Col(C(120, 70, 20));
            for (int s = -1; s <= 1; s += 2)
            {
                var pts = new List<V3> { P(-0.08, 0.385, s * 0.065), P(0, 0.40, s * 0.065) };
                foreach (double y in new[] { 0.37, 0.32, 0.26, 0.20, 0.15 })
                {
                    double q = 1 - Math.Pow((y - 0.25) / 0.16, 2) - Math.Pow(0.065 / 0.14, 2);
                    pts.Add(P(0.12 * Math.Sqrt(Math.Max(0, q)) + 0.008, y, s * 0.065));
                }
                Tube(c, pts.ToArray(), 0.012, 5);
            }
            c.Col(Skin);
            for (int s = -1; s <= 1; s += 2) Limb(c, P(0, 0.35, s * 0.13), P(0.04, 0.18, s * 0.16), 0.04, 6, 4);
            var head = P(0.03, 0.46, 0);
            c.Ellipsoid(head.X, head.Y, head.Z, 0.145, 0.135, 0.145, 14, 8);
            Eyes(c, head, 0.145, 0.36, 0.1, 0.045);
            Cheeks(c, head, 0.145, 0.85, -0.18, 0.03);
            Smile(c, head, 0.145, -0.3, 0.35, 0.009, C(60, 40, 30));
        }

        // ================================================================== furniture

        /// <summary>Pupil's desk (towards +x) and chair; v 1..3 = a turtle, a bunny or a frog sitting on it.</summary>
        static void Desk(Model m, int v)
        {
            var w = m.Get(WoodTex());
            var c = m.Plain;
            w.Col(White); w.Box(0.18, 0.58, 0, 0.42, 0.035, 0.8);
            w.Col(C(200, 185, 170));
            w.Box(0.20, 0.47, 0, 0.36, 0.02, 0.74);
            w.Box(0.37, 0.47, 0, 0.02, 0.18, 0.76);
            c.Col(Metal);
            for (int s = -1; s <= 1; s += 2)
            {
                c.Rod(P(0, 0, s * 0.37), P(0, 0.565, s * 0.37), 0.016, 6);
                c.Rod(P(0.36, 0, s * 0.37), P(0.36, 0.565, s * 0.37), 0.016, 6);
                c.Rod(P(-0.02, 0.02, s * 0.37), P(0.38, 0.02, s * 0.37), 0.014, 6);
            }
            // a book and a pencil
            c.Col(C(70, 120, 220)); c.Box(0.15, 0.609, -0.18, 0.16, 0.022, 0.12);
            c.Col(White); c.Box(0.153, 0.609, -0.18, 0.15, 0.016, 0.125);
            c.Col(C(250, 210, 60)); c.Rod(P(0.10, 0.606, 0.10), P(0.25, 0.606, 0.17), 0.008, 6);
            c.Col(C(240, 200, 150)); Cone(c, P(0.25, 0.606, 0.17), P(0.27, 0.606, 0.179), 0.008, 0.002, 6);
            c.Col(C(240, 130, 150)); c.Rod(P(0.09, 0.606, 0.0953), P(0.10, 0.606, 0.10), 0.0085, 6);
            // chair
            w.Col(C(235, 190, 150)); w.Box(-0.25, 0.34, 0, 0.32, 0.03, 0.36);
            w.Push(Mul(T(-0.425, 0.60, 0), M4.RotZ(0.1)));
            w.Box(0, 0, 0, 0.03, 0.15, 0.36);
            w.Pop();
            c.Col(Metal);
            for (int s = -1; s <= 1; s += 2)
            {
                c.Rod(P(-0.38, 0, s * 0.15), P(-0.38, 0.325, s * 0.15), 0.014, 6);
                c.Rod(P(-0.12, 0, s * 0.15), P(-0.12, 0.325, s * 0.15), 0.014, 6);
                c.Rod(P(-0.39, 0.33, s * 0.15), P(-0.43, 0.70, s * 0.15), 0.014, 6);
            }
            if (v == 0) return;

            // the seated classmate
            var head = P(-0.21, 0.72, 0);
            if (v == 1)
            {
                BackShell(m, P(-0.31, 0.50, 0), 0.15, 0.06, 0.13, C(255, 255, 255));
                Seated(c, C(240, 120, 170), Skin, SkinDark);
                c.Col(Skin); c.Ellipsoid(head.X, head.Y, head.Z, 0.115, 0.11, 0.115, 14, 8);
                Eyes(c, head, 0.115, 0.38, 0.1, 0.036);
                Cheeks(c, head, 0.115, 0.85, -0.18, 0.025);
                Smile(c, head, 0.115, -0.3, 0.32, 0.007, C(60, 40, 30));
            }
            else if (v == 2)
            {
                var fur = C(232, 228, 228);
                Seated(c, C(80, 160, 220), fur, C(215, 210, 210));
                c.Col(fur); c.Ellipsoid(head.X, head.Y, head.Z, 0.115, 0.105, 0.11, 14, 8);
                c.Sphere(-0.35, 0.40, 0, 0.04, 8, 4);   // fluffy tail
                for (int s = -1; s <= 1; s += 2)
                {
                    c.Col(fur); Limb(c, P(-0.22, 0.81, s * 0.05), P(-0.25, 1.04, s * 0.075), 0.034, 8, 5);
                    c.Col(C(255, 185, 200)); Limb(c, P(-0.198, 0.85, s * 0.05), P(-0.226, 1.02, s * 0.074), 0.017, 6, 4);
                }
                Eyes(c, head, 0.112, 0.4, 0.12, 0.032);
                c.Col(White);
                for (int s = -1; s <= 1; s += 2) Sph(c, On(head, s * 0.13, -0.28, 0.1), 0.035, 8, 4);
                c.Box(-0.093, 0.665, 0, 0.01, 0.028, 0.03);
                c.Col(C(250, 140, 160)); Sph(c, On(head, 0, -0.1, 0.112), 0.018, 6, 3);
                c.Col(C(120, 110, 110));
                for (int s = -1; s <= 1; s += 2)
                    for (int k = 0; k < 2; k++)
                        c.Rod(On(head, s * 0.2, -0.25, 0.115), On(head, s * 0.2, -0.25, 0.115) + P(-0.02, 0.015 - 0.03 * k, s * 0.08), 0.002, 3);
                Cheeks(c, head, 0.112, 0.85, -0.15, 0.022);
            }
            else
            {
                var green = C(90, 180, 80);
                Seated(c, C(240, 120, 60), green, C(70, 150, 60));
                c.Col(green); c.Ellipsoid(-0.20, 0.70, 0, 0.11, 0.085, 0.14, 14, 8);
                for (int s = -1; s <= 1; s += 2)
                {
                    c.Col(green); c.Sphere(-0.19, 0.78, s * 0.075, 0.05, 10, 6);
                    Eye(c, P(-0.16, 0.79, s * 0.075), 0.036, P(0.8, 0.2, s * 0.3).Normalized());
                }
                var face = P(-0.20, 0.70, 0);
                c.Col(C(50, 90, 40));
                var pts = new V3[5];
                for (int k = 0; k < 5; k++)
                {
                    double z = -0.08 + 0.04 * k, q = 1 - Math.Pow(z / 0.14, 2) - 0.2;
                    pts[k] = P(face.X + 0.11 * Math.Sqrt(Math.Max(0, q)) + 0.003, 0.66 - 0.012 * (1 - z * z / 0.0064), z);
                }
                Tube(c, pts, 0.007, 5);
                c.Col(Pink);
                for (int s = -1; s <= 1; s += 2) c.Sphere(-0.13, 0.68, s * 0.085, 0.022, 6, 3);
            }
        }

        /// <summary>Body, legs and arms of a classmate sitting on the chair (hands resting on the desk).</summary>
        static void Seated(MeshBuilder c, Color shirt, Color skin, Color skinDark)
        {
            c.Col(shirt); c.Ellipsoid(-0.24, 0.48, 0, 0.10, 0.13, 0.115, 12, 7);
            for (int s = -1; s <= 1; s += 2)
            {
                c.Col(skin);
                Limb(c, P(-0.22, 0.39, s * 0.055), P(-0.07, 0.40, s * 0.06), 0.045, 6, 4);
                Limb(c, P(-0.07, 0.39, s * 0.06), P(-0.05, 0.21, s * 0.06), 0.035, 6, 4);
                c.Col(skinDark); c.Ellipsoid(-0.02, 0.19, s * 0.06, 0.05, 0.025, 0.035, 6, 3);
                c.Col(shirt); Limb(c, P(-0.24, 0.56, s * 0.11), P(-0.01, 0.615, s * 0.10), 0.035, 6, 4);
                c.Col(skin); c.Sphere(0.01, 0.615, s * 0.095, 0.035, 6, 4);
            }
        }

        /// <summary>Teacher's desk; the teacher stands at -x (drawers on that side) facing the class at +x.</summary>
        static void TeacherDesk(Model m)
        {
            var w = m.Get(WoodTex());
            var c = m.Plain;
            w.Col(C(190, 150, 120)); w.Box(0, 0.30, 0, 0.46, 0.52, 1.40);
            w.Col(White); w.Box(0, 0.58, 0, 0.56, 0.04, 1.54);
            c.Col(C(90, 60, 40)); c.Box(0, 0.02, 0, 0.48, 0.04, 1.42);
            c.Col(C(70, 45, 30)); c.Box(-0.231, 0.30, 0, 0.004, 0.46, 0.40);
            w.Col(C(215, 175, 140));
            for (int k = -1; k <= 1; k++) w.Box(0.232, 0.30, k * 0.46, 0.006, 0.40, 0.40);
            foreach (double z in new[] { -0.45, 0.45 })
                for (int k = 0; k < 3; k++)
                {
                    double y = 0.13 + k * 0.15;
                    w.Col(C(230, 200, 160)); w.Box(-0.235, y, z, 0.012, 0.13, 0.44);
                    c.Col(Gold); c.Sphere(-0.248, y, z, 0.018, 6, 4);
                }
            // apple
            c.Col(C(220, 40, 40)); c.Sphere(0.05, 0.645, 0.50, 0.045, 12, 7);
            c.Col(C(100, 60, 30)); c.Rod(P(0.05, 0.685, 0.5), P(0.055, 0.71, 0.5), 0.005, 4);
            c.Col(C(80, 170, 60)); c.Ellipsoid(0.068, 0.703, 0.51, 0.02, 0.006, 0.012, 6, 3);
            // stack of books
            var covers = new[] { C(200, 60, 60), C(60, 110, 200), C(70, 160, 80) };
            for (int k = 0; k < 3; k++)
            {
                m.Push(Mul(T(0, 0.62 + k * 0.04, -0.45), M4.RotY(k * 0.15 - 0.1)));
                c.Col(covers[k]); c.Box(0, 0, 0, 0.22, 0.04, 0.30);
                c.Col(White); c.Box(0.005, 0, 0, 0.215, 0.032, 0.29);
                m.Pop();
            }
            // pencil cup
            c.Col(C(70, 130, 200)); c.Cylinder(0.05, 0.60, -0.08, 0.04, 0.045, 0.70, 12);
            c.Col(C(30, 40, 60)); Annulus(c, 0.05, 0.701, -0.08, 0, 0.038, 12);
            var pens = new[] { C(250, 210, 60), C(220, 60, 60), C(70, 170, 80) };
            for (int k = 0; k < 3; k++)
            {
                double dz = (k - 1) * 0.018;
                c.Col(pens[k]); c.Rod(P(0.05, 0.64, -0.08 + dz), P(0.05 + (k - 1) * 0.015, 0.79, -0.08 + dz * 2), 0.007, 6);
            }
        }

        /// <summary>Wrapped present ≈0.25 cube with gold ribbons and a bow; origin at its bottom centre.</summary>
        static void Gift(Model m)
        {
            var c = m.Plain;
            c.Col(C(225, 45, 55)); c.Box(0, 0.11, 0, 0.24, 0.22, 0.24);
            c.Col(C(240, 65, 70)); c.Box(0, 0.235, 0, 0.26, 0.05, 0.26);
            c.Col(Gold);
            c.Box(0, 0.11, 0, 0.245, 0.22, 0.05);
            c.Box(0, 0.11, 0, 0.05, 0.22, 0.245);
            c.Box(0, 0.235, 0, 0.265, 0.052, 0.05);
            c.Box(0, 0.235, 0, 0.05, 0.052, 0.265);
            for (int s = -1; s <= 1; s += 2)
            {
                c.Push(Mul(T(0, 0.275, s * 0.045), M4.RotX(s * 0.6)));
                c.Ellipsoid(0, 0, 0, 0.03, 0.035, 0.05, 8, 5);
                c.Pop();
                c.Push(Mul(T(s * 0.045, 0.275, 0), M4.RotZ(-s * 0.6)));
                c.Ellipsoid(0, 0, 0, 0.05, 0.035, 0.03, 8, 5);
                c.Pop();
            }
            c.Col(GoldDark); c.Sphere(0, 0.27, 0, 0.024, 8, 4);
        }

        /// <summary>Wooden bench along z with the backrest at -x and metal legs.</summary>
        static void Bench(Model m)
        {
            var w = m.Get(WoodTex());
            var c = m.Plain;
            w.Col(C(220, 175, 130));
            for (int k = -1; k <= 1; k++) w.Box(k * 0.11, 0.43, 0, 0.10, 0.03, 1.2);
            w.Push(Mul(T(-0.167, 0.62, 0), M4.RotZ(0.16))); w.Box(0, 0, 0, 0.03, 0.09, 1.2); w.Pop();
            w.Push(Mul(T(-0.188, 0.75, 0), M4.RotZ(0.16))); w.Box(0, 0, 0, 0.03, 0.09, 1.2); w.Pop();
            c.Col(C(70, 85, 80));
            for (int s = -1; s <= 1; s += 2)
            {
                double z = s * 0.5;
                c.Rod(P(0.14, 0, z), P(0.14, 0.415, z), 0.018, 6);
                c.Rod(P(-0.14, 0, z), P(-0.16, 0.415, z), 0.018, 6);
                c.Rod(P(-0.16, 0.415, z), P(-0.225, 0.84, z), 0.018, 6);
                c.Rod(P(-0.17, 0.405, z), P(0.17, 0.405, z), 0.018, 6);
                c.Sphere(-0.16, 0.415, z, 0.018, 6, 3);
            }
        }

        /// <summary>Terracotta pot with lush leaves, ≈0.9 tall.</summary>
        static void Plant(Model m)
        {
            var c = m.Plain;
            c.Col(C(200, 105, 65)); c.Cylinder(0, 0, 0, 0.12, 0.16, 0.28, 16);
            c.Col(C(215, 120, 75)); c.Cylinder(0, 0.27, 0, 0.175, 0.175, 0.33, 16);
            c.Col(C(85, 55, 35)); c.Cylinder(0, 0.30, 0, 0.16, 0.16, 0.335, 14);
            var lf = m.Get(LeafTex());
            for (int k = 0; k < 11; k++)
            {
                bool outer = k < 6;
                double az = outer ? k * PI / 3 : (k - 6) * 2 * PI / 5 + 0.4;
                double tilt = outer ? 0.85 : 0.35, len = outer ? 0.40 : 0.48;
                lf.Col(outer ? C(225, 245, 215) : C(255, 255, 255));
                m.Push(Mul(T(0, 0.33, 0), M4.RotY(az), M4.RotZ(tilt)));
                lf.Ellipsoid(0, len / 2, 0, 0.018, len / 2, 0.075, 8, 6);
                m.Pop();
            }
            lf.Col(White);
            lf.Ellipsoid(0, 0.60, 0, 0.16, 0.14, 0.16, 12, 7);
            lf.Ellipsoid(0.07, 0.72, 0.05, 0.12, 0.12, 0.12, 10, 6);
            lf.Ellipsoid(-0.06, 0.78, -0.04, 0.11, 0.11, 0.11, 10, 6);
        }

        /// <summary>School globe on a wooden stand with a gold meridian ring.</summary>
        static void Globe(Model m)
        {
            var w = m.Get(WoodTex());
            var c = m.Plain;
            w.Col(C(160, 110, 70));
            w.Cylinder(0, 0, 0, 0.16, 0.13, 0.05, 16);
            w.Cylinder(0, 0.05, 0, 0.045, 0.025, 0.17, 10);
            c.Col(Gold);
            Tube(c, new[] { P(0, 0.17, 0), P(0, 0.30, -0.05), P(0, 0.413, -0.0897) }, 0.012, 6);
            m.Push(Mul(T(0, 0.62, 0), M4.RotX(0.41)));
            m.Get(GlobeTex()).Ellipsoid(0, 0, 0, 0.2, 0.2, 0.2, 24, 14);
            c.Torus(0, 0, 0, 0.225, 0.01, 28, 5);
            c.Sphere(0, 0.225, 0, 0.018, 6, 3);
            c.Sphere(0, -0.225, 0, 0.018, 6, 3);
            m.Pop();
        }

        /// <summary>Standing gold coin (faces ±x) with a raised star on both sides.</summary>
        static void Coin(Model m)
        {
            var c = m.Plain;
            c.Col(C(255, 200, 40));
            c.Push(Mul(T(0, 0.14, 0), M4.RotZ(-PI / 2)));
            c.Cylinder(0, -0.015, 0, 0.123, 0.123, 0.015, 24);
            c.Pop();
            c.Col(C(230, 165, 25)); c.Torus(0, 0.14, 0, 0.123, 0.016, 24, 6);
            c.Col(C(255, 235, 120));
            StarX(c, 0.14, 0, 0.085, 0.035, 0.012, 0.022);
            StarX(c, 0.14, 0, 0.085, 0.035, -0.022, -0.012);
        }

        // ================================================================== garden and playground

        static void Tree(Model m, int v)
        {
            var lf = m.Get(LeafTex());
            if (v == 0)
            {
                // a big round deciduous tree
                var bark = m.Get(BarkTex());
                bark.Cylinder(0, 0, 0, 0.17, 0.11, 1.7, 12, false, 2);
                bark.Cylinder(0, 0, 0, 0.26, 0.17, 0.25, 12, false, 2);
                Cone(bark, P(0, 1.3, 0), P(0.45, 1.95, 0.25), 0.07, 0.04, 8, false);
                Cone(bark, P(0, 1.4, 0), P(-0.4, 2.0, -0.3), 0.07, 0.04, 8, false);
                var crowns = new[] { new[] { 0, 2.3, 0, 0.95, 0.8 }, new[] { 0.5, 2.05, 0.35, 0.6, 0.55 }, new[] { -0.5, 2.15, -0.35, 0.62, 0.55 }, new[] { 0.15, 2.85, -0.1, 0.62, 0.55 }, new[] { -0.35, 1.95, 0.45, 0.5, 0.45 }, new[] { 0.4, 2.45, -0.5, 0.5, 0.45 } };
                foreach (var q in crowns)
                {
                    lf.Col(q[3] > 0.9 ? C(235, 245, 230) : White);
                    lf.Ellipsoid(q[0], q[1], q[2], q[3], q[4], q[3], 14, 9, -1, 1, 3);
                }
            }
            else if (v == 1)
            {
                // a slender birch: white bark, light green drooping crowns
                var bark = m.Get(BirchTex());
                m.Push(M4.RotZ(0.04));
                bark.Cylinder(0, 0, 0, 0.11, 0.06, 2.6, 10, false, 1);
                m.Pop();
                m.Push(Mul(T(0.1, 0, 0.08), M4.RotZ(-0.12)));
                bark.Cylinder(0, 0, 0, 0.07, 0.04, 2.1, 8, false, 1);
                m.Pop();
                lf.Col(C(190, 235, 150));
                var crowns = new[] { new[] { -0.05, 2.45, 0, 0.62, 0.85 }, new[] { 0.32, 2.0, 0.25, 0.45, 0.6 }, new[] { -0.3, 2.15, -0.22, 0.45, 0.62 }, new[] { -0.07, 3.05, 0.05, 0.38, 0.45 }, new[] { -0.25, 1.8, 0.3, 0.35, 0.45 } };
                foreach (var q in crowns) lf.Ellipsoid(q[0], q[1], q[2], q[3], q[4], q[3], 14, 9, -1, 1, 2);
            }
            else
            {
                // a pine: stacked cones
                m.Get(BarkTex()).Cylinder(0, 0, 0, 0.13, 0.07, 1.2, 10, false, 2);
                lf.Col(C(110, 160, 115));
                var cones = new[] { new[] { 0.5, 1.05, 1.9 }, new[] { 1.15, 0.85, 2.45 }, new[] { 1.75, 0.62, 2.95 }, new[] { 2.35, 0.40, 3.35 } };
                foreach (var q in cones) lf.Cylinder(0, q[0], 0, q[1], 0, q[2], 18, true, 4);
            }
        }

        /// <summary>Teofila's lion fountain: stone basin with water, a column with a bowl and four golden lion heads.</summary>
        static void Fountain(Model m)
        {
            var s = m.Get(StoneTex());
            var c = m.Plain;
            s.Cylinder(0, 0, 0, 1.6, 1.6, 0.5, 32, false, 8);
            InnerWall(s, 0, 0, 1.45, 0.39, 0.5, 32, 8);
            Annulus(s, 0, 0.5, 0, 1.45, 1.6, 32, 8);
            c.Col(C(70, 150, 215)); Annulus(c, 0, 0.40, 0, 0, 1.45, 32);
            c.Col(C(130, 190, 240));
            Annulus(c, 0, 0.402, 0, 0.68, 0.71, 32);
            Annulus(c, 0, 0.402, 0, 1.08, 1.11, 32);
            // column with a bowl on top
            s.Cylinder(0, 0.38, 0, 0.32, 0.30, 0.55, 16, true, 2);
            s.Cylinder(0, 0.55, 0, 0.21, 0.16, 1.32, 16, false, 2);
            s.Cylinder(0, 1.30, 0, 0.17, 0.24, 1.38, 16, true, 2);
            s.Ellipsoid(0, 1.48, 0, 0.42, 0.14, 0.42, 20, 5, -1, 0);
            s.Push(Mul(T(0, 1.48, 0), M4.RotZ(PI / 2)));
            s.Torus(0, 0, 0, 0.41, 0.03, 24, 5);
            s.Pop();
            c.Col(C(70, 150, 215)); Annulus(c, 0, 1.46, 0, 0, 0.40, 20);
            s.Cylinder(0, 1.46, 0, 0.05, 0.04, 1.62, 8);
            s.Sphere(0, 1.66, 0, 0.06, 10, 6);
            for (int k = 0; k < 4; k++)
            {
                m.Push(M4.RotY(k * PI / 2));
                LionHead(c);
                m.Pop();
            }
        }

        /// <summary>Golden lion head on the column facing +x, with a jet of water from its open mouth.</summary>
        static void LionHead(MeshBuilder c)
        {
            c.Col(C(205, 140, 35)); c.Torus(0.205, 1.0, 0, 0.095, 0.042, 10, 4);
            for (int s = -1; s <= 1; s += 2) c.Sphere(0.215, 1.085, s * 0.055, 0.026, 5, 3);
            c.Col(C(245, 195, 60)); c.Ellipsoid(0.235, 1.0, 0, 0.055, 0.085, 0.075, 8, 4);
            c.Col(C(250, 210, 90)); c.Ellipsoid(0.285, 0.975, 0, 0.04, 0.035, 0.045, 6, 4);
            c.Col(C(150, 90, 20)); c.Sphere(0.322, 0.99, 0, 0.014, 5, 2);
            c.Col(C(70, 35, 25)); c.Ellipsoid(0.305, 0.948, 0, 0.022, 0.016, 0.026, 6, 3);
            c.Col(C(40, 25, 15));
            for (int s = -1; s <= 1; s += 2) c.Sphere(0.278, 1.03, s * 0.03, 0.011, 4, 2);
            c.Col(C(150, 200, 245));
            var pts = new V3[6];
            for (int k = 0; k < 6; k++)
            {
                double t = k / 5.0;
                pts[k] = P(0.32 + 0.9 * t, 0.945 + 0.15 * t - 0.7 * t * t, 0);
            }
            Tube(c, pts, 0.022, 5);
        }

        static void Peacock(Model m)
        {
            var c = m.Plain;
            // legs and toes
            c.Col(C(170, 140, 110));
            for (int s = -1; s <= 1; s += 2)
            {
                double z = s * 0.045;
                c.Rod(P(0.02, 0, z), P(0, 0.30, z), 0.011, 6);
                c.Rod(P(0.02, 0.006, z), P(0.08, 0.006, z), 0.007, 4);
                c.Rod(P(0.02, 0.006, z), P(0.06, 0.006, z + 0.035), 0.007, 4);
                c.Rod(P(0.02, 0.006, z), P(0.06, 0.006, z - 0.035), 0.007, 4);
                c.Rod(P(0.02, 0.006, z), P(-0.03, 0.006, z), 0.007, 4);
            }
            // long closed tail trailing down to -x, with eye spots
            c.Push(Mul(T(-0.425, 0.215, 0), M4.RotZ(0.47)));
            c.Col(C(40, 140, 90)); c.Ellipsoid(0, 0, 0, 0.38, 0.035, 0.12, 14, 6);
            foreach (double lx in new[] { -0.28, -0.12, 0.04 })
                for (int s = -1; s <= 1; s += 2)
                {
                    double lz = s * 0.05, ys = 0.035 * Math.Sqrt(Math.Max(0, 1 - Math.Pow(lx / 0.38, 2) - Math.Pow(lz / 0.12, 2))) + 0.002;
                    c.Col(C(220, 190, 60)); c.Ellipsoid(lx, ys, lz, 0.03, 0.008, 0.025, 8, 3);
                    c.Col(C(40, 80, 200)); c.Ellipsoid(lx, ys + 0.004, lz, 0.017, 0.006, 0.014, 6, 3);
                }
            c.Pop();
            // body, wings, neck, head
            c.Col(C(35, 85, 195));
            c.Push(Mul(T(0, 0.40, 0), M4.RotZ(0.35)));
            c.Ellipsoid(0, 0, 0, 0.17, 0.12, 0.11, 16, 9);
            c.Pop();
            c.Col(C(60, 110, 140));
            for (int s = -1; s <= 1; s += 2)
            {
                c.Push(Mul(T(-0.03, 0.41, s * 0.085), M4.RotZ(0.25)));
                c.Ellipsoid(0, 0, 0, 0.14, 0.08, 0.035, 10, 5);
                c.Pop();
            }
            c.Col(C(30, 100, 215));
            Limb(c, P(0.08, 0.45, 0), P(0.16, 0.73, 0), 0.05, 10, 6);
            c.Sphere(0.175, 0.77, 0, 0.055, 12, 7);
            c.Col(C(225, 200, 150)); Cone(c, P(0.215, 0.765, 0), P(0.28, 0.758, 0), 0.016, 0.002, 6);
            for (int s = -1; s <= 1; s += 2)
            {
                c.Col(White); c.Ellipsoid(0.195, 0.78, s * 0.042, 0.018, 0.012, 0.008, 6, 3);
                c.Col(Pupil); c.Sphere(0.205, 0.782, s * 0.047, 0.008, 6, 3);
            }
            // crest
            foreach (var tip in new[] { P(0.12, 0.92, 0), P(0.16, 0.94, 0), P(0.20, 0.92, 0) })
            {
                c.Col(C(30, 90, 200)); c.Rod(P(0.17, 0.815, 0), tip, 0.004, 4);
                c.Col(C(40, 170, 160)); Sph(c, tip, 0.014, 6, 3);
            }
        }

        /// <summary>The open tail fan of the peacock (drawn together with "peacock"): a cupped fan of textured strips.</summary>
        static void PeacockFan(Model m)
        {
            var b = m.Get(FanTex(), true);
            const int N = 8;
            const double Y0 = 0.15, Y1 = 1.10, Hw = 0.7;
            Func<double, double> fx = z => -0.15 + 0.10 * (z / Hw) * (z / Hw);
            for (int side = 0; side < 2; side++)
            {
                b.Col(side == 0 ? White : C(150, 140, 120));
                for (int i = 0; i < N; i++)
                {
                    double za = Hw - 2 * Hw * i / N, zb = Hw - 2 * Hw * (i + 1) / N;
                    float ua = (float)((Hw - za) / (2 * Hw)), ub = (float)((Hw - zb) / (2 * Hw));
                    V3 p0 = P(fx(za), Y0, za), p1 = P(fx(zb), Y0, zb), p2 = P(fx(zb), Y1, zb), p3 = P(fx(za), Y1, za);
                    if (side == 0) QuadUV(b, p0, p1, p2, p3, new[] { ua, 1f, ub, 1f, ub, 0f, ua, 0f });
                    else QuadUV(b, p1, p0, p3, p2, new[] { ub, 1f, ua, 1f, ua, 0f, ub, 0f });
                }
            }
        }

        /// <summary>White swan floating on the water (origin at the water line).</summary>
        static void Swan(Model m)
        {
            var c = m.Plain;
            c.Col(C(248, 248, 245));
            c.Ellipsoid(0, 0.04, 0, 0.30, 0.13, 0.17, 18, 8, -0.35, 1);
            c.Push(Mul(T(-0.27, 0.11, 0), M4.RotZ(-0.6)));
            c.Ellipsoid(0, 0, 0, 0.11, 0.045, 0.09, 10, 5);
            c.Pop();
            c.Col(C(240, 240, 235));
            for (int s = -1; s <= 1; s += 2)
            {
                c.Push(Mul(T(-0.04, 0.12, s * 0.10), M4.RotZ(-0.15)));
                c.Ellipsoid(0, 0, 0, 0.22, 0.06, 0.065, 12, 6);
                c.Pop();
            }
            // S-shaped neck
            c.Col(C(248, 248, 245));
            var neck = new[] { P(0.20, 0.10, 0), P(0.25, 0.20, 0), P(0.27, 0.31, 0), P(0.23, 0.42, 0), P(0.20, 0.51, 0), P(0.23, 0.59, 0), P(0.28, 0.62, 0) };
            var rad = new[] { 0.05, 0.045, 0.04, 0.037, 0.035, 0.034, 0.034 };
            for (int k = 0; k + 1 < neck.Length; k++)
            {
                Cone(c, neck[k], neck[k + 1], rad[k], rad[k + 1], 10, false);
                Sph(c, neck[k + 1], rad[k + 1], 10, 5);
            }
            c.Ellipsoid(0.29, 0.625, 0, 0.065, 0.045, 0.045, 12, 7);
            c.Col(C(240, 130, 40)); Cone(c, P(0.34, 0.618, 0), P(0.43, 0.595, 0), 0.024, 0.006, 8);
            c.Col(Pupil);
            c.Sphere(0.338, 0.628, 0, 0.02, 8, 4);
            for (int s = -1; s <= 1; s += 2) c.Sphere(0.31, 0.64, s * 0.04, 0.009, 6, 3);
        }

        /// <summary>Football goal: mouth at x = 0 opening to +x, posts at z = ±1.5, net (AlphaTest) on the back, top and sides.</summary>
        static void Goal(Model m)
        {
            var c = m.Plain;
            c.Col(White);
            for (int s = -1; s <= 1; s += 2)
            {
                double z = s * 1.5;
                c.Rod(P(0, 0, z), P(0, 1.2, z), 0.05, 10);
                c.Sphere(0, 1.2, z, 0.05, 10, 5);
            }
            c.Rod(P(0, 1.2, -1.5), P(0, 1.2, 1.5), 0.05, 10);
            c.Col(C(225, 225, 225));
            for (int s = -1; s <= 1; s += 2)
            {
                double z = s * 1.5;
                c.Rod(P(0, 1.2, z), P(-0.5, 1.2, z), 0.022, 6);
                c.Rod(P(-0.5, 1.2, z), P(-0.9, 0, z), 0.022, 6);
                c.Rod(P(0, 0.02, z), P(-0.9, 0.02, z), 0.022, 6);
            }
            c.Rod(P(-0.5, 1.2, -1.5), P(-0.5, 1.2, 1.5), 0.022, 6);
            c.Rod(P(-0.9, 0.02, -1.5), P(-0.9, 0.02, 1.5), 0.022, 6);
            // net: planar uv in cells of 0.15
            var n = m.Get(NetTex(), true);
            const float k = 1 / 0.15f;
            float slope = (float)Math.Sqrt(0.4 * 0.4 + 1.2 * 1.2);
            NetQuad(n, P(0, 1.2, 1.5), P(0, 1.2, -1.5), P(-0.5, 1.2, -1.5), P(-0.5, 1.2, 1.5), new[] { 0f, 0, 3 * k, 0, 3 * k, 0.5f * k, 0, 0.5f * k });
            NetQuad(n, P(-0.5, 1.2, 1.5), P(-0.5, 1.2, -1.5), P(-0.9, 0, -1.5), P(-0.9, 0, 1.5), new[] { 0f, 0, 3 * k, 0, 3 * k, slope * k, 0, slope * k });
            for (int s = -1; s <= 1; s += 2)
            {
                double z = s * 1.5;
                NetQuad(n, P(0, 0, z), P(-0.9, 0, z), P(-0.5, 1.2, z), P(0, 1.2, z), new[] { 0f, 0, 0.9f * k, 0, 0.5f * k, 1.2f * k, 0, 1.2f * k });
            }
        }

        /// <summary>Double-sided net quad with explicit uvs.</summary>
        static void NetQuad(MeshBuilder b, V3 p0, V3 p1, V3 p2, V3 p3, float[] uv)
        {
            QuadUV(b, p0, p1, p2, p3, uv);
            QuadUV(b, p3, p2, p1, p0, new[] { uv[6], uv[7], uv[4], uv[5], uv[2], uv[3], uv[0], uv[1] });
        }

        static void Pitch(Model m)
        {
            const float y = 0.012f;
            m.Get(PitchTex()).Quad(new V3(-10, y, 7), new V3(10, y, 7), new V3(10, y, -7), new V3(-10, y, -7));
        }

        static void Backdrop(Model m)
        {
            m.Get(CastleTex(), true).Quad(new V3(0, 0, 17), new V3(0, 0, -17), new V3(0, 9, -17), new V3(0, 9, 17));
        }

        /// <summary>Signpost "BOISKO SP 2 KÓRNIK" / "PLAYGROUND SP 2 KÓRNIK" facing +x.</summary>
        static void Sign(Model m)
        {
            var w = m.Get(WoodTex());
            w.Col(C(200, 160, 120));
            for (int s = -1; s <= 1; s += 2) w.Box(-0.06, 0.8, s * 0.72, 0.08, 1.6, 0.08);
            w.Col(C(170, 120, 75)); w.Box(0, 1.3, 0, 0.05, 0.62, 1.64);
            w.Col(C(150, 105, 65)); w.Box(-0.01, 1.635, 0, 0.12, 0.03, 1.72);
            m.Get(SignTex()).Quad(new V3(0.0265, 1.02, 0.79), new V3(0.0265, 1.02, -0.79), new V3(0.0265, 1.58, -0.79), new V3(0.0265, 1.58, 0.79));
        }

        // ================================================================== textures

        static Tex shellTex, woodTex, leafTex, barkTex, birchTex, stoneTex, ballTex, globeTex, robeTex, netTex, fanTex, pitchTex, castleTex;
        static readonly Tex[] signTex = new Tex[2];

        static Color CA(int a, int r, int g, int b) => Color.FromArgb(Math.Max(0, Math.Min(255, a)), C(r, g, b));
        static Color CA(int a, Color c) => Color.FromArgb(Math.Max(0, Math.Min(255, a)), c);
        static float Rf(Random r) => (float)r.NextDouble();

        static Graphics Gfx(Bitmap bmp)
        {
            var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            return g;
        }

        static Tex Paint(int w, int h, bool transparent, Action<Graphics> paint)
        {
            using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                using (var g = Gfx(bmp)) paint(g);
                var t = Tex.FromBitmap(bmp, transparent);
                if (transparent) Bleed(t);
                return t;
            }
        }

        /// <summary>Opaque texture computed per pixel from (u, v) in 0..1.</summary>
        static Tex PerPixel(int w, int h, Func<double, double, Color> f)
        {
            var t = new Tex(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    t.Px[y * w + x] = f((x + 0.5) / w, (y + 0.5) / h).ToArgb() | unchecked((int)0xFF000000);
            return t;
        }

        /// <summary>Copies colours into neighbouring transparent pixels (alpha stays 0) so filtering doesn't darken cut-out edges.</summary>
        static void Bleed(Tex t)
        {
            int w = t.W, h = t.H;
            var px = t.Px;
            var filled = new bool[px.Length];
            for (int i = 0; i < px.Length; i++) filled[i] = px[i] != 0;
            for (int pass = 0; pass < 4; pass++)
            {
                var next = (bool[])filled.Clone();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (filled[i]) continue;
                        int n = x > 0 && filled[i - 1] ? i - 1 : x < w - 1 && filled[i + 1] ? i + 1 : y > 0 && filled[i - w] ? i - w : y < h - 1 && filled[i + w] ? i + w : -1;
                        if (n < 0) continue;
                        px[i] = px[n] & 0x00FFFFFF;
                        next[i] = true;
                    }
                filled = next;
            }
        }

        static void FillE(Graphics g, Color c, float x, float y, float w, float h)
        {
            using (var b = new SolidBrush(c)) g.FillEllipse(b, x, y, w, h);
        }

        static void FillR(Graphics g, Color c, float x, float y, float w, float h)
        {
            using (var b = new SolidBrush(c)) g.FillRectangle(b, x, y, w, h);
        }

        static void FillPoly(Graphics g, Color c, params float[] xy)
        {
            var pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(xy[i * 2], xy[i * 2 + 1]);
            using (var b = new SolidBrush(c)) g.FillPolygon(b, pts);
        }

        static void Line(Graphics g, Color c, float width, float x1, float y1, float x2, float y2)
        {
            using (var p = new Pen(c, width) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, x1, y1, x2, y2);
        }

        static Color Lerp(Color a, Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return C((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        /// <summary>Turtle shell plates: a central plate at the pole, a ring of 6 and a ring of 12 plates, gold borders.</summary>
        static Tex ShellTex() => shellTex ?? (shellTex = PerPixel(512, 256, (u, v) =>
        {
            const int W = 512, H = 256;
            double vv = v < 0.5 ? v : 1 - v;
            double cl = Math.Cos((0.5 - vv) * PI);
            double b1 = 0.12 + 0.012 * Math.Cos(6 * 2 * PI * u);
            double dist;
            Color plate;
            if (vv < b1)
            {
                dist = (b1 - vv) * H;
                plate = C(135, 185, 70);
            }
            else if (vv < 0.32)
            {
                double f = u * 6 - Math.Floor(u * 6);
                int k = (int)Math.Round(u * 6) % 6;
                dist = Math.Min(Math.Min((vv - b1) * H, (0.32 - vv) * H), Math.Abs(f - 0.5) / 6 * W * cl);
                plate = k % 2 == 0 ? C(120, 172, 62) : C(110, 162, 56);
            }
            else
            {
                double f = u * 12 - Math.Floor(u * 12);
                int k = (int)Math.Floor(u * 12);
                dist = Math.Min((vv - 0.32) * H, Math.Min(f, 1 - f) / 12 * W * cl);
                plate = k % 2 == 0 ? C(100, 150, 50) : C(92, 140, 46);
            }
            if (dist < 2.2) return C(45, 85, 32);
            if (dist < 5.5) return RimGold;
            var col = Lerp(Shade(plate, -30), Shade(plate, 18), dist / 28);
            if (dist > 9 && (dist % 11) < 1.1) col = Shade(col, -14);   // growth rings
            return col;
        }));

        static Tex WoodTex() => woodTex ?? (woodTex = Paint(256, 128, false, g =>
        {
            FillR(g, C(205, 160, 105), 0, 0, 256, 128);
            var r = new Random(5);
            for (int i = 0; i < 46; i++)
            {
                float y0 = Rf(r) * 128, amp = 1 + Rf(r) * 4, ph = Rf(r) * 6;
                var col = Rf(r) < 0.7 ? CA(90 + r.Next(80), 150, 105, 60) : CA(70, 230, 195, 145);
                var pts = new PointF[17];
                for (int k = 0; k < pts.Length; k++) pts[k] = new PointF(k * 16, y0 + amp * (float)Math.Sin(k * 0.5 + ph));
                using (var p = new Pen(col, 0.8f + Rf(r) * 1.8f)) g.DrawLines(p, pts);
            }
            for (int i = 0; i < 2; i++)
            {
                float x = 50 + i * 130, y = 30 + i * 60;
                for (int k = 0; k < 4; k++)
                    using (var p = new Pen(CA(110, 130, 85, 45), 1.5f)) g.DrawEllipse(p, x - 6 - k * 4, y - 3 - k * 2, 12 + k * 8, 6 + k * 4);
            }
        }));

        static Tex LeafTex() => leafTex ?? (leafTex = Paint(256, 256, false, g =>
        {
            FillR(g, C(45, 105, 40), 0, 0, 256, 256);
            var r = new Random(11);
            for (int i = 0; i < 700; i++)
            {
                float x = Rf(r) * 256, y = Rf(r) * 256, a = Rf(r) * 360, lw = 10 + Rf(r) * 9, lh = 5 + Rf(r) * 4;
                var col = C(40 + r.Next(70), 120 + r.Next(65), 30 + r.Next(40));
                for (int ox = -1; ox <= 1; ox++)
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        float px = x + ox * 256, py = y + oy * 256;
                        if (px < -20 || px > 276 || py < -20 || py > 276) continue;
                        g.TranslateTransform(px, py);
                        g.RotateTransform(a);
                        FillE(g, col, -lw / 2, -lh / 2, lw, lh);
                        g.ResetTransform();
                    }
            }
        }));

        static Tex BarkTex() => barkTex ?? (barkTex = Paint(128, 256, false, g =>
        {
            FillR(g, C(105, 75, 50), 0, 0, 128, 256);
            var r = new Random(13);
            for (int i = 0; i < 70; i++)
            {
                float x = Rf(r) * 128, w = 1.5f + Rf(r) * 3;
                var col = Rf(r) < 0.65 ? C(65, 45, 28) : C(140, 105, 75);
                var pts = new PointF[9];
                for (int k = 0; k < pts.Length; k++) pts[k] = new PointF(x + (float)Math.Sin(k * 1.3 + i) * 3, k * 32);
                for (int ox = -1; ox <= 1; ox++)
                {
                    var q = Array.ConvertAll(pts, p => new PointF(p.X + ox * 128, p.Y));
                    using (var p = new Pen(col, w)) g.DrawLines(p, q);
                }
            }
        }));

        static Tex BirchTex() => birchTex ?? (birchTex = Paint(128, 256, false, g =>
        {
            FillR(g, C(238, 235, 228), 0, 0, 128, 256);
            var r = new Random(17);
            for (int i = 0; i < 30; i++) FillR(g, CA(60, 160, 155, 145), 0, Rf(r) * 256, 128, 1 + Rf(r) * 2);
            for (int i = 0; i < 46; i++)
            {
                float x = Rf(r) * 128, y = Rf(r) * 256, w = 8 + Rf(r) * 30, h = 2.5f + Rf(r) * 4;
                for (int ox = -1; ox <= 1; ox++) FillE(g, C(45, 42, 40), x + ox * 128 - w / 2, y, w, h);
            }
        }));

        static Tex StoneTex() => stoneTex ?? (stoneTex = Paint(256, 256, false, g =>
        {
            FillR(g, C(140, 135, 125), 0, 0, 256, 256);
            var r = new Random(19);
            for (int row = 0; row < 4; row++)
                for (int k = 0; k < 5; k++)
                {
                    float x = k * 64 - (row % 2) * 32, y = row * 64;
                    int tone = r.Next(35);
                    var col = C(170 + tone, 165 + tone, 152 + tone);
                    using (var path = new GraphicsPath())
                    {
                        path.AddRectangle(new RectangleF(x + 3, y + 3, 58, 58));
                        using (var br = new LinearGradientBrush(new RectangleF(x, y, 64, 64), Shade(col, 14), Shade(col, -16), 60f)) g.FillPath(br, path);
                    }
                    for (int s = 0; s < 40; s++) FillR(g, CA(70, 100, 95, 90), x + 4 + Rf(r) * 55, y + 4 + Rf(r) * 55, 2, 2);
                }
        }));

        /// <summary>Football: black pentagons and white hexagons (truncated icosahedron), equirectangular.</summary>
        static Tex BallTex()
        {
            if (ballTex != null) return ballTex;
            double ph = (1 + Math.Sqrt(5)) / 2;
            var ico = new List<V3>();
            foreach (var s1 in new[] { -1, 1 })
                foreach (var s2 in new[] { -1, 1 })
                {
                    ico.Add(new V3(0, s1, s2 * ph).Normalized());
                    ico.Add(new V3(s1, s2 * ph, 0).Normalized());
                    ico.Add(new V3(s2 * ph, 0, s1).Normalized());
                }
            var centres = new List<V3>(ico);
            for (int i = 0; i < 12; i++)
                for (int j = i + 1; j < 12; j++)
                    for (int k = j + 1; k < 12; k++)
                        if (V3.Dot(ico[i], ico[j]) > 0.4 && V3.Dot(ico[j], ico[k]) > 0.4 && V3.Dot(ico[i], ico[k]) > 0.4)
                            centres.Add((ico[i] + ico[j] + ico[k]).Normalized());
            return ballTex = PerPixel(256, 128, (u, v) =>
            {
                double phi = 2 * PI * u, th = (0.5 - v) * PI;
                var d = new V3(Math.Cos(th) * Math.Cos(phi), Math.Sin(th), Math.Cos(th) * Math.Sin(phi));
                double best = -2, second = -2;
                int bi = 0;
                for (int i = 0; i < centres.Count; i++)
                {
                    double dot = V3.Dot(d, centres[i]);
                    if (dot > best) { second = best; best = dot; bi = i; }
                    else if (dot > second) second = dot;
                }
                if (Math.Acos(Math.Min(1, second)) - Math.Acos(Math.Min(1, best)) < 0.03) return C(110, 110, 115);
                return bi < 12 ? C(30, 30, 35) : C(245, 245, 245);
            });
        }

        /// <summary>World map (east to the right when seen from outside the sphere) with a light grid.</summary>
        static Tex GlobeTex() => globeTex ?? (globeTex = Paint(512, 256, false, g =>
        {
            const float W = 512, H = 256;
            Func<float, float> X = lon => (180 - lon) / 360 * W;
            Func<float, float> Y = lat => (90 - lat) / 180 * H;
            using (var br = new LinearGradientBrush(new RectangleF(0, -1, W, H + 2), C(60, 130, 215), C(30, 85, 180), 90f))
            {
                br.SetBlendTriangularShape(0.5f);
                g.FillRectangle(br, 0, 0, W, H);
            }
            Action<Color, float[]> land = (col, ll) =>
            {
                var pts = new PointF[ll.Length / 2];
                for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(X(ll[i * 2]), Y(ll[i * 2 + 1]));
                using (var b = new SolidBrush(col)) g.FillClosedCurve(b, pts, FillMode.Alternate, 0.3f);
            };
            var green = C(95, 175, 80);
            land(green, new float[] { -168, 66, -140, 70, -95, 72, -80, 63, -60, 55, -66, 45, -80, 32, -81, 25, -97, 26, -97, 18, -87, 15, -80, 8, -105, 20, -117, 32, -125, 40, -125, 49, -135, 58, -150, 60, -165, 60 });
            land(C(230, 240, 245), new float[] { -55, 82, -25, 82, -20, 70, -42, 60, -55, 68 });
            land(green, new float[] { -80, 10, -62, 10, -50, 0, -35, -6, -40, -22, -58, -38, -65, -55, -74, -50, -72, -30, -80, -5 });
            land(green, new float[] { -10, 36, -9, 44, -2, 48, -5, 58, 5, 62, 15, 70, 30, 71, 40, 66, 40, 45, 28, 40, 20, 38, 12, 44, 3, 42 });
            land(green, new float[] { -17, 21, -10, 36, 10, 37, 32, 31, 43, 12, 51, 12, 40, -15, 32, -28, 20, -35, 13, -20, 9, 4, -8, 4, -17, 14 });
            land(green, new float[] { 40, 66, 70, 73, 105, 78, 140, 72, 179, 68, 170, 60, 140, 52, 130, 42, 122, 30, 108, 20, 100, 10, 80, 8, 72, 20, 58, 25, 48, 28, 43, 12, 35, 32, 28, 40, 40, 45 });
            land(green, new float[] { 114, -22, 130, -12, 142, -11, 153, -26, 146, -39, 135, -35, 115, -34 });
            land(green, new float[] { 44, -13, 50, -15, 48, -25, 44, -24 });
            land(green, new float[] { 130, 33, 141, 41, 142, 36, 135, 33 });
            land(green, new float[] { -5, 50, 2, 51, -3, 58, -6, 56 });
            // deserts and ice
            land(C(220, 200, 130), new float[] { -12, 20, 30, 30, 32, 18, -5, 14 });
            land(C(220, 200, 130), new float[] { 38, 28, 55, 25, 52, 16, 44, 14 });
            land(C(215, 185, 120), new float[] { 120, -22, 140, -20, 140, -30, 125, -30 });
            FillR(g, C(235, 242, 248), 0, Y(-72), W, H);
            FillR(g, C(235, 242, 248), 0, 0, W, Y(80));
            using (var p = new Pen(CA(70, 255, 255, 255), 1))
            {
                for (int lat = -60; lat <= 60; lat += 30) g.DrawLine(p, 0, Y(lat), W, Y(lat));
                for (int lon = -180; lon < 180; lon += 30) g.DrawLine(p, X(lon), 0, X(lon), H);
            }
        }));

        static Tex RobeTex() => robeTex ?? (robeTex = Paint(256, 256, false, g =>
        {
            using (var br = new LinearGradientBrush(new RectangleF(0, -1, 256, 258), C(150, 80, 210), C(90, 42, 145), 90f)) g.FillRectangle(br, 0, 0, 256, 256);
            var r = new Random(7);
            for (int i = 0; i < 16; i++)
            {
                float x = Rf(r) * 256, y = 12 + Rf(r) * 196, s = 7 + Rf(r) * 6;
                for (int ox = -1; ox <= 1; ox++)
                    using (var b = new SolidBrush(C(255, 215, 80))) g.FillPolygon(b, Art.StarPoints(x + ox * 256, y, s, s * 0.42f));
            }
            FillR(g, C(200, 150, 40), 0, 224, 256, 4);
            FillR(g, C(240, 195, 70), 0, 228, 256, 28);
            for (int k = 0; k < 16; k++) FillE(g, C(200, 150, 40), k * 16 + 5, 238, 6, 6);
        }));

        /// <summary>One net cell: white lines on two edges, transparent hole (tiles with GL_REPEAT).</summary>
        static Tex NetTex() => netTex ?? (netTex = Paint(32, 32, true, g =>
        {
            g.SmoothingMode = SmoothingMode.None;
            FillR(g, C(245, 245, 245), 0, 0, 32, 3);
            FillR(g, C(245, 245, 245), 0, 0, 3, 32);
        }));

        /// <summary>Open peacock tail: a fan of feathers with eye spots, transparent outside the fan.</summary>
        static Tex FanTex() => fanTex ?? (fanTex = Paint(512, 352, true, g =>
        {
            float cx = 256, cy = 285, R = 275;
            using (var b = new SolidBrush(C(30, 90, 60))) g.FillPie(b, cx - R * 0.93f, cy - R * 0.93f, R * 1.86f, R * 1.86f, -190, 200);
            const int N = 31;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < N; i++)
                {
                    float a = -100 + 200f * i / (N - 1);
                    g.TranslateTransform(cx, cy);
                    g.RotateTransform(a);
                    if (pass == 0)
                    {
                        // barbs and shaft
                        using (var br = new LinearGradientBrush(new RectangleF(-18, -R, 36, R), C(80, 175, 95), C(40, 120, 70), 90f)) g.FillEllipse(br, -17, -R + 4, 34, R * 0.85f);
                        Line(g, C(200, 210, 150), 1.5f, 0, -20, 0, -R + 10);
                    }
                    else
                    {
                        // eye spots: outer row on every feather, inner row on every other
                        float d = R - 30;
                        FillE(g, C(200, 175, 60), -16, -d - 19, 32, 38);
                        FillE(g, C(40, 160, 95), -12, -d - 14, 24, 29);
                        FillE(g, C(30, 95, 205), -8, -d - 9, 16, 19);
                        FillE(g, C(20, 30, 90), -4, -d - 5, 8, 10);
                        if (i % 2 == 0)
                        {
                            d = R * 0.62f;
                            FillE(g, C(200, 175, 60), -11, -d - 13, 22, 26);
                            FillE(g, C(30, 95, 205), -6, -d - 7, 12, 14);
                            FillE(g, C(20, 30, 90), -3, -d - 3, 6, 7);
                        }
                    }
                    g.ResetTransform();
                }
        }));

        /// <summary>Football pitch 20x14 units (51.2 px per unit), goals at x = ±10.</summary>
        static Tex PitchTex() => pitchTex ?? (pitchTex = Paint(1024, 716, false, g =>
        {
            const float K = 51.2f, KZ = 716f / 14;
            Func<double, float> X = wx => (float)((wx + 10) * K);
            Func<double, float> Z = wz => (float)((wz + 7) * KZ);
            for (int i = 0; i < 12; i++) FillR(g, i % 2 == 0 ? C(95, 170, 70) : C(80, 150, 58), i * 1024f / 12, 0, 1024f / 12 + 1, 716);
            var white = C(245, 245, 245);
            using (var pen = new Pen(white, 5))
            {
                const double gx = 9.94, gz = 6.9;
                g.DrawRectangle(pen, X(-gx), Z(-gz), X(gx) - X(-gx), Z(gz) - Z(-gz));
                g.DrawLine(pen, X(0), Z(-gz), X(0), Z(gz));
                g.DrawEllipse(pen, X(-1.7), Z(-1.7), 3.4f * K, 3.4f * KZ);
                FillE(g, white, X(-0.12), Z(-0.12), 0.24f * K, 0.24f * KZ);
                for (int s = -1; s <= 1; s += 2)
                {
                    double line = s * gx, box = s * (gx - 3.2), area = s * (gx - 1.1), spot = s * (gx - 2.2);
                    g.DrawRectangle(pen, Math.Min(X(line), X(box)), Z(-4.0), Math.Abs(X(line) - X(box)), Z(4.0) - Z(-4.0));
                    g.DrawRectangle(pen, Math.Min(X(line), X(area)), Z(-2.3), Math.Abs(X(line) - X(area)), Z(2.3) - Z(-2.3));
                    FillE(g, white, X(spot) - 0.1f * K, Z(-0.1), 0.2f * K, 0.2f * KZ);
                    // penalty arc outside the box
                    g.SetClip(s > 0 ? new RectangleF(0, 0, X(box), 716) : new RectangleF(X(box), 0, 1024 - X(box), 716));
                    g.DrawEllipse(pen, X(spot) - 1.7f * K, Z(-1.7), 3.4f * K, 3.4f * KZ);
                    g.ResetClip();
                    // corner arcs
                    for (int t = -1; t <= 1; t += 2)
                    {
                        float r = 0.45f;
                        float sx = X(line), sz = Z(t * gz);
                        float start = s > 0 ? (t > 0 ? 180 : 90) : (t > 0 ? 270 : 0);
                        g.DrawArc(pen, sx - r * K, sz - r * KZ, 2 * r * K, 2 * r * KZ, start, 90);
                    }
                }
            }
        }));

        // ------------------------------------------------------------------ Kórnik backdrop

        /// <summary>Lake Kórnik with the castle on the shore; the sky is transparent.</summary>
        static Tex CastleTex()
        {
            if (castleTex != null) return castleTex;
            const int W = 2048, H = 512, Shore = 392;
            using (var land = new Bitmap(W, H, PixelFormat.Format32bppArgb))
            using (var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb))
            {
                using (var g = Gfx(land))
                {
                    g.SetClip(new Rectangle(0, 0, W, Shore + 2));
                    PaintLand(g, W, Shore);
                }
                using (var g = Gfx(bmp))
                {
                    using (var br = new LinearGradientBrush(new Rectangle(0, Shore - 1, W, H - Shore + 2), C(110, 160, 210), C(35, 85, 150), 90f))
                        g.FillRectangle(br, 0, Shore, W, H - Shore);
                    // squashed, faded reflection of the shore
                    var cm = new ColorMatrix(new[]
                    {
                        new float[] { 0.75f, 0, 0, 0, 0 }, new float[] { 0, 0.8f, 0, 0, 0 }, new float[] { 0, 0, 0.95f, 0, 0 },
                        new float[] { 0, 0, 0, 0.42f, 0 }, new float[] { 0, 0.02f, 0.08f, 0, 1 },
                    });
                    using (var ia = new ImageAttributes())
                    {
                        ia.SetColorMatrix(cm);
                        int bottom = Shore + (int)(Shore * 0.55);
                        g.DrawImage(land, new[] { new Point(0, bottom), new Point(W, bottom), new Point(0, Shore) }, new Rectangle(0, 0, W, Shore), GraphicsUnit.Pixel, ia);
                    }
                    var r = new Random(23);
                    for (int i = 0; i < 160; i++)
                    {
                        float y = Shore + 4 + Rf(r) * (H - Shore - 4), x = Rf(r) * W, len = 10 + Rf(r) * 40 * (y - Shore) / (H - Shore);
                        Line(g, CA(70 + r.Next(60), 255, 255, 255), 1.2f, x, y, x + len, y);
                    }
                    // a sailing boat, lily pads and reeds in the foreground
                    FillPoly(g, C(120, 70, 40), 1480, 440, 1560, 440, 1548, 452, 1492, 452);
                    FillPoly(g, C(250, 250, 245), 1518, 438, 1518, 380, 1556, 436);
                    FillPoly(g, C(235, 230, 220), 1514, 438, 1514, 392, 1488, 436);
                    Line(g, C(90, 60, 40), 2, 1517, 440, 1517, 376);
                    for (int i = 0; i < 14; i++)
                    {
                        float x = 120 + Rf(r) * 360 + (i % 2) * 1300, y = 450 + Rf(r) * 50;
                        FillE(g, C(60, 130, 55), x, y, 26 + Rf(r) * 12, 8 + Rf(r) * 4);
                        if (i % 4 == 0) FillE(g, C(250, 220, 230), x + 8, y - 2, 8, 6);
                    }
                    for (int i = 0; i < 90; i++)
                    {
                        float x = i < 45 ? Rf(r) * 260 : W - Rf(r) * 260, h = 30 + Rf(r) * 70;
                        Line(g, C(50 + r.Next(40), 110 + r.Next(50), 40), 2.5f, x, H, x + (Rf(r) - 0.5f) * 20, H - h);
                        if (i % 6 == 0) FillE(g, C(110, 75, 45), x - 3, H - h - 6, 7, 18);
                    }
                    g.DrawImage(land, new Rectangle(0, 0, W, H));
                }
                var t = Tex.FromBitmap(bmp, true);
                Bleed(t);
                return castleTex = t;
            }
        }

        static void PaintLand(Graphics g, int W, int shore)
        {
            var r = new Random(21);
            // distant hazy hills and the forest along the far shore
            for (int i = 0; i < 26; i++)
            {
                float x = i * W / 25f + (Rf(r) - 0.5f) * 60, w = 220 + Rf(r) * 160, h = 70 + Rf(r) * 60;
                FillE(g, C(125, 160, 145), x - w / 2, shore - h * 0.8f, w, h * 1.6f);
            }
            for (int x = -20; x < W + 20; x += 16)
            {
                float w = 40 + Rf(r) * 34, h = 36 + Rf(r) * 44;
                FillE(g, C(50 + r.Next(30), 95 + r.Next(35), 50 + r.Next(20)), x - w / 2, shore - h * 0.75f, w, h);
            }
            // grassy park in front of the castle
            FillE(g, C(105, 155, 75), 1024 - 480, shore - 26, 960, 60);
            Castle(g, 1024, shore);
            // big trees flanking the castle
            foreach (float tx in new[] { 560f, 680f, 1380f, 1500f, 1610f, 300f, 1830f })
            {
                float h = 120 + Rf(r) * 70;
                FillR(g, C(85, 60, 40), tx - 6, shore - h * 0.45f, 12, h * 0.45f);
                for (int k = 0; k < 6; k++)
                {
                    float w = 70 + Rf(r) * 60, ch = 60 + Rf(r) * 50;
                    float x = tx + (Rf(r) - 0.5f) * 80, y = shore - h + Rf(r) * h * 0.45f;
                    FillE(g, C(45 + r.Next(40), 100 + r.Next(50), 45 + r.Next(25)), x - w / 2, y, w, ch);
                }
            }
            // shore line with reeds
            FillR(g, C(70, 115, 55), 0, shore - 4, W, 6);
            for (int i = 0; i < 220; i++)
            {
                float x = Rf(r) * W;
                if (Math.Abs(x - 1024) < 40) continue;
                Line(g, C(60 + r.Next(40), 110 + r.Next(40), 50), 1.5f, x, shore + 1, x + (Rf(r) - 0.5f) * 6, shore - 6 - Rf(r) * 12);
            }
        }

        /// <summary>Kórnik Castle: cream neo-Gothic walls, red roofs, crenellated towers, Gothic windows.</summary>
        static void Castle(Graphics g, float cx, float shore)
        {
            var wall = C(238, 228, 205);
            var shade = C(214, 202, 176);
            var roof = C(178, 62, 50);
            var roofDark = C(140, 45, 38);
            Action<float, float, float, float, Color, Color> block = (x, y, w, h, a, b) =>
            {
                using (var br = new LinearGradientBrush(new RectangleF(x - 1, y, w + 2, h), a, b, 0f)) g.FillRectangle(br, x, y, w, h);
            };
            Action<float, float, float, int, float> merlons = (x, y, w, n, mh) =>
            {
                float step = w / (n * 2 - 1);
                for (int i = 0; i < n; i++) FillR(g, wall, x + i * 2 * step, y - mh, step, mh + 1);
            };
            // left wing with battlements, right wing with a roof
            block(cx - 420, 292, 125, shore - 292, shade, wall);
            merlons(cx - 420, 292, 125, 7, 10);
            block(cx + 270, 300, 130, shore - 300, wall, shade);
            FillPoly(g, roof, cx + 262, 302, cx + 408, 302, cx + 385, 270, cx + 285, 270);
            // main block with its roof and dormers
            FillPoly(g, roof, cx - 245, 257, cx + 245, 257, cx + 205, 212, cx - 205, 212);
            FillPoly(g, roofDark, cx - 245, 257, cx + 245, 257, cx + 240, 252, cx - 240, 252);
            foreach (float dx in new[] { -170f, -110f, 110f, 170f })
            {
                FillR(g, wall, cx + dx - 9, 226, 18, 20);
                FillPoly(g, roofDark, cx + dx - 13, 228, cx + dx + 13, 228, cx + dx, 214);
                FillR(g, C(60, 70, 95), cx + dx - 4, 232, 8, 11);
            }
            block(cx - 232, 257, 464, shore - 257, wall, shade);
            // corner turrets with pointed roofs
            foreach (float tx in new[] { cx - 246, cx + 218 })
            {
                block(tx, 206, 28, 90, wall, shade);
                FillPoly(g, shade, tx, 296, tx + 28, 296, tx + 14, 312);
                FillPoly(g, roof, tx - 4, 208, tx + 32, 208, tx + 14, 158);
            }
            // big crenellated tower on the left with a flag
            block(cx - 312, 122, 100, shore - 122, Shade(wall, 8), shade);
            block(cx - 320, 106, 116, 18, wall, shade);
            for (int i = 0; i < 9; i++) FillR(g, C(150, 140, 120), cx - 316 + i * 13, 118, 6, 5);
            merlons(cx - 320, 106, 116, 7, 14);
            Line(g, C(90, 80, 70), 2, cx - 262, 92, cx - 262, 44);
            FillR(g, C(250, 250, 250), cx - 261, 46, 34, 9);
            FillR(g, C(220, 30, 50), cx - 261, 55, 34, 9);
            // right tower with a tall pyramid roof
            block(cx + 205, 165, 66, shore - 165, wall, shade);
            block(cx + 200, 154, 76, 13, wall, shade);
            FillPoly(g, roof, cx + 198, 156, cx + 278, 156, cx + 238, 84);
            FillPoly(g, roofDark, cx + 238, 84, cx + 278, 156, cx + 256, 156);
            FillE(g, C(240, 200, 70), cx + 233, 76, 10, 10);
            // central gatehouse with battlements and the gate over the moat bridge
            block(cx - 62, 226, 124, shore - 226, Shade(wall, 6), shade);
            merlons(cx - 66, 226, 132, 7, 12);
            using (var gate = new GraphicsPath())
            {
                gate.AddArc(cx - 22, shore - 50, 44, 40, 180, 180);
                gate.AddLine(cx + 22, shore - 30, cx + 22, shore);
                gate.AddLine(cx - 22, shore, cx - 22, shore - 30);
                gate.CloseFigure();
                using (var b = new SolidBrush(C(60, 45, 40))) g.FillPath(b, gate);
            }
            FillR(g, C(200, 185, 160), cx - 34, shore - 6, 68, 6);
            GothicWindow(g, cx - 14, 244, 28, 52);
            // rows of Gothic windows
            for (float x = cx - 214; x <= cx + 200; x += 34)
            {
                if (Math.Abs(x + 7 - cx) < 80) continue;
                GothicWindow(g, x, 270, 14, 26);
                GothicWindow(g, x, 314, 15, 32);
                GothicWindow(g, x, 358, 13, 22);
            }
            foreach (float y in new[] { 150f, 205f, 262f, 320f })
            {
                GothicWindow(g, cx - 290, y, 14, 30);
                GothicWindow(g, cx - 248, y, 14, 30);
            }
            foreach (float y in new[] { 190f, 245f, 305f }) GothicWindow(g, cx + 230, y, 14, 30);
            for (int i = 0; i < 3; i++)
            {
                GothicWindow(g, cx - 400 + i * 38, 315, 13, 26);
                GothicWindow(g, cx + 290 + i * 38, 322, 13, 24);
            }
        }

        static void GothicWindow(Graphics g, float x, float y, float w, float h)
        {
            Action<float, Color> shape = (grow, col) =>
            {
                float gx = x - grow, gy = y - grow, gw = w + grow * 2, gh = h + grow * 2;
                using (var p = new GraphicsPath())
                {
                    p.AddArc(gx, gy, gw, gw, 180, 180);
                    p.AddLine(gx + gw, gy + gw / 2, gx + gw, gy + gh);
                    p.AddLine(gx, gy + gh, gx, gy + gw / 2);
                    p.CloseFigure();
                    using (var b = new SolidBrush(col)) g.FillPath(b, p);
                }
            };
            shape(2, C(250, 246, 236));
            shape(0, C(60, 70, 95));
            Line(g, C(235, 228, 210), 1.2f, x + w / 2, y + 2, x + w / 2, y + h);
        }

        /// <summary>Wooden sign board with the playground name in the current language.</summary>
        static Tex SignTex()
        {
            int li = L.Pl ? 0 : 1;
            if (signTex[li] != null) return signTex[li];
            return signTex[li] = Paint(512, 184, false, g =>
            {
                using (var br = new LinearGradientBrush(new RectangleF(0, -1, 512, 186), C(220, 172, 112), C(176, 122, 72), 90f)) g.FillRectangle(br, 0, 0, 512, 184);
                var r = new Random(29);
                for (int i = 0; i < 18; i++)
                {
                    float y = Rf(r) * 184;
                    Line(g, CA(60, 120, 75, 35), 1.2f, 0, y, 512, y + (Rf(r) - 0.5f) * 8);
                }
                using (var p = new Pen(C(105, 62, 30), 8)) g.DrawRectangle(p, 8, 8, 496, 168);
                foreach (var pt in new[] { new PointF(22, 22), new PointF(490, 22), new PointF(22, 162), new PointF(490, 162) })
                    FillE(g, C(90, 80, 70), pt.X - 5, pt.Y - 5, 10, 10);
                string[] lines = { L.Pl ? "BOISKO" : "PLAYGROUND", "SP 2 KÓRNIK" };
                float size = 72;
                Font font = null;
                while (true)
                {
                    font?.Dispose();
                    font = new Font("Comic Sans MS", size, FontStyle.Bold, GraphicsUnit.Pixel);
                    float widest = Math.Max(g.MeasureString(lines[0], font).Width, g.MeasureString(lines[1], font).Width);
                    if (widest <= 430 && size * 2.3f <= 160 || size <= 20) break;
                    size -= 2;
                }
                using (font)
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var shadow = new SolidBrush(CA(110, 255, 235, 200)))
                using (var ink = new SolidBrush(C(70, 38, 15)))
                    for (int k = 0; k < 2; k++)
                    {
                        float y = 92 + (k == 0 ? -1 : 1) * size * 0.58f;
                        g.DrawString(lines[k], font, shadow, 258, y + 2, fmt);
                        g.DrawString(lines[k], font, ink, 256, y, fmt);
                    }
            });
        }
    }
}
