using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace LazyTurtle
{
    /// <summary>
    /// Hardware renderer (OpenGL 2.0 + GLSL 1.10): the school/yard as static vertex buffers with baked
    /// lighting (ceiling lamps, sun shadows, ambient occlusion), 3D models for people and furniture,
    /// billboards for everything without a model, blob shadows, a sky dome outdoors, fog, 4x MSAA
    /// and the HUD bitmap drawn on top. Created with <see cref="TryCreate"/>; returns null when the PC
    /// has no usable OpenGL driver so the game can keep the CPU raycaster.
    /// </summary>
    sealed class GLRenderer : IDisposable
    {
        const double FovY = 62 * Math.PI / 180, Near = 0.05, Far = 400;
        const float SkyRadius = 150;
        const float LampRadius = 6, LampIntensity = 0.5f;
        const float IndoorFogStart = 14, IndoorFogEnd = 52, OutdoorFogStart = 50, OutdoorFogEnd = 150;   // gentle haze, not gloom
        const float IndoorDrawDist = 56, OutdoorDrawDist = 160;     // beyond the fog end, so nothing pops in

        static readonly V3 Up = new V3(0, 1, 0);
        static readonly V3 SunDir = new V3(-0.45, 0.8, 0.35).Normalized();
        static readonly V3 SunCol = new V3(1.0, 0.95, 0.85);
        static readonly V3 SkyAmb = new V3(0.60, 0.63, 0.70);
        static readonly V3 IndoorAmb = new V3(0.50, 0.47, 0.42);    // bright, warm classroom fill light
        static readonly V3 LampCol = new V3(1.0, 0.95, 0.84);
        static readonly V3 LampTileCol = new V3(1.3, 1.27, 1.2);
        static readonly Tex PlainTex = MakePlain();   // stands in for a missing texture

        static Tex MakePlain()
        {
            var t = new Tex(1, 1);
            t.Px[0] = unchecked((int)0xFFC8C8C8);
            return t;
        }

        [DllImport("opengl32.dll")] static extern IntPtr wglGetCurrentContext();

        // ---- context
        IntPtr hwnd, hdc, hglrc;
        bool msaa;
        float maxAniso;

        public string Info { get; private set; }
        public bool VSync { get; private set; }

        // ---- shaders
        uint prog, hudProg;
        int uMVP, uModel, uCam, uTex, uFogColor, uFogStart, uFogEnd, uDynamic, uAmbient, uLightDir, uDiffuse, uSpec, uTint, uAlphaTest, uUVOffset;

        /// <summary>A GL texture plus what the renderer needs to know about its pixels.</summary>
        sealed class GpuTex
        {
            public uint Id;
            public bool Alpha;      // has transparent pixels -> alpha test
            public bool Scroll;     // strongly blue (pond water) -> scrolling uv
        }

        /// <summary>One static vertex buffer drawn with one texture.</summary>
        sealed class GpuPart
        {
            public uint Vbo;
            public int Count;
            public Tex Tex;
            public bool AlphaTest;
            public bool Scroll;     // pond water: uv scrolls with time
        }

        readonly Dictionary<Tex, GpuTex> textures = new Dictionary<Tex, GpuTex>();
        GpuTex white;
        uint quadVbo;               // 0..5 upright unit quad (x -0.5..0.5, y 0..1, facing +z), 6..11 flat quad (x,z -0.5..0.5)
        uint skyVbo;
        int skyDomeCount, skySkirtCount;
        Tex skyTex, blobTex;
        V3 horizon = new V3(0.75, 0.84, 0.95);

        // ---- world cache
        World cachedWorld;
        byte[] cachedWall, cachedFloor;
        bool invalid;
        readonly List<GpuPart> worldParts = new List<GpuPart>();
        readonly Dictionary<string, List<GpuPart>> models = new Dictionary<string, List<GpuPart>>();
        float[] gridR, gridG, gridB, gridSun;   // per-cell light for dynamic objects

        // ---- light baking state (only valid while building)
        World bw;
        readonly List<V3> lamps = new List<V3>();
        V3[] sunDirs;
        List<float[]>[] treeCells;  // per cell: trees (x, z, r^2, height) that may cover it
        float maxOccluder;

        // ---- per-frame scratch (avoid allocations)
        readonly float[] vp = new float[16], mvp = new float[16], model = new float[16], tmp = new float[16], tmp2 = new float[16];
        readonly float[] identity = M4.Identity();
        readonly List<float> shadows = new List<float>();
        uint boundVbo;
        uint hudTexId;
        int hudW, hudH;
        V3 eye, lightDir;
        bool outdoor;

        GLRenderer() { }

        // ================================================================== creation

        /// <summary>
        /// Creates a context on the control's window; returns null (and a reason in info) when OpenGL 2.0
        /// with GLSL is not available (e.g. the "GDI Generic" software driver) so the game falls back to the CPU renderer.
        /// </summary>
        public static GLRenderer TryCreate(Control target, out string info)
        {
            var r = new GLRenderer();
            try
            {
                if (r.Init(target, out info)) return r;
            }
            catch (Exception ex)
            {
                info = "OpenGL init failed: " + ex.Message;
            }
            r.Dispose();
            return null;
        }

        bool Init(Control target, out string info)
        {
            int msaaFormat = FindMsaaFormat();

            hwnd = target.Handle;
            hdc = Wgl.GetDC(hwnd);
            if (hdc == IntPtr.Zero) { info = "GetDC failed"; return false; }

            var pfd = Wgl.Descriptor();
            if (msaaFormat > 0 && Wgl.SetPixelFormat(hdc, msaaFormat, ref pfd)) msaa = true;
            else
            {
                int fmt = Wgl.ChoosePixelFormat(hdc, ref pfd);
                if (fmt == 0 || !Wgl.SetPixelFormat(hdc, fmt, ref pfd)) { info = "SetPixelFormat failed"; return false; }
            }

            hglrc = GL.wglCreateContext(hdc);
            if (hglrc == IntPtr.Zero) { info = "wglCreateContext failed"; return false; }
            if (!GL.wglMakeCurrent(hdc, hglrc)) { info = "wglMakeCurrent failed"; return false; }

            string vendor = GL.GetString(GL.VENDOR), renderer = GL.GetString(GL.RENDERER), version = GL.GetString(GL.VERSION);
            string desc = "OpenGL " + version + " " + renderer + " (" + vendor + ")";
            if (renderer.IndexOf("GDI Generic", StringComparison.OrdinalIgnoreCase) >= 0) { info = desc + ": software driver"; return false; }
            if (ParseVersion(version) < 20) { info = desc + ": OpenGL 2.0 required"; return false; }
            if (!GL.LoadExtensions()) { info = desc + ": OpenGL 2.0 functions missing"; return false; }

            if (GL.SwapInterval != null) VSync = GL.SwapInterval(1);

            string ext = GL.GetString(GL.EXTENSIONS);
            if (ext.Contains("GL_EXT_texture_filter_anisotropic"))
            {
                float max;
                GL.glGetFloatv(GL.MAX_TEXTURE_MAX_ANISOTROPY, out max);
                maxAniso = Math.Min(8, max);
            }

            string err;
            prog = BuildProgram(MainVS, MainFS, out err);
            if (prog == 0) { info = desc + ": shader error: " + err; return false; }
            hudProg = BuildProgram(HudVS, HudFS, out err);
            if (hudProg == 0) { info = desc + ": shader error: " + err; return false; }

            uMVP = GL.GetUniformLocation(prog, "uMVP");
            uModel = GL.GetUniformLocation(prog, "uModel");
            uCam = GL.GetUniformLocation(prog, "uCam");
            uTex = GL.GetUniformLocation(prog, "uTex");
            uFogColor = GL.GetUniformLocation(prog, "uFogColor");
            uFogStart = GL.GetUniformLocation(prog, "uFogStart");
            uFogEnd = GL.GetUniformLocation(prog, "uFogEnd");
            uDynamic = GL.GetUniformLocation(prog, "uDynamic");
            uAmbient = GL.GetUniformLocation(prog, "uAmbient");
            uLightDir = GL.GetUniformLocation(prog, "uLightDir");
            uDiffuse = GL.GetUniformLocation(prog, "uDiffuse");
            uSpec = GL.GetUniformLocation(prog, "uSpec");
            uTint = GL.GetUniformLocation(prog, "uTint");
            uAlphaTest = GL.GetUniformLocation(prog, "uAlphaTest");
            uUVOffset = GL.GetUniformLocation(prog, "uUVOffset");
            GL.UseProgram(prog);
            GL.Uniform1i(uTex, 0);
            GL.UseProgram(hudProg);
            GL.Uniform1i(GL.GetUniformLocation(hudProg, "uTex"), 0);

            for (uint a = 0; a < 4; a++) GL.EnableVertexAttribArray(a);
            GL.glPixelStorei(GL.UNPACK_ALIGNMENT, 4);
            if (msaa) GL.glEnable(GL.MULTISAMPLE);

            white = CreateWhite();
            quadVbo = CreateQuads();
            if (GL.glGetError() != 0) { info = desc + ": OpenGL error during setup"; return false; }

            Info = desc + (msaa ? " MSAA 4x" : "") + (VSync ? " VSync" : "");
            info = Info;
            return true;
        }

        static int ParseVersion(string v)
        {
            int major = 0, minor = 0, i = 0;
            while (i < v.Length && char.IsDigit(v[i])) major = major * 10 + (v[i++] - '0');
            if (i < v.Length && v[i] == '.') { i++; if (i < v.Length && char.IsDigit(v[i])) minor = v[i] - '0'; }
            return major * 10 + minor;
        }

        /// <summary>
        /// Dummy-window trick: a temporary context on a hidden window gives access to wglChoosePixelFormatARB,
        /// which can pick a multisampled format (a window's pixel format can be set only once). 0 = none.
        /// </summary>
        static int FindMsaaFormat()
        {
            IntPtr dc = IntPtr.Zero, rc = IntPtr.Zero, h = IntPtr.Zero;
            Form dummy = null;
            try
            {
                dummy = new Form { ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.None, Size = new Size(16, 16) };
                h = dummy.Handle;
                dc = Wgl.GetDC(h);
                if (dc == IntPtr.Zero) return 0;
                var pfd = Wgl.Descriptor();
                int fmt = Wgl.ChoosePixelFormat(dc, ref pfd);
                if (fmt == 0 || !Wgl.SetPixelFormat(dc, fmt, ref pfd)) return 0;
                rc = GL.wglCreateContext(dc);
                if (rc == IntPtr.Zero || !GL.wglMakeCurrent(dc, rc)) return 0;
                var choose = GL.LoadChoosePixelFormat();
                if (choose == null) return 0;
                int[] attribs =
                {
                    0x2001, 1,          // WGL_DRAW_TO_WINDOW_ARB
                    0x2010, 1,          // WGL_SUPPORT_OPENGL_ARB
                    0x2011, 1,          // WGL_DOUBLE_BUFFER_ARB
                    0x2013, 0x202B,     // WGL_PIXEL_TYPE_ARB = WGL_TYPE_RGBA_ARB
                    0x2014, 32,         // WGL_COLOR_BITS_ARB
                    0x2022, 24,         // WGL_DEPTH_BITS_ARB
                    0x2023, 8,          // WGL_STENCIL_BITS_ARB
                    0x2041, 1,          // WGL_SAMPLE_BUFFERS_ARB
                    0x2042, 4,          // WGL_SAMPLES_ARB
                    0,
                };
                var formats = new int[1];
                uint count;
                if (choose(dc, attribs, new float[] { 0, 0 }, 1, formats, out count) && count > 0) return formats[0];
                return 0;
            }
            catch (Exception)
            {
                return 0;
            }
            finally
            {
                if (rc != IntPtr.Zero)
                {
                    GL.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
                    GL.wglDeleteContext(rc);
                }
                if (dc != IntPtr.Zero) Wgl.ReleaseDC(h, dc);
                if (dummy != null) dummy.Dispose();
            }
        }

        public void Dispose()
        {
            if (hglrc != IntPtr.Zero)
            {
                // deleting the context frees every texture, buffer and program it owns
                GL.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
                GL.wglDeleteContext(hglrc);
                hglrc = IntPtr.Zero;
            }
            if (hdc != IntPtr.Zero)
            {
                Wgl.ReleaseDC(hwnd, hdc);
                hdc = IntPtr.Zero;
            }
            textures.Clear();
            models.Clear();
            worldParts.Clear();
        }

        /// <summary>Forget the cached world mesh and models (e.g. after a language change: signs contain text).</summary>
        public void Invalidate() => invalid = true;

        // ================================================================== shaders

        const string MainVS = @"#version 110
uniform mat4 uMVP;
uniform mat4 uModel;
uniform vec3 uUVOffset;
attribute vec3 aPos;
attribute vec3 aNormal;
attribute vec2 aUV;
attribute vec3 aCol;
varying vec3 vWorld;
varying vec3 vNormal;
varying vec2 vUV;
varying vec3 vCol;
void main()
{
    vec4 w = uModel * vec4(aPos, 1.0);
    vWorld = w.xyz;
    vNormal = (uModel * vec4(aNormal, 0.0)).xyz;
    vUV = aUV + uUVOffset.xy;
    vCol = aCol;
    gl_Position = uMVP * vec4(aPos, 1.0);
}
";

        const string MainFS = @"#version 110
uniform sampler2D uTex;
uniform vec4 uTint;
uniform float uAlphaTest;
uniform float uDynamic;
uniform vec3 uAmbient;
uniform vec3 uLightDir;
uniform float uDiffuse;
uniform float uSpec;
uniform vec3 uCam;
uniform vec3 uFogColor;
uniform float uFogStart;
uniform float uFogEnd;
varying vec3 vWorld;
varying vec3 vNormal;
varying vec2 vUV;
varying vec3 vCol;
void main()
{
    vec4 t = texture2D(uTex, vUV) * uTint;
    if (t.a < uAlphaTest) discard;
    vec3 c = t.rgb * vCol;
    if (uDynamic > 0.5)
    {
        vec3 n = normalize(vNormal);
        vec3 l = normalize(uLightDir);
        vec3 v = normalize(uCam - vWorld);
        float ndl = dot(n, l);
        float spec = 0.0;
        if (ndl > 0.0) spec = pow(max(dot(n, normalize(l + v)), 0.0), 24.0) * uSpec;
        c = c * (uAmbient + uDiffuse * max(ndl, 0.0)) + vec3(spec);
    }
    float x = clamp((length(vWorld - uCam) - uFogStart) / (uFogEnd - uFogStart), 0.0, 1.0);
    float f = 1.0 - exp(-3.0 * x * x);
    gl_FragColor = vec4(mix(c, uFogColor, f), t.a);
}
";

        const string HudVS = @"#version 110
attribute vec3 aPos;
attribute vec2 aUV;
varying vec2 vUV;
void main()
{
    vUV = aUV;
    gl_Position = vec4(aPos.x * 2.0, aPos.y * 2.0 - 1.0, 0.0, 1.0);
}
";

        const string HudFS = @"#version 110
uniform sampler2D uTex;
varying vec2 vUV;
void main()
{
    gl_FragColor = texture2D(uTex, vUV);
}
";

        static uint Compile(uint type, string src, StringBuilder log)
        {
            uint s = GL.CreateShader(type);
            GL.ShaderSource(s, 1, new[] { src }, null);
            GL.CompileShader(s);
            int ok;
            GL.GetShaderiv(s, GL.COMPILE_STATUS, out ok);
            if (ok == 0)
            {
                if (GL.GetShaderInfoLog != null)
                {
                    var sb = new StringBuilder(4096);
                    int len;
                    GL.GetShaderInfoLog(s, sb.Capacity, out len, sb);
                    log.Append(sb.ToString().Trim()).Append(' ');
                }
                return 0;
            }
            return s;
        }

        static uint BuildProgram(string vs, string fs, out string error)
        {
            var log = new StringBuilder();
            uint v = Compile(GL.VERTEX_SHADER, vs, log), f = Compile(GL.FRAGMENT_SHADER, fs, log);
            if (v == 0 || f == 0) { error = log.Length > 0 ? log.ToString() : "compile failed"; return 0; }
            uint p = GL.CreateProgram();
            GL.AttachShader(p, v);
            GL.AttachShader(p, f);
            GL.BindAttribLocation(p, 0, "aPos");
            GL.BindAttribLocation(p, 1, "aNormal");
            GL.BindAttribLocation(p, 2, "aUV");
            GL.BindAttribLocation(p, 3, "aCol");
            GL.LinkProgram(p);
            int ok;
            GL.GetProgramiv(p, GL.LINK_STATUS, out ok);
            if (ok == 0)
            {
                if (GL.GetProgramInfoLog != null)
                {
                    var sb = new StringBuilder(4096);
                    int len;
                    GL.GetProgramInfoLog(p, sb.Capacity, out len, sb);
                    log.Append(sb.ToString().Trim());
                }
                error = log.Length > 0 ? log.ToString() : "link failed";
                return 0;
            }
            error = null;
            return p;
        }

        // ================================================================== buffers and textures

        static uint Upload(List<float> data) => Upload(data.ToArray());

        static uint Upload(float[] data)
        {
            var ids = new uint[1];
            GL.GenBuffers(1, ids);
            GL.BindBuffer(GL.ARRAY_BUFFER, ids[0]);
            GL.BufferData(GL.ARRAY_BUFFER, new IntPtr(data.Length * 4), data, GL.STATIC_DRAW);
            return ids[0];
        }

        static void DeleteVbo(uint id)
        {
            if (id != 0 && GL.DeleteBuffers != null) GL.DeleteBuffers(1, new[] { id });
        }

        void BindVbo(uint vbo)
        {
            if (vbo == boundVbo) return;
            boundVbo = vbo;
            GL.BindBuffer(GL.ARRAY_BUFFER, vbo);
            const int s = MeshBuilder.Stride * 4;
            GL.VertexAttribPointer(0, 3, GL.FLOAT, false, s, IntPtr.Zero);
            GL.VertexAttribPointer(1, 3, GL.FLOAT, false, s, new IntPtr(12));
            GL.VertexAttribPointer(2, 2, GL.FLOAT, false, s, new IntPtr(24));
            GL.VertexAttribPointer(3, 3, GL.FLOAT, false, s, new IntPtr(32));
        }

        static void AddV(List<float> d, V3 p, V3 n, float u, float v, V3 c)
        {
            d.Add(p.X); d.Add(p.Y); d.Add(p.Z);
            d.Add(n.X); d.Add(n.Y); d.Add(n.Z);
            d.Add(u); d.Add(v);
            d.Add(c.X); d.Add(c.Y); d.Add(c.Z);
        }

        static uint CreateQuads()
        {
            var d = new List<float>();
            var wc = new V3(1, 1, 1);
            var fz = new V3(0, 0, 1);
            // upright: bottom at y=0, uv (0,1) bottom-left .. (1,0) top-right (texture row 0 = top)
            AddV(d, new V3(-0.5, 0, 0), fz, 0, 1, wc); AddV(d, new V3(0.5, 0, 0), fz, 1, 1, wc); AddV(d, new V3(0.5, 1, 0), fz, 1, 0, wc);
            AddV(d, new V3(-0.5, 0, 0), fz, 0, 1, wc); AddV(d, new V3(0.5, 1, 0), fz, 1, 0, wc); AddV(d, new V3(-0.5, 1, 0), fz, 0, 0, wc);
            // flat on the floor
            AddV(d, new V3(-0.5, 0, -0.5), Up, 0, 0, wc); AddV(d, new V3(0.5, 0, -0.5), Up, 1, 0, wc); AddV(d, new V3(0.5, 0, 0.5), Up, 1, 1, wc);
            AddV(d, new V3(-0.5, 0, -0.5), Up, 0, 0, wc); AddV(d, new V3(0.5, 0, 0.5), Up, 1, 1, wc); AddV(d, new V3(-0.5, 0, 0.5), Up, 0, 1, wc);
            return Upload(d);
        }

        GpuTex CreateWhite()
        {
            var ids = new uint[1];
            GL.glGenTextures(1, ids);
            GL.glBindTexture(GL.TEXTURE_2D, ids[0]);
            GL.glTexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GL.LINEAR);
            GL.glTexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)GL.LINEAR);
            GL.glTexImage2D(GL.TEXTURE_2D, 0, (int)GL.RGBA, 1, 1, 0, GL.BGRA, GL.UNSIGNED_BYTE, new[] { -1 });
            return new GpuTex { Id = ids[0] };
        }

        /// <summary>GL texture for a Tex (uploaded once, mipmapped, anisotropic). null = 1x1 white.</summary>
        GpuTex GetTex(Tex t)
        {
            if (t == null) return white;
            GpuTex g;
            if (textures.TryGetValue(t, out g)) return g;

            g = new GpuTex();
            long r = 0, gr = 0, b = 0, n = 0;
            foreach (int c in t.Px)
            {
                if ((uint)c >> 24 < 255) { g.Alpha = true; continue; }
                r += (c >> 16) & 255; gr += (c >> 8) & 255; b += c & 255; n++;
            }
            if (n > 0)
            {
                r /= n; gr /= n; b /= n;
                g.Scroll = b > r + 40 && b > gr;
            }
            int[] px = g.Alpha ? Dilate(t) : t.Px;

            var ids = new uint[1];
            GL.glGenTextures(1, ids);
            g.Id = ids[0];
            GL.glBindTexture(GL.TEXTURE_2D, g.Id);
            GL.glTexParameteri(GL.TEXTURE_2D, GL.GENERATE_MIPMAP, 1);
            GL.glTexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GL.LINEAR_MIPMAP_LINEAR);
            GL.glTexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)GL.LINEAR);
            GL.glTexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, (int)GL.REPEAT);
            GL.glTexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, (int)GL.REPEAT);
            if (maxAniso > 1) GL.glTexParameterf(GL.TEXTURE_2D, GL.TEXTURE_MAX_ANISOTROPY, maxAniso);
            GL.glTexImage2D(GL.TEXTURE_2D, 0, (int)GL.RGBA, t.W, t.H, 0, GL.BGRA, GL.UNSIGNED_BYTE, px);
            textures[t] = g;
            return g;
        }

        /// <summary>
        /// Copies the pixels and bleeds the colour of opaque pixels into neighbouring fully transparent ones
        /// (alpha stays 0), so mipmaps and bilinear filtering don't draw dark fringes around sprites.
        /// </summary>
        static int[] Dilate(Tex t)
        {
            int w = t.W, h = t.H;
            var px = (int[])t.Px.Clone();
            var done = new bool[px.Length];
            for (int i = 0; i < px.Length; i++) done[i] = ((uint)px[i] >> 24) != 0;
            var next = new List<int>();
            for (int pass = 0; pass < 4; pass++)
            {
                next.Clear();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (done[i]) continue;
                        int r = 0, g = 0, b = 0, n = 0;
                        for (int k = 0; k < 4; k++)
                        {
                            int nx = x + (k == 0 ? -1 : k == 1 ? 1 : 0), ny = y + (k == 2 ? -1 : k == 3 ? 1 : 0);
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            int j = ny * w + nx;
                            if (!done[j]) continue;
                            int c = px[j];
                            r += (c >> 16) & 255; g += (c >> 8) & 255; b += c & 255; n++;
                        }
                        if (n == 0) continue;
                        px[i] = ((r / n) << 16) | ((g / n) << 8) | (b / n);    // alpha 0
                        next.Add(i);
                    }
                if (next.Count == 0) break;
                foreach (int i in next) done[i] = true;
            }
            return px;
        }

        void BindTex(GpuTex g) => GL.glBindTexture(GL.TEXTURE_2D, g.Id);

        /// <summary>Deletes everything cached for the current world (meshes, models, textures).</summary>
        void ResetCaches()
        {
            foreach (var p in worldParts) DeleteVbo(p.Vbo);
            worldParts.Clear();
            foreach (var list in models.Values)
                if (list != null)
                    foreach (var p in list) DeleteVbo(p.Vbo);
            models.Clear();
            var ids = new List<uint>();
            foreach (var g in textures.Values) ids.Add(g.Id);
            if (ids.Count > 0) GL.glDeleteTextures(ids.Count, ids.ToArray());
            textures.Clear();
            DeleteVbo(skyVbo);
            skyVbo = 0;
            boundVbo = 0;
            cachedWorld = null;
        }

        /// <summary>Model parts as vertex buffers, cached per key; null when the key is unknown.</summary>
        List<GpuPart> GetModel(string key, Art art)
        {
            List<GpuPart> parts;
            if (models.TryGetValue(key, out parts)) return parts;
            var built = Models.Build(key, art);
            if (built != null)
            {
                parts = new List<GpuPart>();
                foreach (var mp in built)
                {
                    if (mp == null || mp.Mesh == null || mp.Mesh.Vertices == 0) continue;
                    parts.Add(new GpuPart { Vbo = Upload(mp.Mesh.Data), Count = mp.Mesh.Vertices, Tex = mp.Tex, AlphaTest = mp.AlphaTest });
                }
                if (parts.Count == 0) parts = null;
            }
            boundVbo = 0;
            models[key] = parts;
            return parts;
        }

        // ================================================================== world mesh and baked light

        bool WorldChanged(World w)
        {
            if (w != cachedWorld || cachedWall == null || cachedWall.Length != w.Wall.Length) return true;
            for (int i = 0; i < w.Wall.Length; i++)
                if (w.Wall[i] != cachedWall[i] || w.Floor[i] != cachedFloor[i]) return true;   // doors opened, stairs unlocked
            return false;
        }

        void BuildWorld(World w)
        {
            ResetCaches();
            cachedWorld = w;
            cachedWall = (byte[])w.Wall.Clone();
            cachedFloor = (byte[])w.Floor.Clone();
            bw = w;
            outdoor = w.Outdoor;
            PrepareLights(w);

            var batches = new Dictionary<Tex, List<float>>();
            Func<Tex, List<float>> batch = t =>
            {
                List<float> l;
                if (t == null) t = PlainTex;
                if (!batches.TryGetValue(t, out l)) batches[t] = l = new List<float>();
                return l;
            };

            var lat = new V3[64];
            var down = new V3(0, -1, 0);
            for (int z = 0; z < w.Ht; z++)
                for (int x = 0; x < w.Wd; x++)
                {
                    int i = z * w.Wd + x;
                    if (w.Wall[i] == 0)
                    {
                        // ---- floor (2x2 subdivision, light on a 3x3 lattice)
                        for (int k = 0; k < 3; k++)
                            for (int j = 0; j < 3; j++)
                            {
                                float px = x + j * 0.5f, pz = z + k * 0.5f;
                                float d = WallDist(px, pz, x, z, -1, -1);
                                float ao = outdoor ? 0.7f + 0.3f * Smooth(d / 0.5f) : 0.5f + 0.5f * Smooth(d / 0.55f);
                                lat[k * 3 + j] = Light(new V3(px, 0, pz), Up, x + 0.5f, z + 0.5f, ao);
                            }
                        EmitGrid(batch(TexOf(w.FloorTex, w.Floor[i])), lat, x, 0, z, Up);

                        // ---- ceiling
                        if (!outdoor)
                        {
                            float h = w.WallHeight(i);
                            bool lamp = w.Ceil[i] == 1;
                            for (int k = 0; k < 3; k++)
                                for (int j = 0; j < 3; j++)
                                {
                                    float px = x + j * 0.5f, pz = z + k * 0.5f;
                                    if (lamp) { lat[k * 3 + j] = LampTileCol; continue; }
                                    float d = WallDist(px, pz, x, z, -1, -1);
                                    float ao = 0.7f + 0.3f * Smooth(d / 0.5f);
                                    lat[k * 3 + j] = Light(new V3(px, h, pz), down, x + 0.5f, z + 0.5f, ao);
                                }
                            EmitGrid(batch(TexOf(w.CeilTex, w.Ceil[i])), lat, x, h, z, down);
                        }
                    }

                    // ---- wall faces seen from this cell (open, or a lower wall outdoors)
                    float bottom = 0;
                    if (w.Wall[i] != 0)
                    {
                        if (!outdoor) continue;
                        bottom = w.WallHeight(i);
                        // flat top of an outdoor wall
                        float th = bottom;
                        for (int k = 0; k < 3; k++)
                            for (int j = 0; j < 3; j++)
                                lat[k * 3 + j] = Light(new V3(x + j * 0.5f, th, z + k * 0.5f), Up, x + 0.5f, z + 0.5f, 1);
                        EmitGrid(batch(w.WallAt(i, World.FaceN)), lat, x, th, z, Up);
                    }
                    if (z > 0) WallFace(batch, lat, x, z, x, z - 1, World.FaceS, bottom);
                    if (z < w.Ht - 1) WallFace(batch, lat, x, z, x, z + 1, World.FaceN, bottom);
                    if (x > 0) WallFace(batch, lat, x, z, x - 1, z, World.FaceE, bottom);
                    if (x < w.Wd - 1) WallFace(batch, lat, x, z, x + 1, z, World.FaceW, bottom);
                }

            // ---- outdoors: endless meadow around the map so nothing floats in the void behind the fences
            if (outdoor && w.FloorTex.Count > 0)
            {
                var d = batch(w.FloorTex[0]);
                var c = SkyAmb * 0.95 + SunCol * (0.8 * SunDir.Y);
                const float far = 160;
                float[][] rects =
                {
                    new[] { -far, -far, w.Wd + far, 0 },
                    new[] { -far, w.Ht, w.Wd + far, w.Ht + far },
                    new[] { -far, 0, 0, w.Ht },
                    new[] { (float)w.Wd, 0, w.Wd + far, w.Ht },
                };
                foreach (var r in rects)
                {
                    // tiles of 16 units keep the uv values small and interpolation accurate
                    for (float z0 = r[1]; z0 < r[3]; z0 += 16)
                        for (float x0 = r[0]; x0 < r[2]; x0 += 16)
                        {
                            float x1 = Math.Min(r[2], x0 + 16), z1 = Math.Min(r[3], z0 + 16);
                            float ox = (float)Math.Floor(x0), oz = (float)Math.Floor(z0);
                            V3 p00 = new V3(x0, -0.002, z0), p10 = new V3(x1, -0.002, z0), p11 = new V3(x1, -0.002, z1), p01 = new V3(x0, -0.002, z1);
                            AddV(d, p00, Up, x0 - ox, z0 - oz, c); AddV(d, p10, Up, x1 - ox, z0 - oz, c); AddV(d, p11, Up, x1 - ox, z1 - oz, c);
                            AddV(d, p00, Up, x0 - ox, z0 - oz, c); AddV(d, p11, Up, x1 - ox, z1 - oz, c); AddV(d, p01, Up, x0 - ox, z1 - oz, c);
                        }
                }
            }

            foreach (var kv in batches)
            {
                if (kv.Value.Count == 0) continue;
                var g = GetTex(kv.Key);
                bool water = outdoor && g.Scroll && w.FloorTex.Contains(kv.Key);
                worldParts.Add(new GpuPart { Vbo = Upload(kv.Value), Count = kv.Value.Count / MeshBuilder.Stride, Tex = kv.Key, AlphaTest = g.Alpha, Scroll = water });
            }

            // ---- per-cell light for dynamic objects
            int cells = w.Wd * w.Ht;
            gridR = new float[cells]; gridG = new float[cells]; gridB = new float[cells]; gridSun = new float[cells];
            for (int z = 0; z < w.Ht; z++)
                for (int x = 0; x < w.Wd; x++)
                {
                    int i = z * w.Wd + x;
                    if (w.Wall[i] != 0) continue;
                    V3 c;
                    float sun = 0;
                    var p = new V3(x + 0.5, 0.5, z + 0.5);
                    if (outdoor)
                    {
                        sun = SunVis(p);
                        c = SkyAmb * 0.9 + SunCol * (0.2 * sun);
                    }
                    else
                    {
                        c = IndoorAmb;
                        foreach (var l in lamps)
                        {
                            float dx = l.X - p.X, dy = l.Y - p.Y, dz = l.Z - p.Z, d2 = dx * dx + dy * dy + dz * dz;
                            if (d2 >= LampRadius * LampRadius) continue;
                            float a = 1 - d2 / (LampRadius * LampRadius);
                            if (!Visible(p.X, p.Z, l.X, l.Z)) continue;
                            c = c + LampCol * (a * a * 0.45);
                        }
                        c = HallTint(c, p.X);
                    }
                    gridR[i] = Math.Min(1.3f, c.X); gridG[i] = Math.Min(1.3f, c.Y); gridB[i] = Math.Min(1.3f, c.Z);
                    gridSun[i] = sun;
                }

            bw = null;
            treeCells = null;
            boundVbo = 0;
        }

        static Tex TexOf(List<Tex> list, byte id) => id < list.Count ? list[id] : (list.Count > 0 ? list[0] : null);

        static float Smooth(float t)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            return t * t * (3 - 2 * t);
        }

        /// <summary>Two triangles per lattice square: a horizontal cell (floor, ceiling, wall top) with uv = map fraction.</summary>
        static void EmitGrid(List<float> d, V3[] lat, int x, float y, int z, V3 n)
        {
            for (int k = 0; k < 2; k++)
                for (int j = 0; j < 2; j++)
                {
                    float u0 = j * 0.5f, u1 = u0 + 0.5f, v0 = k * 0.5f, v1 = v0 + 0.5f;
                    var p00 = new V3(x + u0, y, z + v0);
                    var p10 = new V3(x + u1, y, z + v0);
                    var p11 = new V3(x + u1, y, z + v1);
                    var p01 = new V3(x + u0, y, z + v1);
                    V3 c00 = lat[k * 3 + j], c10 = lat[k * 3 + j + 1], c11 = lat[(k + 1) * 3 + j + 1], c01 = lat[(k + 1) * 3 + j];
                    AddV(d, p00, n, u0, v0, c00); AddV(d, p10, n, u1, v0, c10); AddV(d, p11, n, u1, v1, c11);
                    AddV(d, p00, n, u0, v0, c00); AddV(d, p11, n, u1, v1, c11); AddV(d, p01, n, u0, v1, c01);
                }
        }

        /// <summary>
        /// Face of wall cell (wx,wz) seen from cell (cx,cz), from height 'bottom' up to the wall's top. u grows to the
        /// viewer's right, v = 0 at the top of the wall and 1 at the floor (the texture spans the full wall height).
        /// </summary>
        void WallFace(Func<Tex, List<float>> batch, V3[] lat, int cx, int cz, int wx, int wz, int face, float bottom)
        {
            var w = bw;
            int j = wz * w.Wd + wx;
            if (w.Wall[j] == 0) return;
            float top = w.WallHeight(j);
            if (top <= bottom + 0.001f) return;

            V3 a, b, n;     // a = u 0, b = u 1, n = towards the viewer
            switch (face)
            {
                case World.FaceS: a = new V3(cx, 0, cz); b = new V3(cx + 1, 0, cz); n = new V3(0, 0, 1); break;
                case World.FaceN: a = new V3(cx + 1, 0, cz + 1); b = new V3(cx, 0, cz + 1); n = new V3(0, 0, -1); break;
                case World.FaceE: a = new V3(cx, 0, cz + 1); b = new V3(cx, 0, cz); n = new V3(1, 0, 0); break;
                default: a = new V3(cx + 1, 0, cz); b = new V3(cx + 1, 0, cz + 1); n = new V3(-1, 0, 0); break;
            }
            bool alongX = face == World.FaceS || face == World.FaceN;
            int rows = Math.Max(1, (int)Math.Ceiling((top - bottom) / 0.5f - 0.01f));
            if (rows > 20) rows = 20;
            if (lat.Length < 3 * (rows + 1)) lat = new V3[3 * (rows + 1)];
            float ccx = cx + 0.5f, ccz = cz + 0.5f;

            for (int r = 0; r <= rows; r++)
            {
                float y = top - (top - bottom) * r / rows;
                for (int k = 0; k < 3; k++)
                {
                    float t = k * 0.5f;
                    var p = new V3(a.X + (b.X - a.X) * t, y, a.Z + (b.Z - a.Z) * t);
                    float ao = 1;
                    if (bottom <= 0)
                    {
                        ao = 0.6f + 0.4f * Smooth(y / 0.45f);
                        if (!outdoor) ao *= 0.75f + 0.25f * Smooth((top - y) / 0.35f);
                        // inside corners: perpendicular walls next to this face
                        float d = WallDist(p.X + n.X * 0.02f, p.Z + n.Z * 0.02f, cx, cz, alongX ? -1 : wx, alongX ? wz : -1);
                        ao *= 0.65f + 0.35f * Smooth(d / 0.5f);
                    }
                    lat[r * 3 + k] = Light(p, n, ccx, ccz, ao);
                }
            }

            var d2 = batch(w.WallAt(j, face));
            for (int r = 0; r < rows; r++)
            {
                float yt = top - (top - bottom) * r / rows, yb = top - (top - bottom) * (r + 1) / rows;
                float vt = (top - yt) / top, vb = (top - yb) / top;
                for (int k = 0; k < 2; k++)
                {
                    float t0 = k * 0.5f, t1 = t0 + 0.5f;
                    var q0 = new V3(a.X + (b.X - a.X) * t0, 0, a.Z + (b.Z - a.Z) * t0);
                    var q1 = new V3(a.X + (b.X - a.X) * t1, 0, a.Z + (b.Z - a.Z) * t1);
                    var pt0 = new V3(q0.X, yt, q0.Z); var pt1 = new V3(q1.X, yt, q1.Z);
                    var pb0 = new V3(q0.X, yb, q0.Z); var pb1 = new V3(q1.X, yb, q1.Z);
                    V3 ct0 = lat[r * 3 + k], ct1 = lat[r * 3 + k + 1], cb0 = lat[(r + 1) * 3 + k], cb1 = lat[(r + 1) * 3 + k + 1];
                    AddV(d2, pb0, n, t0, vb, cb0); AddV(d2, pb1, n, t1, vb, cb1); AddV(d2, pt1, n, t1, vt, ct1);
                    AddV(d2, pb0, n, t0, vb, cb0); AddV(d2, pt1, n, t1, vt, ct1); AddV(d2, pt0, n, t0, vt, ct0);
                }
            }
        }

        /// <summary>
        /// Distance (in the floor plane) from a point in cell (cx,cz) to the nearest wall among the 8 neighbours.
        /// exX/exZ (>= 0) skip the cells in that column/row (the wall a face belongs to).
        /// </summary>
        float WallDist(float px, float pz, int cx, int cz, int exX, int exZ)
        {
            var w = bw;
            float best = 9;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int x = cx + dx, z = cz + dz;
                    if (x == exX || z == exZ) continue;
                    if (w.InBounds(x, z) && w.Wall[z * w.Wd + x] == 0) continue;
                    float ex = Math.Max(0, Math.Max(x - px, px - (x + 1)));
                    float ez = Math.Max(0, Math.Max(z - pz, pz - (z + 1)));
                    float d = (float)Math.Sqrt(ex * ex + ez * ez);
                    if (d < best) best = d;
                }
            return best;
        }

        void PrepareLights(World w)
        {
            lamps.Clear();
            maxOccluder = 0;
            treeCells = null;
            if (!w.Outdoor)
            {
                for (int z = 0; z < w.Ht; z++)
                    for (int x = 0; x < w.Wd; x++)
                    {
                        int i = z * w.Wd + x;
                        if (w.Wall[i] == 0 && w.Ceil[i] == 1) lamps.Add(new V3(x + 0.5, w.WallHeight(i) - 0.1, z + 0.5));
                    }
                return;
            }

            // three slightly jittered sun directions give soft shadow edges
            sunDirs = new V3[3];
            double az = Math.Atan2(SunDir.Z, SunDir.X), el = Math.Asin(SunDir.Y);
            double[] jA = { 0, 0.05, -0.04 }, jE = { 0, -0.03, 0.04 };
            for (int k = 0; k < 3; k++)
            {
                double a = az + jA[k], e = el + jE[k];
                sunDirs[k] = new V3(Math.Cos(e) * Math.Cos(a), Math.Sin(e), Math.Cos(e) * Math.Sin(a));
            }

            for (int i = 0; i < w.Wall.Length; i++)
                if (w.Wall[i] != 0) maxOccluder = Math.Max(maxOccluder, w.WallHeight(i));

            treeCells = new List<float[]>[w.Wd * w.Ht];
            foreach (var e in w.Entities)
            {
                if (!e.Alive || e.Model == null || !e.Model.StartsWith("tree", StringComparison.Ordinal)) continue;
                float s = (float)(e.Scale > 0 ? e.Scale : 1), r = 1.0f * s, h = 3.2f * s;
                maxOccluder = Math.Max(maxOccluder, h);
                var tree = new[] { (float)e.X, (float)e.Y, r * r, h };
                for (int z = (int)Math.Floor(e.Y - r); z <= (int)Math.Floor(e.Y + r); z++)
                    for (int x = (int)Math.Floor(e.X - r); x <= (int)Math.Floor(e.X + r); x++)
                    {
                        if (!w.InBounds(x, z)) continue;
                        int i = z * w.Wd + x;
                        if (treeCells[i] == null) treeCells[i] = new List<float[]>();
                        treeCells[i].Add(tree);
                    }
            }
            maxOccluder += 0.1f;
        }

        /// <summary>Baked light at a surface point: ambient x AO plus lamps (indoor) or sun with shadows (outdoor).</summary>
        V3 Light(V3 p, V3 n, float ccx, float ccz, float ao)
        {
            // probe slightly inside the open cell and off the surface so grid rays don't start in a wall
            var q = new V3(p.X + (ccx - p.X) * 0.04f + n.X * 0.03f, p.Y + n.Y * 0.03f, p.Z + (ccz - p.Z) * 0.04f + n.Z * 0.03f);
            V3 c;
            if (outdoor)
            {
                float ndl = V3.Dot(n, SunDir), sun = 0;
                if (ndl > 0) sun = ndl * SunVis(q);
                c = SkyAmb * (ao * (0.8f + 0.2f * n.Y)) + SunCol * (0.8f * sun);
            }
            else
            {
                c = IndoorAmb * ao;
                const float r2 = LampRadius * LampRadius;
                foreach (var l in lamps)
                {
                    float dx = l.X - q.X, dy = l.Y - q.Y, dz = l.Z - q.Z, d2 = dx * dx + dy * dy + dz * dz;
                    if (d2 >= r2) continue;
                    float d = (float)Math.Sqrt(d2) + 1e-4f;
                    float ndl = (n.X * dx + n.Y * dy + n.Z * dz) / d;
                    float a = 1 - d2 / r2;
                    a *= a;
                    float glow = 0;
                    if (n.Y < -0.5f && d < 3) { float g = 1 - d * d / 9; glow = 0.35f * g * g; }   // ceiling near a lamp
                    float wrap = (ndl + 0.2f) / 1.2f;
                    if (wrap <= 0 && glow <= 0) continue;
                    if (!Visible(q.X, q.Z, l.X, l.Z)) continue;
                    c = c + LampCol * (LampIntensity * a * Math.Max(0, wrap) * (0.6f + 0.4f * ao) + glow);
                }
                c = HallTint(c, p.X);
            }
            return new V3(Math.Min(1.35f, c.X), Math.Min(1.35f, c.Y), Math.Min(1.35f, c.Z));
        }

        /// <summary>Warm golden light in the Master Turtle's hall (east end of every school floor).</summary>
        V3 HallTint(V3 c, float x)
        {
            if (outdoor) return c;
            float t = Smooth((x - 41) / 2);
            if (t <= 0) return c;
            return new V3(c.X * (1 + 0.08f * t), c.Y * (1 - 0.03f * t), c.Z * (1 - 0.18f * t));
        }

        /// <summary>True when the straight line between two floor points crosses no wall cell (lamp visibility).</summary>
        bool Visible(float ax, float az, float bx, float bz)
        {
            var w = bw;
            float dx = bx - ax, dz = bz - az;
            int steps = (int)(Math.Sqrt(dx * dx + dz * dz) / 0.2) + 1;
            for (int s = 1; s < steps; s++)
            {
                float t = (float)s / steps;
                int x = (int)Math.Floor(ax + dx * t), z = (int)Math.Floor(az + dz * t);
                if (!w.InBounds(x, z) || w.Wall[z * w.Wd + x] != 0) return false;
            }
            return true;
        }

        /// <summary>Fraction (0..1) of the three sun rays from p that reach the sky past walls and trees.</summary>
        float SunVis(V3 p)
        {
            var w = bw;
            int vis = 0;
            foreach (var dir in sunDirs)
            {
                bool hit = false;
                for (float t = 0.06f; ; t += 0.12f)
                {
                    float x = p.X + dir.X * t, y = p.Y + dir.Y * t, z = p.Z + dir.Z * t;
                    if (y > maxOccluder) break;
                    int cx = (int)Math.Floor(x), cz = (int)Math.Floor(z);
                    if (!w.InBounds(cx, cz)) break;
                    int i = cz * w.Wd + cx;
                    if (w.Wall[i] != 0 && y < w.WallHeight(i)) { hit = true; break; }
                    var trees = treeCells[i];
                    if (trees != null)
                        foreach (var tr in trees)
                        {
                            float dx = x - tr[0], dz = z - tr[1];
                            if (y < tr[3] && dx * dx + dz * dz < tr[2]) { hit = true; break; }
                        }
                    if (hit) break;
                }
                if (!hit) vis++;
            }
            return vis / 3f;
        }

        /// <summary>Light for a dynamic object at map (x,y): bilinear over the open cells' samples.</summary>
        V3 AmbientAt(World w, double x, double y, out float sun)
        {
            double fx = x - 0.5, fy = y - 0.5;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            float tx = (float)(fx - x0), ty = (float)(fy - y0);
            float r = 0, g = 0, b = 0, s = 0, wsum = 0;
            for (int k = 0; k < 4; k++)
            {
                int cx = x0 + (k & 1), cy = y0 + (k >> 1);
                if (!w.InBounds(cx, cy)) continue;
                int i = cy * w.Wd + cx;
                if (w.Wall[i] != 0 || i >= gridR.Length) continue;
                float wt = ((k & 1) != 0 ? tx : 1 - tx) * ((k >> 1) != 0 ? ty : 1 - ty) + 1e-4f;
                r += gridR[i] * wt; g += gridG[i] * wt; b += gridB[i] * wt; s += gridSun[i] * wt; wsum += wt;
            }
            if (wsum <= 0)
            {
                sun = outdoor ? 1 : 0;
                return outdoor ? SkyAmb : new V3(0.8, 0.78, 0.74);
            }
            sun = s / wsum;
            return new V3(r / wsum, g / wsum, b / wsum);
        }

        // ================================================================== sky

        void BuildSky()
        {
            if (skyTex == null) skyTex = Art.SkyDome();
            if (skyTex != null)
            {
                // horizon colour = average of the bottom row of the panorama
                long r = 0, g = 0, b = 0;
                int o = (skyTex.H - 1) * skyTex.W;
                for (int x = 0; x < skyTex.W; x++)
                {
                    int c = skyTex.Px[o + x];
                    r += (c >> 16) & 255; g += (c >> 8) & 255; b += c & 255;
                }
                horizon = new V3(r / (255.0 * skyTex.W), g / (255.0 * skyTex.W), b / (255.0 * skyTex.W));
            }

            const int slices = 48, stacks = 16;
            var d = new List<float>();
            var wc = new V3(1, 1, 1);
            Func<double, double, V3> dir = (a, e) => new V3(Math.Cos(e) * Math.Cos(a), Math.Sin(e), Math.Cos(e) * Math.Sin(a));
            for (int i = 0; i < stacks; i++)
            {
                float v0 = (float)i / stacks, v1 = (float)(i + 1) / stacks;
                double e0 = (1 - v0) * Math.PI / 2, e1 = (1 - v1) * Math.PI / 2;
                for (int j = 0; j < slices; j++)
                {
                    float u0 = (float)j / slices, u1 = (float)(j + 1) / slices;
                    double a0 = u0 * Math.PI * 2, a1 = u1 * Math.PI * 2;
                    V3 p00 = dir(a0, e0) * SkyRadius, p10 = dir(a1, e0) * SkyRadius, p11 = dir(a1, e1) * SkyRadius, p01 = dir(a0, e1) * SkyRadius;
                    AddV(d, p00, Up, u0, v0, wc); AddV(d, p10, Up, u1, v0, wc); AddV(d, p11, Up, u1, v1, wc);
                    AddV(d, p00, Up, u0, v0, wc); AddV(d, p11, Up, u1, v1, wc); AddV(d, p01, Up, u0, v1, wc);
                }
            }
            skyDomeCount = d.Count / MeshBuilder.Stride;
            // skirt below the horizon in the horizon colour (white texture)
            for (int j = 0; j < slices; j++)
            {
                double a0 = Math.PI * 2 * j / slices, a1 = Math.PI * 2 * (j + 1) / slices, e1 = -0.6;
                V3 p00 = dir(a0, 0) * SkyRadius, p10 = dir(a1, 0) * SkyRadius, p11 = dir(a1, e1) * SkyRadius, p01 = dir(a0, e1) * SkyRadius;
                AddV(d, p00, Up, 0, 0, horizon); AddV(d, p10, Up, 0, 0, horizon); AddV(d, p11, Up, 0, 0, horizon);
                AddV(d, p00, Up, 0, 0, horizon); AddV(d, p11, Up, 0, 0, horizon); AddV(d, p01, Up, 0, 0, horizon);
            }
            skySkirtCount = d.Count / MeshBuilder.Stride - skyDomeCount;
            skyVbo = Upload(d);
            boundVbo = 0;
        }

        // ================================================================== frame

        /// <summary>
        /// Draws one frame and swaps buffers. hud = premultiplied-alpha ARGB bitmap (PixelFormat.Format32bppPArgb)
        /// of exactly width x height, or null.
        /// </summary>
        public void Render(Game game, int width, int height, Bitmap hud)
        {
            if (hglrc == IntPtr.Zero || width <= 0 || height <= 0) return;
            if (wglGetCurrentContext() != hglrc) GL.wglMakeCurrent(hdc, hglrc);

            var w = game.World;
            if (invalid) { ResetCaches(); invalid = false; }
            if (w != null && WorldChanged(w)) BuildWorld(w);
            outdoor = w != null && w.Outdoor;
            if (outdoor && skyVbo == 0) BuildSky();

            V3 fog;
            if (outdoor) fog = horizon * 0.7 + new V3(0.85, 0.9, 0.95) * 0.3;
            else if (w != null && w.Theme != null) fog = new V3(w.Theme.Fog.R / 255.0, w.Theme.Fog.G / 255.0, w.Theme.Fog.B / 255.0);
            else fog = new V3(0.2, 0.2, 0.22);
            V3 clear = outdoor ? horizon : fog;

            GL.glViewport(0, 0, width, height);
            GL.glDepthMask(true);
            GL.glClearColor(clear.X, clear.Y, clear.Z, 1);
            GL.glClear(GL.COLOR_BUFFER_BIT | GL.DEPTH_BUFFER_BIT);
            GL.glDisable(GL.CULL_FACE);
            GL.glDisable(GL.BLEND);
            GL.glDisable(GL.POLYGON_OFFSET_FILL);
            GL.glEnable(GL.DEPTH_TEST);
            GL.glDepthFunc(GL.LEQUAL);
            boundVbo = 0;

            if (w != null)
            {
                eye = game.CamEye;
                var target = game.CamTarget;
                if ((target - eye).Length < 1e-4f) target = eye + new V3(1, 0, 0);
                var proj = M4.Perspective(FovY, (double)width / height, Near, Far);
                var view = M4.LookAt(eye, target, Up);
                MulInto(proj, view, vp);

                // indoor key light for models: from above and slightly from the camera side
                var back = new V3(eye.X - target.X, 0, eye.Z - target.Z).Normalized();
                lightDir = outdoor ? SunDir : (back * 0.6 + new V3(0, 1, 0)).Normalized();

                GL.UseProgram(prog);
                GL.Uniform3f(uCam, eye.X, eye.Y, eye.Z);
                GL.Uniform3f(uFogColor, fog.X, fog.Y, fog.Z);
                GL.Uniform3f(uLightDir, lightDir.X, lightDir.Y, lightDir.Z);
                GL.Uniform4f(uTint, 1, 1, 1, 1);
                GL.Uniform3f(uUVOffset, 0, 0, 0);
                GL.Uniform1f(uSpec, 0);

                if (outdoor) DrawSky();
                SetFog(outdoor ? OutdoorFogStart : IndoorFogStart, outdoor ? OutdoorFogEnd : IndoorFogEnd);
                DrawWorld(game.Time);
                DrawEntities(game, w);
                DrawShadows();
            }

            if (hud != null) DrawHud(hud);
            Wgl.SwapBuffers(hdc);
        }

        void SetFog(float start, float end)
        {
            GL.Uniform1f(uFogStart, start);
            GL.Uniform1f(uFogEnd, end);
        }

        static void MulInto(float[] a, float[] b, float[] r)
        {
            for (int c = 0; c < 4; c++)
                for (int row = 0; row < 4; row++)
                    r[c * 4 + row] = a[row] * b[c * 4] + a[4 + row] * b[c * 4 + 1] + a[8 + row] * b[c * 4 + 2] + a[12 + row] * b[c * 4 + 3];
        }

        /// <summary>m = T(x,y,z) * RotY(angle) * S(sx,sy,sz).</summary>
        static void Trs(float[] m, double x, double y, double z, double angle, double sx, double sy, double sz)
        {
            float c = (float)Math.Cos(angle), s = (float)Math.Sin(angle);
            m[0] = c * (float)sx; m[1] = 0; m[2] = -s * (float)sx; m[3] = 0;
            m[4] = 0; m[5] = (float)sy; m[6] = 0; m[7] = 0;
            m[8] = s * (float)sz; m[9] = 0; m[10] = c * (float)sz; m[11] = 0;
            m[12] = (float)x; m[13] = (float)y; m[14] = (float)z; m[15] = 1;
        }

        void SetModel(float[] m)
        {
            MulInto(vp, m, mvp);
            GL.UniformMatrix4fv(uMVP, 1, false, mvp);
            GL.UniformMatrix4fv(uModel, 1, false, m);
        }

        void DrawSky()
        {
            GL.glDisable(GL.DEPTH_TEST);
            GL.glDepthMask(false);
            SetFog(1e6f, 2e6f);
            GL.Uniform1f(uDynamic, 0);
            GL.Uniform1f(uAlphaTest, 0);
            Trs(model, eye.X, eye.Y, eye.Z, 0, 1, 1, 1);
            SetModel(model);
            BindVbo(skyVbo);
            BindTex(skyTex != null ? GetTex(skyTex) : white);
            GL.glDrawArrays(GL.TRIANGLES, 0, skyDomeCount);
            BindTex(white);
            GL.glDrawArrays(GL.TRIANGLES, skyDomeCount, skySkirtCount);
            GL.glDepthMask(true);
            GL.glEnable(GL.DEPTH_TEST);
        }

        void DrawWorld(double time)
        {
            GL.Uniform1f(uDynamic, 0);
            SetModel(identity);
            foreach (var p in worldParts)
            {
                var g = GetTex(p.Tex);
                BindTex(g);
                GL.Uniform1f(uAlphaTest, p.AlphaTest ? 0.5f : 0);
                if (p.Scroll) GL.Uniform3f(uUVOffset, (float)(time * 0.05 % 1), (float)(time * 0.03 % 1), 0);
                BindVbo(p.Vbo);
                GL.glDrawArrays(GL.TRIANGLES, 0, p.Count);
                if (p.Scroll) GL.Uniform3f(uUVOffset, 0, 0, 0);
            }
        }

        void SetDynamicLight(World w, double x, double y)
        {
            float sun;
            var a = AmbientAt(w, x, y, out sun);
            if (outdoor)
            {
                GL.Uniform3f(uAmbient, a.X, a.Y, a.Z);
                GL.Uniform1f(uDiffuse, 0.15f + 0.65f * sun);
                GL.Uniform1f(uSpec, 0.15f * (0.3f + 0.7f * sun));
            }
            else
            {
                GL.Uniform3f(uAmbient, a.X * 0.75f, a.Y * 0.75f, a.Z * 0.75f);
                GL.Uniform1f(uDiffuse, 0.35f);
                GL.Uniform1f(uSpec, 0.15f);
            }
        }

        void DrawParts(List<GpuPart> parts, float[] m)
        {
            SetModel(m);
            foreach (var p in parts)
            {
                var g = GetTex(p.Tex);
                BindTex(g);
                GL.Uniform1f(uAlphaTest, p.AlphaTest || g.Alpha ? 0.5f : 0);
                BindVbo(p.Vbo);
                GL.glDrawArrays(GL.TRIANGLES, 0, p.Count);
            }
        }

        void QueueShadow(double x, double z, double size, double alpha)
        {
            shadows.Add((float)x); shadows.Add((float)z); shadows.Add((float)size); shadows.Add((float)alpha);
        }

        void DrawEntities(Game game, World w)
        {
            double time = game.Time;
            var art = game.Art;
            float maxD = outdoor ? OutdoorDrawDist : IndoorDrawDist;
            shadows.Clear();
            GL.Uniform1f(uDynamic, 1);
            bool playerModel = GetModel("player", art) != null;

            foreach (var e in w.Entities)
            {
                // the player's own sprite entity is only drawn when there is no turtle model
                if (!e.Alive || (e.Kind == Kind.Player && playerModel)) continue;
                double dx = e.X - eye.X, dz = e.Y - eye.Z;
                if (dx * dx + dz * dz > maxD * maxD) continue;

                var parts = e.Model != null ? GetModel(e.Model, art) : null;
                double bob = e.Bob ? 0.03 + 0.03 * Math.Sin(time * 3 + e.Phase) : 0;
                double scale = e.Scale > 0 ? e.Scale : 1;
                if (parts != null)
                {
                    SetDynamicLight(w, e.X, e.Y);
                    GL.Uniform1f(uDynamic, 1);
                    double sy = scale;
                    switch (e.Kind)
                    {
                        case Kind.Coin:
                            Trs(model, e.X, e.Z + bob, e.Y, -(time * 3 + e.Phase), scale, scale, scale);
                            break;
                        case Kind.Ball:
                            Trs(tmp, e.X, e.Z + 0.13, e.Y, -e.Yaw, 1, 1, 1);
                            var roll = M4.Mul(M4.RotZ(-e.Phase), M4.Scale((float)scale, (float)scale, (float)scale));
                            MulInto(tmp, roll, model);
                            break;
                        case Kind.Teacher:
                        case Kind.Master:
                        case Kind.Kid:
                            sy = scale * (1 + 0.015 * Math.Sin(time * 2 + e.Phase));
                            if (e.Kind == Kind.Kid && e.AnimRate > 0) bob += 0.035 * Math.Abs(Math.Sin(time * e.AnimRate * 1.5 + e.Phase));
                            Trs(model, e.X, e.Z + bob, e.Y, -e.Yaw, scale, sy, scale);
                            break;
                        default:
                            Trs(model, e.X, e.Z + bob, e.Y, -e.Yaw, scale, scale, scale);
                            break;
                    }
                    DrawParts(parts, model);
                    if (e.Model == "peacock" && e.Variant == 1)
                    {
                        var fan = GetModel("peacockfan", art);
                        if (fan != null) DrawParts(fan, model);
                    }
                }
                else if (e.Frames != null && e.Frames.Length > 0)
                    DrawBillboard(w, e, time, bob);
                else continue;

                switch (e.Kind)
                {
                    case Kind.Teacher: QueueShadow(e.X, e.Y, 0.75 * scale, 0.55); break;
                    case Kind.Master: QueueShadow(e.X, e.Y, 0.95 * scale, 0.55); break;
                    case Kind.Kid: QueueShadow(e.X, e.Y, 0.55 * scale, 0.55); break;
                    case Kind.Animal: QueueShadow(e.X, e.Y, 0.6 * scale, 0.5); break;
                    case Kind.Ball:
                        double hb = Math.Min(1.5, Math.Max(0, e.Z));
                        QueueShadow(e.X, e.Y, 0.3 * scale * (1 - hb * 0.35), 0.55 * (1 - hb * 0.45));
                        break;
                }
            }

            DrawPlayer(game, w);
        }

        void DrawBillboard(World w, Entity e, double time, double bob)
        {
            var frames = e.Frames;
            int fi = frames.Length == 1 ? 0 : (int)(time * e.AnimRate + e.Phase * 10) % frames.Length;
            if (fi < 0) fi += frames.Length;
            var t = frames[fi];
            if (t == null) return;

            double angle = Math.Atan2(eye.X - e.X, eye.Z - e.Y);   // quad normal (+z) towards the camera
            double sx = e.Kind == Kind.Kid && e.Dir < 0 ? -e.W : e.W;
            Trs(model, e.X, e.Z + bob, e.Y, angle, sx, e.H, 1);

            GL.Uniform1f(uDynamic, 1);
            GL.Uniform1f(uDiffuse, 0);
            GL.Uniform1f(uSpec, 0);
            if (e.Kind == Kind.Sparkle || e.Kind == Kind.Marker || e.Kind == Kind.Coin) GL.Uniform3f(uAmbient, 1, 1, 1);
            else
            {
                float sun;
                var a = AmbientAt(w, e.X, e.Y, out sun);
                if (outdoor) a = a + SunCol * (0.35 * sun);
                GL.Uniform3f(uAmbient, Math.Min(1.15f, a.X), Math.Min(1.15f, a.Y), Math.Min(1.15f, a.Z));
            }
            SetModel(model);
            BindTex(GetTex(t));
            GL.Uniform1f(uAlphaTest, 0.5f);
            BindVbo(quadVbo);
            GL.glDrawArrays(GL.TRIANGLES, 0, 6);
        }

        void DrawPlayer(Game game, World w)
        {
            var p = game.P;
            var body = GetModel("player", game.Art);
            if (body == null) return;
            var center = new V3(p.X, p.JumpZ + 0.3, p.Y);
            if ((center - eye).Length < 0.6f) return;

            SetDynamicLight(w, p.X, p.Y);
            GL.Uniform1f(uDynamic, 1);
            Trs(tmp, p.X, p.JumpZ, p.Y, -p.Angle, 1, 1, 1);
            DrawParts(body, tmp);

            var leg = GetModel("leg", game.Art);
            if (leg != null)
            {
                double swing = p.Moving ? Math.Sin(p.Bob) * 0.06 : 0;
                bool jumping = p.JumpZ > 0;
                double spreadX = jumping ? 1.18 : 1, spreadZ = jumping ? 1.25 : 1;
                for (int k = 0; k < 4; k++)
                {
                    double lx = (k & 1) == 0 ? 0.19 : -0.19, lz = (k & 2) == 0 ? 0.18 : -0.18;
                    double sgn = (lx > 0) == (lz > 0) ? 1 : -1;     // diagonal pairs swing together
                    Trs(tmp2, lx * spreadX + swing * sgn, 0.07 - (jumping ? 0.02 : 0), lz * spreadZ, 0, 1, 1, 1);
                    MulInto(tmp, tmp2, model);
                    DrawParts(leg, model);
                }
            }
            double h = Math.Min(1, p.JumpZ);
            QueueShadow(p.X, p.Y, 0.7 * (1 - h * 0.3), 0.6 * (1 - h * 0.4));
        }

        void DrawShadows()
        {
            if (shadows.Count == 0) return;
            if (blobTex == null) blobTex = Art.BlobShadow();
            if (blobTex == null) return;
            GL.glEnable(GL.BLEND);
            GL.glBlendFunc(GL.SRC_ALPHA, GL.ONE_MINUS_SRC_ALPHA);
            GL.glDepthMask(false);
            GL.glEnable(GL.POLYGON_OFFSET_FILL);
            GL.glPolygonOffset(-1, -4);
            GL.Uniform1f(uDynamic, 0);
            GL.Uniform1f(uAlphaTest, 0);
            BindTex(GetTex(blobTex));
            BindVbo(quadVbo);
            for (int i = 0; i < shadows.Count; i += 4)
            {
                GL.Uniform4f(uTint, 1, 1, 1, shadows[i + 3]);
                Trs(model, shadows[i], 0.004, shadows[i + 1], 0, shadows[i + 2], 1, shadows[i + 2]);
                SetModel(model);
                GL.glDrawArrays(GL.TRIANGLES, 6, 6);
            }
            GL.Uniform4f(uTint, 1, 1, 1, 1);
            GL.glDisable(GL.POLYGON_OFFSET_FILL);
            GL.glDepthMask(true);
            GL.glDisable(GL.BLEND);
        }

        void DrawHud(Bitmap hud)
        {
            int w = hud.Width, h = hud.Height;
            if (w <= 0 || h <= 0) return;
            if (hudTexId == 0)
            {
                var ids = new uint[1];
                GL.glGenTextures(1, ids);
                hudTexId = ids[0];
                GL.glBindTexture(GL.TEXTURE_2D, hudTexId);
                GL.glTexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, (int)GL.LINEAR);
                GL.glTexParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)GL.LINEAR);
                GL.glTexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, (int)GL.CLAMP_TO_EDGE);
                GL.glTexParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, (int)GL.CLAMP_TO_EDGE);
                hudW = hudH = 0;
            }
            else GL.glBindTexture(GL.TEXTURE_2D, hudTexId);

            var data = hud.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                if (w != hudW || h != hudH)
                {
                    GL.glTexImage2D(GL.TEXTURE_2D, 0, (int)GL.RGBA, w, h, 0, GL.BGRA, GL.UNSIGNED_BYTE, data.Scan0);
                    hudW = w;
                    hudH = h;
                }
                else GL.glTexSubImage2D(GL.TEXTURE_2D, 0, 0, 0, w, h, GL.BGRA, GL.UNSIGNED_BYTE, data.Scan0);
            }
            finally
            {
                hud.UnlockBits(data);
            }

            GL.glDisable(GL.DEPTH_TEST);
            GL.glDepthMask(false);
            GL.glEnable(GL.BLEND);
            GL.glBlendFunc(GL.ONE, GL.ONE_MINUS_SRC_ALPHA);
            GL.UseProgram(hudProg);
            BindVbo(quadVbo);
            GL.glDrawArrays(GL.TRIANGLES, 0, 6);
            GL.glDisable(GL.BLEND);
            GL.glDepthMask(true);
            GL.glEnable(GL.DEPTH_TEST);
        }
    }
}
