using System;
using System.Collections.Generic;
using System.Drawing;

namespace LazyTurtle
{
    /// <summary>
    /// Builds triangle meshes (position, normal, uv, color) from simple shapes: quads, boxes,
    /// ellipsoids, cylinders, cones and tori. A transform stack places the shapes.
    /// Coordinates: x/z = floor plane (map X/Y), y = up.
    /// </summary>
    sealed class MeshBuilder
    {
        public const int Stride = 11;
        public readonly List<float> Data = new List<float>();
        float[] m = M4.Identity();
        readonly Stack<float[]> stack = new Stack<float[]>();
        public V3 Color = new V3(1, 1, 1);

        public int Vertices => Data.Count / Stride;

        public void Push(float[] t)
        {
            stack.Push(m);
            m = M4.Mul(m, t);
        }

        public void Pop() => m = stack.Pop();

        public MeshBuilder Col(Color c)
        {
            Color = new V3(c.R / 255.0, c.G / 255.0, c.B / 255.0);
            return this;
        }

        public void Vertex(V3 p, V3 n, float u, float v) => Vertex(p, n, u, v, Color);

        public void Vertex(V3 p, V3 n, float u, float v, V3 col)
        {
            var wp = M4.Point(m, p);
            var wn = M4.Dir(m, n).Normalized();
            Data.Add(wp.X); Data.Add(wp.Y); Data.Add(wp.Z);
            Data.Add(wn.X); Data.Add(wn.Y); Data.Add(wn.Z);
            Data.Add(u); Data.Add(v);
            Data.Add(col.X); Data.Add(col.Y); Data.Add(col.Z);
        }

        /// <summary>Quad p0..p3 counter-clockwise when seen from the front; uv (0,0) at p0, (1,1) at p2.</summary>
        public void Quad(V3 p0, V3 p1, V3 p2, V3 p3, float u0 = 0, float v0 = 0, float u1 = 1, float v1 = 1)
        {
            var n = V3.Cross(p1 - p0, p2 - p0).Normalized();
            Vertex(p0, n, u0, v1); Vertex(p1, n, u1, v1); Vertex(p2, n, u1, v0);
            Vertex(p0, n, u0, v1); Vertex(p2, n, u1, v0); Vertex(p3, n, u0, v0);
        }

        public void Box(double cx, double cy, double cz, double sx, double sy, double sz)
        {
            float x0 = (float)(cx - sx / 2), x1 = (float)(cx + sx / 2);
            float y0 = (float)(cy - sy / 2), y1 = (float)(cy + sy / 2);
            float z0 = (float)(cz - sz / 2), z1 = (float)(cz + sz / 2);
            Quad(new V3(x0, y0, z1), new V3(x1, y0, z1), new V3(x1, y1, z1), new V3(x0, y1, z1));   // +z
            Quad(new V3(x1, y0, z0), new V3(x0, y0, z0), new V3(x0, y1, z0), new V3(x1, y1, z0));   // -z
            Quad(new V3(x1, y0, z1), new V3(x1, y0, z0), new V3(x1, y1, z0), new V3(x1, y1, z1));   // +x
            Quad(new V3(x0, y0, z0), new V3(x0, y0, z1), new V3(x0, y1, z1), new V3(x0, y1, z0));   // -x
            Quad(new V3(x0, y1, z1), new V3(x1, y1, z1), new V3(x1, y1, z0), new V3(x0, y1, z0));   // top
            Quad(new V3(x0, y0, z0), new V3(x1, y0, z0), new V3(x1, y0, z1), new V3(x0, y0, z1));   // bottom
        }

        /// <summary>Ellipsoid; uv wraps once around (u) and pole to pole (v). yFrom/yTo cut it (-1..1).</summary>
        public void Ellipsoid(double cx, double cy, double cz, double rx, double ry, double rz, int slices = 16, int stacks = 10, double yFrom = -1, double yTo = 1, float uRepeat = 1)
        {
            double a0 = Math.Asin(Math.Max(-1, Math.Min(1, yFrom))), a1 = Math.Asin(Math.Max(-1, Math.Min(1, yTo)));
            for (int i = 0; i < stacks; i++)
            {
                double t0 = a0 + (a1 - a0) * i / stacks, t1 = a0 + (a1 - a0) * (i + 1) / stacks;
                for (int j = 0; j < slices; j++)
                {
                    double p0 = 2 * Math.PI * j / slices, p1 = 2 * Math.PI * (j + 1) / slices;
                    var q = new V3[4];
                    var n = new V3[4];
                    var uv = new float[8];
                    int k = 0;
                    foreach (var tp in new[] { new[] { t0, p0 }, new[] { t0, p1 }, new[] { t1, p1 }, new[] { t1, p0 } })
                    {
                        double ct = Math.Cos(tp[0]), st = Math.Sin(tp[0]), cp = Math.Cos(tp[1]), sp = Math.Sin(tp[1]);
                        var unit = new V3(ct * cp, st, ct * sp);
                        q[k] = new V3(cx + rx * unit.X, cy + ry * unit.Y, cz + rz * unit.Z);
                        n[k] = new V3(unit.X / rx, unit.Y / ry, unit.Z / rz).Normalized();
                        uv[k * 2] = (float)(tp[1] / (2 * Math.PI)) * uRepeat;
                        uv[k * 2 + 1] = (float)(0.5 - tp[0] / Math.PI);
                        k++;
                    }
                    // counter-clockwise from outside
                    Vertex(q[0], n[0], uv[0], uv[1]); Vertex(q[2], n[2], uv[4], uv[5]); Vertex(q[1], n[1], uv[2], uv[3]);
                    Vertex(q[0], n[0], uv[0], uv[1]); Vertex(q[3], n[3], uv[6], uv[7]); Vertex(q[2], n[2], uv[4], uv[5]);
                }
            }
        }

        public void Sphere(double cx, double cy, double cz, double r, int slices = 14, int stacks = 8) => Ellipsoid(cx, cy, cz, r, r, r, slices, stacks);

        /// <summary>Vertical cylinder (or truncated cone) from y0 to y1 with optional caps.</summary>
        public void Cylinder(double cx, double y0, double cz, double r0, double r1, double y1, int slices = 14, bool caps = true, float uRepeat = 1)
        {
            for (int j = 0; j < slices; j++)
            {
                double p0 = 2 * Math.PI * j / slices, p1 = 2 * Math.PI * (j + 1) / slices;
                var a = new V3(cx + Math.Cos(p0) * r0, y0, cz + Math.Sin(p0) * r0);
                var b = new V3(cx + Math.Cos(p1) * r0, y0, cz + Math.Sin(p1) * r0);
                var c = new V3(cx + Math.Cos(p1) * r1, y1, cz + Math.Sin(p1) * r1);
                var d = new V3(cx + Math.Cos(p0) * r1, y1, cz + Math.Sin(p0) * r1);
                double slope = (r0 - r1) / Math.Max(1e-6, y1 - y0);
                var n0 = new V3(Math.Cos(p0), slope, Math.Sin(p0)).Normalized();
                var n1 = new V3(Math.Cos(p1), slope, Math.Sin(p1)).Normalized();
                float u0 = (float)j / slices * uRepeat, u1 = (float)(j + 1) / slices * uRepeat;
                Vertex(a, n0, u0, 1); Vertex(c, n1, u1, 0); Vertex(b, n1, u1, 1);
                Vertex(a, n0, u0, 1); Vertex(d, n0, u0, 0); Vertex(c, n1, u1, 0);
                if (!caps) continue;
                var top = new V3(cx, y1, cz);
                var bottom = new V3(cx, y0, cz);
                if (r1 > 0)
                {
                    Vertex(top, new V3(0, 1, 0), 0.5f, 0.5f); Vertex(c, new V3(0, 1, 0), 0.5f + (float)Math.Cos(p1) / 2, 0.5f + (float)Math.Sin(p1) / 2);
                    Vertex(d, new V3(0, 1, 0), 0.5f + (float)Math.Cos(p0) / 2, 0.5f + (float)Math.Sin(p0) / 2);
                }
                if (r0 > 0)
                {
                    Vertex(bottom, new V3(0, -1, 0), 0.5f, 0.5f); Vertex(a, new V3(0, -1, 0), 0.5f + (float)Math.Cos(p0) / 2, 0.5f + (float)Math.Sin(p0) / 2);
                    Vertex(b, new V3(0, -1, 0), 0.5f + (float)Math.Cos(p1) / 2, 0.5f + (float)Math.Sin(p1) / 2);
                }
            }
        }

        /// <summary>Cylinder between two arbitrary points (for legs, posts, staffs).</summary>
        public void Rod(V3 from, V3 to, double r, int slices = 10)
        {
            var d = to - from;
            float len = d.Length;
            if (len < 1e-5f) return;
            var dir = d * (1.0 / len);
            // rotation taking +y to dir
            var axis = V3.Cross(new V3(0, 1, 0), dir);
            double angle = Math.Acos(Math.Max(-1, Math.Min(1, dir.Y)));
            float[] rot = M4.Identity();
            if (axis.Length > 1e-5f) rot = AxisAngle(axis.Normalized(), angle);
            else if (dir.Y < 0) rot = M4.RotX(Math.PI);
            Push(M4.Mul(M4.Translate(from.X, from.Y, from.Z), rot));
            Cylinder(0, 0, 0, r, r, len, slices);
            Pop();
        }

        public void Torus(double cx, double cy, double cz, double R, double r, int slices = 16, int sides = 6)
        {
            // ring lying in the y-z plane (facing +x), e.g. glasses rims
            for (int i = 0; i < slices; i++)
                for (int j = 0; j < sides; j++)
                {
                    var q = new V3[4];
                    var n = new V3[4];
                    int k = 0;
                    foreach (var ij in new[] { new[] { i, j }, new[] { i + 1, j }, new[] { i + 1, j + 1 }, new[] { i, j + 1 } })
                    {
                        double u = 2 * Math.PI * ij[0] / slices, v = 2 * Math.PI * ij[1] / sides;
                        var ringCenter = new V3(0, Math.Cos(u) * R, Math.Sin(u) * R);
                        var nn = new V3(Math.Sin(v), Math.Cos(u) * Math.Cos(v), Math.Sin(u) * Math.Cos(v));
                        q[k] = new V3(cx, cy, cz) + ringCenter + nn * r;
                        n[k] = nn;
                        k++;
                    }
                    Vertex(q[0], n[0], 0, 0); Vertex(q[1], n[1], 1, 0); Vertex(q[2], n[2], 1, 1);
                    Vertex(q[0], n[0], 0, 0); Vertex(q[2], n[2], 1, 1); Vertex(q[3], n[3], 0, 1);
                }
        }

        public static float[] AxisAngle(V3 a, double angle)
        {
            float c = (float)Math.Cos(angle), s = (float)Math.Sin(angle), t = 1 - c;
            return new[]
            {
                t * a.X * a.X + c, t * a.X * a.Y + s * a.Z, t * a.X * a.Z - s * a.Y, 0,
                t * a.X * a.Y - s * a.Z, t * a.Y * a.Y + c, t * a.Y * a.Z + s * a.X, 0,
                t * a.X * a.Z + s * a.Y, t * a.Y * a.Z - s * a.X, t * a.Z * a.Z + c, 0,
                0, 0, 0, 1,
            };
        }
    }
}
