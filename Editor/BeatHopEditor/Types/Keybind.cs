﻿using System;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace BeatHopEditor
{
    [Serializable]
    internal class Keybind
    {
        public Keys Key;

        public bool Ctrl;
        public bool Alt;
        public bool Shift;

        public Keybind(Keys key, bool ctrl, bool alt, bool shift)
        {
            Key = key;

            Ctrl = ctrl;
            Alt = alt;
            Shift = shift;
        }
    }
}
