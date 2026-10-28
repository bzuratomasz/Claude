using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LazyTurtle
{
    /// <summary>
    /// Game window: runs the game loop, forwards keyboard and mouse input and draws each frame —
    /// with the OpenGL 3D renderer when the graphics driver supports it, otherwise with the
    /// software raycaster (old PCs, virtual machines without a graphics driver, or "--software").
    /// </summary>
    sealed class GameForm : Form
    {
        const int RenderHeight = 360;   // software renderer: internal resolution (lines), scaled up to the window

        readonly Game game;
        readonly Hud hud;
        readonly Renderer renderer = new Renderer();
        readonly Input input = new Input();
        readonly Timer timer = new Timer { Interval = 10 };
        readonly Stopwatch clock = new Stopwatch();
        readonly bool forceSoftware;
        GLRenderer gl;
        Bitmap hudBitmap;
        double last, fps, fpsStart, lastFrame;
        int frames, languageVersion;
        bool fullScreen, showFps, glWanted;
        Point lastMouse;
        FormWindowState savedState;
        Rectangle savedBounds;

        [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);

        [StructLayout(LayoutKind.Sequential)]
        struct NativeMessage
        {
            public IntPtr Handle;
            public uint Message;
            public IntPtr WParam, LParam;
            public uint Time;
            public Point Location;
        }

        [DllImport("user32.dll")]
        static extern bool PeekMessage(out NativeMessage msg, IntPtr hWnd, uint filterMin, uint filterMax, uint remove);

        public GameForm(int startLevel = -1, bool debug = false, bool software = false)
        {
            forceSoftware = software;
            Text = "Leniwy Żółw - Lazy Turtle";
            BackColor = Color.Black;
            KeyPreview = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(640, 400);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch (ArgumentException) { }

            var area = Screen.PrimaryScreen.WorkingArea;
            int w = Math.Min(1280, area.Width - 40), h = Math.Min(720, area.Height - 60);
            if (w * 9 > h * 16) w = h * 16 / 9; else h = w * 9 / 16;
            ClientSize = new Size(w, h);

            game = new Game { DebugKeys = debug };
            if (startLevel >= 0) game.Play(startLevel);
            hud = new Hud(game);
            timer.Tick += (s, e) => { Tick(); Invalidate(); };
        }

        /// <summary>OpenGL needs its own device context per window.</summary>
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x20;   // CS_OWNDC
                return cp;
            }
        }

        public string RendererInfo => gl != null ? gl.Info : "Software renderer";

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            timeBeginPeriod(1);
            clock.Start();
            if (!forceSoftware)
            {
                string info;
                gl = GLRenderer.TryCreate(this, out info);
                if (gl == null) Trace.WriteLine("OpenGL not available, using the software renderer: " + info);
            }
            if (gl != null)
            {
                // GDI double buffering would fight with OpenGL's own buffers
                SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
                DoubleBuffered = false;
                Application.Idle += OnIdle;
            }
            else timer.Start();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            Application.Idle -= OnIdle;
            if (gl != null)
            {
                gl.Dispose();
                gl = null;
                glWanted = !IsDisposed && !Disposing;
            }
            base.OnHandleDestroyed(e);
        }

        /// <summary>Some window changes recreate the window handle; OpenGL then needs a new context.</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!glWanted) return;
            glWanted = false;
            string info;
            gl = GLRenderer.TryCreate(this, out info);
            if (gl != null) Application.Idle += OnIdle;
            else timer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            Application.Idle -= OnIdle;
            timeEndPeriod(1);
            game.Save.Write();
            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                hud.Dispose();
                game.Dispose();
                hudBitmap?.Dispose();
                gl?.Dispose();
            }
            base.Dispose(disposing);
        }

        void Tick()
        {
            double now = clock.Elapsed.TotalSeconds;
            double dt = Math.Min(0.05, now - last);
            last = now;
            game.Update(dt, input);
            input.EndFrame();
            if (game.ExitRequested) Close();
        }

        /// <summary>OpenGL game loop: update and draw continuously while no window messages are waiting.</summary>
        void OnIdle(object sender, EventArgs e)
        {
            NativeMessage msg;
            while (gl != null && !IsDisposed && !PeekMessage(out msg, IntPtr.Zero, 0, 0, 0))
            {
                if (!gl.VSync)
                {
                    // no vsync: limit to ~120 fps instead of burning a CPU core
                    double wait = lastFrame + 1.0 / 120 - clock.Elapsed.TotalSeconds;
                    if (wait > 0.002) System.Threading.Thread.Sleep((int)(wait * 1000));
                }
                lastFrame = clock.Elapsed.TotalSeconds;
                Tick();
                if (gl == null || IsDisposed) return;
                RenderGL();
            }
        }

        void RenderGL()
        {
            var rc = ClientRectangle;
            if (rc.Width <= 0 || rc.Height <= 0) return;
            if (languageVersion != game.LanguageVersion)
            {
                languageVersion = game.LanguageVersion;
                gl.Invalidate();
            }
            if (hudBitmap == null || hudBitmap.Width != rc.Width || hudBitmap.Height != rc.Height)
            {
                hudBitmap?.Dispose();
                hudBitmap = new Bitmap(rc.Width, rc.Height, PixelFormat.Format32bppPArgb);
            }
            using (var g = Graphics.FromImage(hudBitmap))
            {
                g.Clear(Color.Transparent);
                hud.Draw(g, rc, null);
                CountFrame(g, rc, rc.Width + "x" + rc.Height + "  " + gl.Info);
            }
            gl.Render(game, rc.Width, rc.Height, hudBitmap);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (gl != null)
            {
                RenderGL();
                return;
            }
            var rc = ClientRectangle;
            if (rc.Width <= 0 || rc.Height <= 0) return;
            int h = RenderHeight, w = Math.Max(160, (int)Math.Round(h * rc.Width / (double)rc.Height));
            renderer.Resize(w, h);
            game.Render(renderer);
            hud.Draw(e.Graphics, rc, renderer);
            CountFrame(e.Graphics, rc, w + "x" + h + "  software");
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // everything is painted in OnPaint / by OpenGL
        }

        void CountFrame(Graphics g, Rectangle rc, string info)
        {
            frames++;
            double now = clock.Elapsed.TotalSeconds;
            if (now - fpsStart >= 1)
            {
                fps = frames / (now - fpsStart);
                frames = 0;
                fpsStart = now;
            }
            if (showFps)
                TextRenderer.DrawText(g, ((int)fps) + " FPS  " + info, Font, new Point(8, rc.Height - 22), Color.Yellow, Color.Black);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F11 || (e.KeyCode == Keys.Enter && e.Alt)) ToggleFullScreen();
            else if (e.KeyCode == Keys.F3) showFps = !showFps;
            else input.KeyDown(e.KeyCode);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            lastMouse = e.Location;
            if (e.Button == MouseButtons.Left) input.Click = hud.HitTest(e.Location);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if ((e.Button & MouseButtons.Right) != 0)
                game.MouseLook(e.X - lastMouse.X, e.Y - lastMouse.Y);
            lastMouse = e.Location;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            game.Zoom(e.Delta);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            input.KeyUp(e.KeyCode);
            e.Handled = true;
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            input.Clear();
            game.Pause();
        }

        // Arrow keys and Tab are game keys, not dialog navigation.
        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                case Keys.Tab:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                case Keys.Tab:
                case Keys.Enter:
                case Keys.Escape:
                case Keys.Space:
                    return false;
            }
            return base.ProcessDialogKey(keyData);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_SYSCOMMAND = 0x0112, SC_KEYMENU = 0xF100;
            // Pressing Alt alone must not freeze the game by opening the window menu.
            if (m.Msg == WM_SYSCOMMAND && ((int)m.WParam & 0xFFF0) == SC_KEYMENU) return;
            base.WndProc(ref m);
        }

        void ToggleFullScreen()
        {
            if (!fullScreen)
            {
                savedState = WindowState;
                savedBounds = Bounds;
                WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                Bounds = Screen.FromControl(this).Bounds;
            }
            else
            {
                FormBorderStyle = FormBorderStyle.Sizable;
                Bounds = savedBounds;
                WindowState = savedState;
            }
            fullScreen = !fullScreen;
        }
    }
}
