using System;
using System.Globalization;

namespace BeatHopEditor
{
    [Serializable]
    internal class Note
    {
        public float X;
        public long Ms;

        public long DragStartMs;
        public bool Selected;

        public Note(float x, long ms)
        {
            X = x;
            Ms = ms;
        }

        public Note(string data, CultureInfo culture)
        {
            var split = data.Split('|');

            X = float.Parse(split[0], culture);
            Ms = long.Parse(split[2]);
        }

        public string ToString(CultureInfo culture)
        {
            var x = Math.Round(X, 2);

            return $",{x.ToString(culture)}|0|{Ms}";
        }

        public Note Clone()
        {
            return new(X, Ms);
        }
    }
}
