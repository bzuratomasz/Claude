using System.Collections.Generic;
using System.Windows.Forms;

namespace LazyTurtle
{
    /// <summary>Keyboard state: keys held down plus keys pressed since the last frame.</summary>
    sealed class Input
    {
        readonly HashSet<Keys> down = new HashSet<Keys>();
        readonly HashSet<Keys> pressed = new HashSet<Keys>();

        public void KeyDown(Keys k)
        {
            if (down.Add(k)) pressed.Add(k);
        }

        public void KeyUp(Keys k) => down.Remove(k);

        public void Clear()
        {
            down.Clear();
            pressed.Clear();
        }

        /// <summary>Hit-area action clicked with the mouse since the last frame (-1 = none).</summary>
        public int Click = -1;

        public void EndFrame()
        {
            pressed.Clear();
            Click = -1;
        }

        public bool Down(params Keys[] keys)
        {
            foreach (var k in keys)
                if (down.Contains(k)) return true;
            return false;
        }

        public bool Pressed(params Keys[] keys)
        {
            foreach (var k in keys)
                if (pressed.Contains(k)) return true;
            return false;
        }

        public bool Up => Pressed(Keys.Up, Keys.W);
        public bool DownKey => Pressed(Keys.Down, Keys.S);
        public bool Left => Pressed(Keys.Left, Keys.A);
        public bool Right => Pressed(Keys.Right, Keys.D);
        public bool Accept => Pressed(Keys.Enter, Keys.Space);
        public bool Back => Pressed(Keys.Escape);
    }
}
