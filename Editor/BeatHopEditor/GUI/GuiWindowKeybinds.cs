using System.Collections.Generic;
using System.Drawing;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace BeatHopEditor.GUI
{
    internal class GuiWindowKeybinds : GuiWindow
    {
        private readonly GuiButton BackButton = new(655, 930, 600, 100, 0, "RETURN TO SETTINGS", 48, false, false, "square");

        private readonly GuiLabel HFlipLabel = new(150, 49, 128, 26, "Mirror Notes", 24, false, false, "main", false);
        private readonly GuiLabel HFlipCAS = new(426, 83, 256, 40, "", 24, false, false, "main", false);
        private readonly GuiTextbox HFlipBox = new(150, 75, 128, 40, "", 24, false, false, false, "hFlip", "main", true);
        private readonly GuiButton HFlipReset = new(288, 75, 128, 40, 1, "RESET", 24);

        private readonly GuiLabel SwitchClickLabel = new(150, 119, 128, 26, "Switch Click Function", 24, false, false, "main", false);
        private readonly GuiLabel SwitchClickCAS = new(426, 153, 256, 40, "", 24, false, false, "main", false);
        private readonly GuiTextbox SwitchClickBox = new(150, 145, 128, 40, "", 24, false, false, false, "switchClickTool", "main", true);
        private readonly GuiButton SwitchClickReset = new(288, 145, 128, 40, 3, "RESET", 24);

        private readonly GuiLabel ToggleQuantumLabel = new(150, 189, 128, 26, "Toggle Quantum", 24, false, false, "main", false);
        private readonly GuiLabel ToggleQuantumCAS = new(426, 223, 256, 40, "", 24, false, false, "main", false);
        private readonly GuiTextbox ToggleQuantumBox = new(150, 215, 128, 40, "", 24, false, false, false, "quantum", "main", true);
        private readonly GuiButton ToggleQuantumReset = new(288, 215, 128, 40, 4, "RESET", 24);

        private readonly GuiLabel OpenTimingsLabel = new(150, 259, 128, 26, "Open Timings", 24, false, false, "main", false);
        private readonly GuiLabel OpenTimingsCAS = new(426, 293, 256, 40, "", 24, false, false, "main", false);
        private readonly GuiTextbox OpenTimingsBox = new(150, 285, 128, 40, "", 24, false, false, false, "openTimings", "main", true);
        private readonly GuiButton OpenTimingsReset = new(288, 285, 128, 40, 5, "RESET", 24);

        private readonly GuiLabel OpenBookmarksLabel = new(150, 329, 128, 26, "Open Bookmarks", 24, false, false, "main", false);
        private readonly GuiLabel OpenBookmarksCAS = new(426, 363, 256, 40, "", 24, false, false, "main", false);
        private readonly GuiTextbox OpenBookmarksBox = new(150, 355, 128, 40, "", 24, false, false, false, "openBookmarks", "main", true);
        private readonly GuiButton OpenBookmarksReset = new(288, 355, 128, 40, 6, "RESET", 24);

        private readonly GuiLabel OpenDirectoryLabel = new(150, 399, 128, 26, "Open Directory", 24, false, false, "main", false);
        private readonly GuiLabel OpenDirectoryCAS = new(426, 433, 256, 40, "", 24, false, false, "main", false);
        private readonly GuiTextbox OpenDirectoryBox = new(150, 425, 128, 40, "", 24, false, false, false, "openDirectory", "main", true);
        private readonly GuiButton OpenDirectoryReset = new(288, 425, 128, 40, 10, "RESET", 24);

        private readonly GuiLabel GridLabel = new(1090, 49, 128, 26, "Grid", 24, false, false, "main", false);
        private readonly GuiTextbox Grid0Box = new(1090, 75, 128, 62, "", 24, false, false, false, "gridKey0", "main", true);
        private readonly GuiButton Grid0Reset = new(1090, 141, 128, 62, 90, "RESET", 32);
        private readonly GuiTextbox Grid1Box = new(1228, 75, 128, 62, "", 24, false, false, false, "gridKey1", "main", true);
        private readonly GuiButton Grid1Reset = new(1228, 141, 128, 62, 91, "RESET", 32);
        private readonly GuiTextbox Grid2Box = new(1366, 75, 128, 62, "", 24, false, false, false, "gridKey2", "main", true);
        private readonly GuiButton Grid2Reset = new(1366, 141, 128, 62, 92, "RESET", 32);
        private readonly GuiTextbox Grid3Box = new(1504, 75, 128, 62, "", 24, false, false, false, "gridKey3", "main", true);
        private readonly GuiButton Grid3Reset = new(1504, 141, 128, 62, 93, "RESET", 32);
        private readonly GuiTextbox Grid4Box = new(1642, 75, 128, 62, "", 24, false, false, false, "gridKey4", "main", true);
        private readonly GuiButton Grid4Reset = new(1642, 141, 128, 62, 94, "RESET", 32);

        private readonly GuiCheckbox CtrlIndicator = new(64, 828, 64, 64, "", "CTRL Held", 32);
        private readonly GuiCheckbox AltIndicator = new(64, 912, 64, 64, "", "ALT Held", 32);
        private readonly GuiCheckbox ShiftIndicator = new(64, 996, 64, 64, "", "SHIFT Held", 32);

        private readonly GuiLabel StaticKeysLabel = new(480, 150, 960, 40, "", 24);

        public GuiWindowKeybinds() : base(0, 0, MainWindow.Instance.ClientSize.X, MainWindow.Instance.ClientSize.Y)
        {
            Controls = new List<WindowControl>
            {
                // Buttons
                BackButton, HFlipReset, SwitchClickReset, ToggleQuantumReset, OpenTimingsReset, OpenBookmarksReset, OpenDirectoryReset,
                Grid0Reset, Grid1Reset, Grid2Reset, Grid3Reset, Grid4Reset,
                // Checkboxes
                CtrlIndicator, AltIndicator, ShiftIndicator,
                // Boxes
                HFlipBox, SwitchClickBox, ToggleQuantumBox, OpenTimingsBox, OpenBookmarksBox, OpenDirectoryBox,
                Grid0Box, Grid1Box, Grid2Box, Grid3Box, Grid4Box,
                // Labels
                HFlipLabel, SwitchClickLabel, ToggleQuantumLabel, OpenTimingsLabel, OpenBookmarksLabel, OpenDirectoryLabel,
                HFlipCAS, SwitchClickCAS, ToggleQuantumCAS, OpenTimingsCAS, OpenBookmarksCAS, OpenDirectoryCAS,
                GridLabel, StaticKeysLabel
            };

            BackgroundSquare = new(0, 0, 1920, 1080, Color.FromArgb(255, 30, 30, 30), false, "background_menu.png", "menubg");
            Init();

            string[] staticList =
            {
                "Static keybinds:",
                "",
                "> Zoom: CTRL + SCROLL",
                "",
                "> Beat Divisor: SHIFT + SCROLL",
                ">> CTRL + SHIFT + SCROLL to increment by 0.5",
                "",
                "> Scroll through song: SCROLL/LEFT/RIGHT",
                "",
                "> Place stored patterns: 0-9",
                ">> Hold SHIFT to store selected notes as the key's pattern",
                ">> Hold CTRL to clear the key's pattern",
                "",
                "> Select all: CTRL + A",
                "> Save: CTRL + S",
                "> Save as: CTRL + SHIFT + S",
                "> Undo: CTRL + Z",
                "> Redo: CTRL + Y",
                "> Copy: CTRL + C",
                "> Paste: CTRL + V",
                "> Cut: CTRL + X",
                "> Fullscreen: F11",
                "> Delete: DELETE/BACKSPACE",
                "> Play/Pause: SPACE",
                "> Deselect all: ESCAPE"
            };
            StaticKeysLabel.Text = string.Join("\n", staticList);

            OnResize(MainWindow.Instance.ClientSize);
        }

        public override void Render(float mousex, float mousey, float frametime)
        {
            var editor = MainWindow.Instance;

            CtrlIndicator.Toggle = editor.CtrlHeld;
            AltIndicator.Toggle = editor.AltHeld;
            ShiftIndicator.Toggle = editor.ShiftHeld;

            HFlipCAS.Text = CAS(Settings.settings["hFlip"]);
            SwitchClickCAS.Text = CAS(Settings.settings["switchClickTool"]);
            ToggleQuantumCAS.Text = CAS(Settings.settings["quantum"]);
            OpenTimingsCAS.Text = CAS(Settings.settings["openTimings"]);
            OpenBookmarksCAS.Text = CAS(Settings.settings["openBookmarks"]);
            OpenDirectoryCAS.Text = CAS(Settings.settings["openDirectory"]);

            base.Render(mousex, mousey, frametime);
        }

        public override void OnResize(Vector2i size)
        {
            Rect = new RectangleF(0, 0, size.X, size.Y);

            base.OnResize(size);
        }

        public override void OnButtonClicked(int id)
        {
            switch (id)
            {
                case 0:
                    MainWindow.Instance.SwitchWindow(new GuiWindowSettings());

                    break;

                case 1:
                    Settings.settings["hFlip"] = new Keybind(Keys.H, false, false, true);
                    HFlipBox.Text = "H";
                    break;
                case 3:
                    Settings.settings["switchClickTool"] = new Keybind(Keys.Tab, false, false, false);
                    SwitchClickBox.Text = "TAB";
                    break;
                case 4:
                    Settings.settings["quantum"] = new Keybind(Keys.Q, true, false, false);
                    ToggleQuantumBox.Text = "Q";
                    break;
                case 5:
                    Settings.settings["openTimings"] = new Keybind(Keys.T, true, false, false);
                    OpenTimingsBox.Text = "T";
                    break;
                case 6:
                    Settings.settings["openBookmarks"] = new Keybind(Keys.B, true, false, false);
                    OpenBookmarksBox.Text = "B";
                    break;

                case 90:
                    Settings.settings["gridKeys"][0] = Keys.Q;
                    Grid0Box.Text = "Q";
                    break;
                case 91:
                    Settings.settings["gridKeys"][1] = Keys.W;
                    Grid1Box.Text = "W";
                    break;
                case 92:
                    Settings.settings["gridKeys"][2] = Keys.E;
                    Grid2Box.Text = "E";
                    break;
                case 93:
                    Settings.settings["gridKeys"][3] = Keys.R;
                    Grid3Box.Text = "R";
                    break;
                case 94:
                    Settings.settings["gridKeys"][4] = Keys.T;
                    Grid4Box.Text = "T";
                    break;
            }

            base.OnButtonClicked(id);
        }

        private static string CAS(Keybind key)
        {
            var cas = new List<string>();

            if (key.Ctrl)
                cas.Add("CTRL");
            if (key.Alt)
                cas.Add("ALT");
            if (key.Shift)
                cas.Add("SHIFT");

            return string.Join(" + ", cas);
        }
    }
}
