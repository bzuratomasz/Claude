using System;
using System.Runtime.InteropServices;
using System.Text;

namespace LazyTurtle
{
    /// <summary>
    /// Minimal OpenGL 2.1 binding via opengl32.dll (present on every Windows since 95), so the game
    /// needs no extra libraries. OpenGL 1.1 functions are exported directly; 2.x functions are
    /// loaded through wglGetProcAddress.
    /// </summary>
    static class GL
    {
        const string Lib = "opengl32.dll";

        public const uint COLOR_BUFFER_BIT = 0x4000, DEPTH_BUFFER_BIT = 0x0100;
        public const uint DEPTH_TEST = 0x0B71, CULL_FACE = 0x0B44, BLEND = 0x0BE2, TEXTURE_2D = 0x0DE1, MULTISAMPLE = 0x809D, POLYGON_OFFSET_FILL = 0x8037;
        public const uint SRC_ALPHA = 0x0302, ONE_MINUS_SRC_ALPHA = 0x0303, ONE = 1;
        public const uint TRIANGLES = 4, FLOAT = 0x1406, UNSIGNED_BYTE = 0x1401;
        public const uint RGBA = 0x1908, BGRA = 0x80E1;
        public const uint TEXTURE_MIN_FILTER = 0x2801, TEXTURE_MAG_FILTER = 0x2800, LINEAR = 0x2601, LINEAR_MIPMAP_LINEAR = 0x2703;
        public const uint TEXTURE_WRAP_S = 0x2802, TEXTURE_WRAP_T = 0x2803, REPEAT = 0x2901, CLAMP_TO_EDGE = 0x812F;
        public const uint GENERATE_MIPMAP = 0x8191, TEXTURE_MAX_ANISOTROPY = 0x84FE, MAX_TEXTURE_MAX_ANISOTROPY = 0x84FF;
        public const uint ARRAY_BUFFER = 0x8892, STATIC_DRAW = 0x88E4, STREAM_DRAW = 0x88E0;
        public const uint VERTEX_SHADER = 0x8B31, FRAGMENT_SHADER = 0x8B30, COMPILE_STATUS = 0x8B81, LINK_STATUS = 0x8B82;
        public const uint LEQUAL = 0x0203, BACK = 0x0405;
        public const uint VENDOR = 0x1F00, RENDERER = 0x1F01, VERSION = 0x1F02, EXTENSIONS = 0x1F03;
        public const uint UNPACK_ALIGNMENT = 0x0CF5;

        [DllImport(Lib)] public static extern void glViewport(int x, int y, int w, int h);
        [DllImport(Lib)] public static extern void glClear(uint mask);
        [DllImport(Lib)] public static extern void glClearColor(float r, float g, float b, float a);
        [DllImport(Lib)] public static extern void glEnable(uint cap);
        [DllImport(Lib)] public static extern void glDisable(uint cap);
        [DllImport(Lib)] public static extern void glBlendFunc(uint s, uint d);
        [DllImport(Lib)] public static extern void glDepthFunc(uint f);
        [DllImport(Lib)] public static extern void glDepthMask(bool flag);
        [DllImport(Lib)] public static extern void glCullFace(uint mode);
        [DllImport(Lib)] public static extern void glPolygonOffset(float factor, float units);
        [DllImport(Lib)] public static extern void glGenTextures(int n, uint[] textures);
        [DllImport(Lib)] public static extern void glDeleteTextures(int n, uint[] textures);
        [DllImport(Lib)] public static extern void glBindTexture(uint target, uint texture);
        [DllImport(Lib)] public static extern void glTexParameteri(uint target, uint pname, int param);
        [DllImport(Lib)] public static extern void glTexParameterf(uint target, uint pname, float param);
        [DllImport(Lib)] public static extern void glTexImage2D(uint target, int level, int internalFormat, int w, int h, int border, uint format, uint type, int[] pixels);
        [DllImport(Lib)] public static extern void glTexImage2D(uint target, int level, int internalFormat, int w, int h, int border, uint format, uint type, IntPtr pixels);
        [DllImport(Lib)] public static extern void glTexSubImage2D(uint target, int level, int x, int y, int w, int h, uint format, uint type, IntPtr pixels);
        [DllImport(Lib)] public static extern void glPixelStorei(uint pname, int param);
        [DllImport(Lib)] public static extern void glDrawArrays(uint mode, int first, int count);
        [DllImport(Lib)] public static extern void glGetFloatv(uint pname, out float value);
        [DllImport(Lib)] public static extern IntPtr glGetString(uint name);
        [DllImport(Lib)] public static extern uint glGetError();

        [DllImport(Lib)] public static extern IntPtr wglCreateContext(IntPtr hdc);
        [DllImport(Lib)] public static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);
        [DllImport(Lib)] public static extern bool wglDeleteContext(IntPtr hglrc);
        [DllImport(Lib)] public static extern IntPtr wglGetProcAddress(string name);

        public static string GetString(uint name)
        {
            var p = glGetString(name);
            return p == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(p);
        }

        // ---- OpenGL 2.x entry points

        public delegate uint CreateShaderFn(uint type);
        public delegate void ShaderSourceFn(uint shader, int count, string[] src, int[] length);
        public delegate void CompileShaderFn(uint shader);
        public delegate void GetShaderivFn(uint shader, uint pname, out int value);
        public delegate void GetShaderInfoLogFn(uint shader, int max, out int length, StringBuilder log);
        public delegate uint CreateProgramFn();
        public delegate void AttachShaderFn(uint program, uint shader);
        public delegate void BindAttribLocationFn(uint program, uint index, string name);
        public delegate void LinkProgramFn(uint program);
        public delegate void UseProgramFn(uint program);
        public delegate int GetUniformLocationFn(uint program, string name);
        public delegate void Uniform1iFn(int loc, int v);
        public delegate void Uniform1fFn(int loc, float v);
        public delegate void Uniform3fFn(int loc, float x, float y, float z);
        public delegate void Uniform4fFn(int loc, float x, float y, float z, float w);
        public delegate void UniformMatrix4fvFn(int loc, int count, bool transpose, float[] m);
        public delegate void EnableVertexAttribArrayFn(uint index);
        public delegate void VertexAttribPointerFn(uint index, int size, uint type, bool normalized, int stride, IntPtr offset);
        public delegate void GenBuffersFn(int n, uint[] buffers);
        public delegate void DeleteBuffersFn(int n, uint[] buffers);
        public delegate void BindBufferFn(uint target, uint buffer);
        public delegate void BufferDataFn(uint target, IntPtr size, float[] data, uint usage);
        public delegate bool SwapIntervalFn(int interval);
        public delegate bool ChoosePixelFormatArbFn(IntPtr hdc, int[] attribI, float[] attribF, uint max, int[] formats, out uint count);

        public static CreateShaderFn CreateShader;
        public static ShaderSourceFn ShaderSource;
        public static CompileShaderFn CompileShader;
        public static GetShaderivFn GetShaderiv, GetProgramiv;
        public static GetShaderInfoLogFn GetShaderInfoLog, GetProgramInfoLog;
        public static CreateProgramFn CreateProgram;
        public static AttachShaderFn AttachShader;
        public static BindAttribLocationFn BindAttribLocation;
        public static LinkProgramFn LinkProgram;
        public static UseProgramFn UseProgram;
        public static GetUniformLocationFn GetUniformLocation;
        public static Uniform1iFn Uniform1i;
        public static Uniform1fFn Uniform1f;
        public static Uniform3fFn Uniform3f;
        public static Uniform4fFn Uniform4f;
        public static UniformMatrix4fvFn UniformMatrix4fv;
        public static EnableVertexAttribArrayFn EnableVertexAttribArray;
        public static VertexAttribPointerFn VertexAttribPointer;
        public static GenBuffersFn GenBuffers;
        public static DeleteBuffersFn DeleteBuffers;
        public static BindBufferFn BindBuffer;
        public static BufferDataFn BufferData;
        public static SwapIntervalFn SwapInterval;

        static T Load<T>(string name) where T : class
        {
            var p = wglGetProcAddress(name);
            long v = p.ToInt64();
            if (v == 0 || v == 1 || v == 2 || v == 3 || v == -1) return null;
            return Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
        }

        /// <summary>Loads the OpenGL 2.x functions; false when the driver does not provide them.</summary>
        public static bool LoadExtensions()
        {
            CreateShader = Load<CreateShaderFn>("glCreateShader");
            ShaderSource = Load<ShaderSourceFn>("glShaderSource");
            CompileShader = Load<CompileShaderFn>("glCompileShader");
            GetShaderiv = Load<GetShaderivFn>("glGetShaderiv");
            GetProgramiv = Load<GetShaderivFn>("glGetProgramiv");
            GetShaderInfoLog = Load<GetShaderInfoLogFn>("glGetShaderInfoLog");
            GetProgramInfoLog = Load<GetShaderInfoLogFn>("glGetProgramInfoLog");
            CreateProgram = Load<CreateProgramFn>("glCreateProgram");
            AttachShader = Load<AttachShaderFn>("glAttachShader");
            BindAttribLocation = Load<BindAttribLocationFn>("glBindAttribLocation");
            LinkProgram = Load<LinkProgramFn>("glLinkProgram");
            UseProgram = Load<UseProgramFn>("glUseProgram");
            GetUniformLocation = Load<GetUniformLocationFn>("glGetUniformLocation");
            Uniform1i = Load<Uniform1iFn>("glUniform1i");
            Uniform1f = Load<Uniform1fFn>("glUniform1f");
            Uniform3f = Load<Uniform3fFn>("glUniform3f");
            Uniform4f = Load<Uniform4fFn>("glUniform4f");
            UniformMatrix4fv = Load<UniformMatrix4fvFn>("glUniformMatrix4fv");
            EnableVertexAttribArray = Load<EnableVertexAttribArrayFn>("glEnableVertexAttribArray");
            VertexAttribPointer = Load<VertexAttribPointerFn>("glVertexAttribPointer");
            GenBuffers = Load<GenBuffersFn>("glGenBuffers");
            DeleteBuffers = Load<DeleteBuffersFn>("glDeleteBuffers");
            BindBuffer = Load<BindBufferFn>("glBindBuffer");
            BufferData = Load<BufferDataFn>("glBufferData");
            SwapInterval = Load<SwapIntervalFn>("wglSwapIntervalEXT");
            return CreateShader != null && ShaderSource != null && CompileShader != null && CreateProgram != null && LinkProgram != null &&
                   UseProgram != null && GetUniformLocation != null && UniformMatrix4fv != null && VertexAttribPointer != null &&
                   GenBuffers != null && BindBuffer != null && BufferData != null && EnableVertexAttribArray != null && BindAttribLocation != null &&
                   Uniform1i != null && Uniform1f != null && Uniform3f != null && Uniform4f != null && AttachShader != null && GetShaderiv != null && GetProgramiv != null;
        }

        public static ChoosePixelFormatArbFn LoadChoosePixelFormat() => Load<ChoosePixelFormatArbFn>("wglChoosePixelFormatARB");
    }

    /// <summary>Win32 pieces needed to create an OpenGL context on a window.</summary>
    static class Wgl
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct PIXELFORMATDESCRIPTOR
        {
            public short nSize, nVersion;
            public int dwFlags;
            public byte iPixelType, cColorBits, cRedBits, cRedShift, cGreenBits, cGreenShift, cBlueBits, cBlueShift, cAlphaBits, cAlphaShift;
            public byte cAccumBits, cAccumRedBits, cAccumGreenBits, cAccumBlueBits, cAccumAlphaBits, cDepthBits, cStencilBits, cAuxBuffers, iLayerType, bReserved;
            public int dwLayerMask, dwVisibleMask, dwDamageMask;
        }

        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern int ChoosePixelFormat(IntPtr hdc, ref PIXELFORMATDESCRIPTOR pfd);
        [DllImport("gdi32.dll")] public static extern bool SetPixelFormat(IntPtr hdc, int format, ref PIXELFORMATDESCRIPTOR pfd);
        [DllImport("gdi32.dll")] public static extern bool SwapBuffers(IntPtr hdc);

        public static PIXELFORMATDESCRIPTOR Descriptor()
        {
            return new PIXELFORMATDESCRIPTOR
            {
                nSize = (short)Marshal.SizeOf(typeof(PIXELFORMATDESCRIPTOR)),
                nVersion = 1,
                dwFlags = 0x4 | 0x20 | 0x1,     // draw to window, support OpenGL, double buffer
                iPixelType = 0,
                cColorBits = 32,
                cAlphaBits = 8,
                cDepthBits = 24,
                cStencilBits = 8,
            };
        }
    }

    /// <summary>4x4 column-major matrices (OpenGL layout).</summary>
    static class M4
    {
        public static float[] Identity() => new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };

        public static float[] Mul(float[] a, float[] b)
        {
            var r = new float[16];
            for (int c = 0; c < 4; c++)
                for (int row = 0; row < 4; row++)
                {
                    float s = 0;
                    for (int k = 0; k < 4; k++) s += a[k * 4 + row] * b[c * 4 + k];
                    r[c * 4 + row] = s;
                }
            return r;
        }

        public static float[] Perspective(double fovY, double aspect, double near, double far)
        {
            float f = (float)(1 / Math.Tan(fovY / 2));
            var m = new float[16];
            m[0] = f / (float)aspect;
            m[5] = f;
            m[10] = (float)((far + near) / (near - far));
            m[11] = -1;
            m[14] = (float)(2 * far * near / (near - far));
            return m;
        }

        public static float[] LookAt(V3 eye, V3 target, V3 up)
        {
            var f = (target - eye).Normalized();
            var s = V3.Cross(f, up).Normalized();
            var u = V3.Cross(s, f);
            return new[]
            {
                s.X, u.X, -f.X, 0,
                s.Y, u.Y, -f.Y, 0,
                s.Z, u.Z, -f.Z, 0,
                -V3.Dot(s, eye), -V3.Dot(u, eye), V3.Dot(f, eye), 1,
            };
        }

        public static float[] Translate(float x, float y, float z) => new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, x, y, z, 1 };
        public static float[] Scale(float x, float y, float z) => new float[] { x, 0, 0, 0, 0, y, 0, 0, 0, 0, z, 0, 0, 0, 0, 1 };

        public static float[] RotY(double a)
        {
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            return new[] { c, 0, -s, 0, 0, 1, 0, 0, s, 0, c, 0, 0, 0, 0, 1 };
        }

        public static float[] RotX(double a)
        {
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            return new[] { 1, 0, 0, 0, 0, c, s, 0, 0, -s, c, 0, 0, 0, 0, 1 };
        }

        public static float[] RotZ(double a)
        {
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            return new[] { c, s, 0, 0, -s, c, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
        }

        public static V3 Point(float[] m, V3 p) => new V3(
            m[0] * p.X + m[4] * p.Y + m[8] * p.Z + m[12],
            m[1] * p.X + m[5] * p.Y + m[9] * p.Z + m[13],
            m[2] * p.X + m[6] * p.Y + m[10] * p.Z + m[14]);

        public static V3 Dir(float[] m, V3 p) => new V3(
            m[0] * p.X + m[4] * p.Y + m[8] * p.Z,
            m[1] * p.X + m[5] * p.Y + m[9] * p.Z,
            m[2] * p.X + m[6] * p.Y + m[10] * p.Z);
    }

    struct V3
    {
        public float X, Y, Z;
        public V3(double x, double y, double z) { X = (float)x; Y = (float)y; Z = (float)z; }
        public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V3 operator *(V3 a, double s) => new V3(a.X * s, a.Y * s, a.Z * s);
        public static float Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static V3 Cross(V3 a, V3 b) => new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        public V3 Normalized() { float l = Length; return l < 1e-6f ? this : new V3(X / l, Y / l, Z / l); }
    }
}
