﻿using System;

namespace BeatHopEditor
{
    [Serializable]
    internal class ListSetting
    {
        public string Current;
        public string[] Possible;

        public ListSetting(string current, params string[] possible)
        {
            Current = current;
            Possible = possible;
        }
    }
}
