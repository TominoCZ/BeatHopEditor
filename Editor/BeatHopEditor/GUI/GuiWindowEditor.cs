using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Drawing;
using OpenTK.Mathematics;
using BeatHopEditor.Types;

namespace BeatHopEditor.GUI
{
    internal class GuiWindowEditor : GuiWindow
    {
        private readonly GuiButton CopyButton = new(0, 0, 501, 42, 0, "COPY MAP DATA", 21, true);
        private readonly GuiButton BackButton = new(0, 0, 395, 42, 1, "BACK TO MENU", 21, true);
        private readonly GuiButton SaveButton = new(0, 0, 101, 42, 24, "SAVE", 21, true);

        private readonly GuiSlider Tempo = new(0, 0, 0, 0, "tempo", false);
        private readonly GuiSlider MasterVolume = new(0, 0, 0, 0, "masterVolume", true);
        private readonly GuiSlider SfxVolume = new(0, 0, 0, 0, "sfxVolume", true);
        private readonly GuiSlider BeatSnapDivisor = new(0, 0, 0, 0, "beatDivisor", false);
        private readonly GuiSlider QuantumSnapDivisor = new(0, 0, 0, 0, "quantumSnapping", false);
        public readonly GuiSliderTimeline Timeline = new(0, 0, 0, 0, false);
        private readonly GuiButtonPlayPause PlayPause = new(0, 0, 0, 0, 2);
        private readonly GuiCheckbox AutoAdvance = new(0, 0, 0, 0, "autoAdvance", "Auto-Advance", 25);

        private readonly GuiButton OptionsNav = new(10, 60, 400, 50, 3, "OPTIONS >", 25, false, true);
        private readonly GuiCheckbox Autoplay = new(10, 130, 35, 35, "autoplay", "Autoplay", 22, false, true);
        private readonly GuiCheckbox ApproachSquares = new(10, 175, 35, 35, "approachSquares", "Approach Squares", 22, false, true);
        private readonly GuiCheckbox GridNumbers = new(10, 220, 35, 35, "gridNumbers", "Grid Numbers", 22, false, true);
        private readonly GuiCheckbox GridLetters = new(10, 265, 35, 35, "gridLetters", "Grid Letters", 22, false, true);
        private readonly GuiCheckbox Quantum = new(10, 310, 35, 35, "enableQuantum", "Quantum", 22, false, true);
        private readonly GuiCheckbox QuantumGridLines = new(10, 355, 35, 35, "quantumGridLines", "Quantum Grid Lines", 22, false, true);
        private readonly GuiCheckbox QuantumGridSnap = new(10, 400, 35, 35, "quantumGridSnap", "Snap to Grid", 22, false, true);
        private readonly GuiCheckbox Metronome = new(10, 445, 35, 35, "metronome", "Metronome", 22, false, true);
        private readonly GuiCheckbox SeparateClickTools = new(10, 490, 35, 35, "separateClickTools", "Separate Click Tools", 22, false, true);
        private readonly GuiCheckbox JumpOnPaste = new(10, 535, 35, 35, "jumpPaste", "Jump on Paste", 22, false, true);
        private readonly GuiSlider TrackHeight = new(378, 384, 32, 224, "trackHeight", false, false, true);
        private readonly GuiSlider TrackCursorPos = new(10, 596, 400, 32, "cursorPos", false, false, true);

        private readonly GuiButton TimingNav = new(10, 120, 400, 50, 4, "TIMING >", 25, false, true);
        private readonly GuiTextbox ExportOffset = new(10, 210, 128, 40, "0", 25, true, false, true, "exportOffset");
        private readonly GuiTextbox SfxOffset = new(10, 285, 128, 40, "0", 25, true, false, true, "sfxOffset");
        private readonly GuiButton UseCurrentMs = new(143, 210, 192, 40, 5, "USE CURRENT MS", 21, false, true);
        private readonly GuiButton OpenTimings = new(10, 335, 256, 40, 6, "OPEN BPM SETUP", 21, false, true);
        private readonly GuiButton ImportIni = new(10, 385, 256, 40, 16, "IMPORT INI", 21, false, true);

        private readonly GuiButton PatternsNav = new(10, 180, 400, 50, 8, "PATTERNS >", 25, false, true);
        private readonly GuiButton Mirror = new(10, 250, 256, 40, 9, "MIRROR", 21, false, true);
        public readonly GuiTextbox ScaleBox = new(10, 325, 128, 40, "150", 25, true, false, true);
        private readonly GuiButton ScaleButton = new(143, 325, 128, 40, 15, "SCALE", 21, false, true);
        private readonly GuiCheckbox ApplyOnPaste = new(10, 375, 40, 40, "applyOnPaste", "Apply Scale On Paste", 25, false, true);
        private readonly GuiTextbox TweenDivisor = new(10, 460, 128, 40, "4", 25, true, false, true, "tweenDivisor", "main", false, false, true);
        private readonly GuiButton TweenButton = new(143, 460, 128, 40, 10, "DRAW", 21, false, true);
        private readonly GuiButtonList TweenFunction = new(10, 510, 256, 40, "tweenFunction", 21, false, true);
        private readonly GuiButtonList TweenMode = new(10, 560, 256, 40, "tweenMode", 21, false, true);
        private readonly GuiCheckbox VisualizeTween = new(10, 610, 40, 40, "visualizeTween", "Visualize Tween", 25, false, true);

        private readonly GuiButton ReviewNav = new(10, 240, 400, 50, 19, "REVIEW >", 25, false, true);
        private readonly GuiButton OpenBookmarks = new(10, 310, 256, 40, 7, "EDIT BOOKMARKS", 21, false, true);
        private readonly GuiButton CopyBookmarks = new(10, 360, 256, 40, 20, "COPY BOOKMARKS", 21, false, true);
        private readonly GuiButton PasteBookmarks = new(10, 410, 256, 40, 21, "PASTE BOOKMARKS", 21, false, true);

        private readonly GuiLabel ToastLabel = new(0, 0, 0, 0, "", 36);

        private readonly GuiLabel ZoomLabel = new(420, 60, 75, 30, "Zoom: ", 24, true, true, "main", false, Settings.settings["color1"]);
        private readonly GuiLabel ZoomValueLabel = new(495, 60, 75, 30, "", 24, true, true, "main", false, Settings.settings["color2"]);
        private readonly GuiLabel ClickModeLabel = new(0, 0, 301, 42, "", 24, true, false, "main", false, Settings.settings["color1"]);
        private readonly GuiLabel BeatDivisorLabel = new(0, 0, 0, 30, "", 24, true, true, "main", true, Settings.settings["color1"]);
        private readonly GuiLabel SnappingLabel = new(0, 0, 0, 30, "", 24, true, true, "main", true, Settings.settings["color1"]);

        private readonly GuiLabel TempoLabel = new(0, 0, 0, 30, "", 24, true, false, "main", true, Settings.settings["color1"]);
        private readonly GuiLabel MusicLabel = new(0, 0, 0, 30, "Music", 18, true, false, "main", true, Settings.settings["color1"]);
        private readonly GuiLabel MusicValueLabel = new(0, 0, 0, 30, "", 18, true, false, "main", true, Settings.settings["color1"]);
        private readonly GuiLabel SfxLabel = new(0, 0, 0, 30, "SFX", 18, true, false, "main", true, Settings.settings["color1"]);
        private readonly GuiLabel SfxValueLabel = new(0, 0, 0, 30, "", 18, true, false, "main", true, Settings.settings["color1"]);

        private readonly GuiLabel CurrentTimeLabel = new(0, 0, 0, 30, "", 20, true, false, "main", true, Settings.settings["color1"]);
        private readonly GuiLabel CurrentMsLabel = new(0, 0, 0, 30, "", 20, true, false, "main", true, Settings.settings["color1"]);
        private readonly GuiLabel TotalTimeLabel = new(0, 0, 0, 30, "", 20, true, false, "main", true, Settings.settings["color1"]);
        private readonly GuiLabel NotesLabel = new(0, 0, 0, 30, "", 24, true, false, "main", true, Settings.settings["color1"]);

        private readonly GuiLabel TrackHeightLabel = new(220, 576, 158, 30, "", 22, false, true, "main", false, Settings.settings["color1"]);
        private readonly GuiLabel CursorPosLabel = new(10, 576, 158, 30, "", 22, false, true, "main", false, Settings.settings["color1"]);

        private readonly GuiLabel ExportOffsetLabel = new(10, 183, 158, 30, "Export Offset[ms]:", 24, false, true, "main", false, Settings.settings["color1"]);
        private readonly GuiLabel SfxOffsetLabel = new(10, 258, 158, 30, "SFX Offset[ms]:", 24, false, true, "main", false, Settings.settings["color1"]);

        private readonly GuiLabel ScaleLabel = new(10, 298, 158, 30, "Scale by Percent:", 24, false, true, "main", false, Settings.settings["color1"]);
        private readonly GuiLabel TweenDivisorLabel = new(10, 433, 158, 30, "Tween Divisor:", 24, false, true, "main", false, Settings.settings["color1"]);

        private float toastTime = 0f;
        private string navEnabled = "";
        private bool started = false;

        public GuiWindowEditor() : base(0, 0, MainWindow.Instance.ClientSize.X, MainWindow.Instance.ClientSize.Y)
        {
            Controls = new List<WindowControl>
            {
                // Buttons
                CopyButton, BackButton, SaveButton, PlayPause, OptionsNav, TimingNav, UseCurrentMs, OpenTimings, ImportIni, PatternsNav, Mirror, ScaleButton, TweenButton,
                TweenFunction, TweenMode, ReviewNav, OpenBookmarks, CopyBookmarks, PasteBookmarks,
                // Checkboxes
                AutoAdvance, Autoplay, ApproachSquares, GridNumbers, GridLetters, Quantum, QuantumGridLines, QuantumGridSnap, Metronome, SeparateClickTools, JumpOnPaste,
                ApplyOnPaste, VisualizeTween,
                // Sliders
                Tempo, MasterVolume, SfxVolume, BeatSnapDivisor, QuantumSnapDivisor, Timeline, TrackHeight, TrackCursorPos,
                // Boxes
                ExportOffset, SfxOffset, ScaleBox, TweenDivisor,
                // Labels
                ZoomLabel, ZoomValueLabel, ClickModeLabel, BeatDivisorLabel, SnappingLabel, TempoLabel, MusicLabel, MusicValueLabel, SfxLabel, SfxValueLabel, CurrentTimeLabel,
                CurrentMsLabel, TotalTimeLabel, NotesLabel, TrackHeightLabel, CursorPosLabel, ExportOffsetLabel, SfxOffsetLabel, ScaleLabel, TweenDivisorLabel, ToastLabel
            };

            BackgroundSquare = new(0, 0, 1920, 1080, Color.FromArgb(Settings.settings["editorBGOpacity"], 10, 10, 10), false, "background_editor.png", "editorbg");
            Track = new();
            Grid = new(500, 100);

            YOffset = Settings.settings["trackHeight"].Value + 64;
            Init();

            UpdateNav();
            started = false;
        }

        public override void Render(float mousex, float mousey, float frametime)
        {
            Grid?.ClearPreviewNotes();

            if (frametime < 2)
                toastTime = Math.Min(2, toastTime + frametime);

            var toastOffset = 1f;

            if (toastTime <= 0.5f)
                toastOffset = (float)Math.Sin(Math.Min(0.5f, toastTime) / 0.5f * MathHelper.PiOver2);
            if (toastTime >= 1.75f)
                toastOffset = (float)Math.Cos(Math.Min(0.25f, toastTime - 1.75f) / 0.25f * MathHelper.PiOver2);

            var toastHeight = FontRenderer.GetHeight(ToastLabel.TextSize, ToastLabel.Font);
            ToastLabel.Rect.Location = new PointF(Rect.X + Rect.Width / 2f, Rect.Height - toastOffset * toastHeight * 2.25f + toastHeight / 2f);
            ToastLabel.Color = Color.FromArgb((int)(Math.Pow(toastOffset, 3) * 255), ToastLabel.Color);
            
            ToastLabel.Update();

            var editor = MainWindow.Instance;
            var currentTime = Settings.settings["currentTime"];

            ZoomValueLabel.Text = $"{Math.Round(editor.Zoom * 100)}%";
            ClickModeLabel.Text = $"Click Mode: {(Settings.settings["selectTool"] ? "Select" : "Place")}";
            ClickModeLabel.Visible = Settings.settings["separateClickTools"];

            TrackHeightLabel.Text = $"Track Height: {Math.Round(64f + Settings.settings["trackHeight"].Value)}";
            CursorPosLabel.Text = $"Cursor Pos: {Math.Round(Settings.settings["cursorPos"].Value)}%";

            BeatDivisorLabel.Text = $"Beat Divisor: {Math.Round(Settings.settings["beatDivisor"].Value * 10) / 10 + 1f}";
            SnappingLabel.Text = $"Snapping: {Math.Round(Settings.settings["quantumSnapping"].Value) + 1}";
            TempoLabel.Text = $"PLAYBACK SPEED - {Math.Round(editor.Tempo * 100f)}%";
            MusicValueLabel.Text = Math.Round(Settings.settings["masterVolume"].Value * 100f).ToString();
            SfxValueLabel.Text = Math.Round(Settings.settings["sfxVolume"].Value * 100f).ToString();

            CurrentTimeLabel.Text = $"{(int)(currentTime.Value / 60000f)}:{(int)(currentTime.Value % 60000 / 1000f):0#}";
            TotalTimeLabel.Text = $"{(int)(currentTime.Max / 60000f)}:{(int)(currentTime.Max % 60000 / 1000f):0#}";
            NotesLabel.Text = $"{editor.Notes.Count} Notes";

            var currentMs = $"{(long)currentTime.Value:##,##0}ms";
            var progress = currentTime.Value / currentTime.Max;
            CurrentMsLabel.Rect.Location = new PointF(Timeline.Rect.X + Timeline.Rect.Height / 2f + (Timeline.Rect.Width - Timeline.Rect.Height) * progress, Timeline.Rect.Y - 4f);
            CurrentMsLabel.Text = currentMs;

            if (Settings.settings["visualizeTween"] && Settings.settings["tweenDivisor"] > 0)
            {
                var result = editor.TweenSelected();

                if (result != null)
                {
                    for (int i = 0; i < result.Count; i++)
                    {
                        if (result[i].Y > currentTime.Value)
                            Grid?.AddPreviewNote(result[i].X, result[i].Y, 2);
                    }
                }
            }

            if (!started)
            {
                OnResize(new Vector2i((int)Rect.Width, (int)Rect.Height));
                started = true;
            }

            base.Render(mousex, mousey, frametime);
        }

        public override void OnButtonClicked(int id)
        {
            var editor = MainWindow.Instance;
            var currentTime = Settings.settings["currentTime"];

            switch (id)
            {
                case 0:
                    try
                    {
                        Clipboard.SetText(Map.Save(editor.SoundID, editor.Notes, Settings.settings["correctOnCopy"]));
                        ShowToast("COPIED TO CLIPBOARD", Color.FromArgb(0, 255, 200));
                    }
                    catch { ShowToast("FAILED TO COPY", Color.FromArgb(255, 200, 0)); }

                    break;

                case 1:
                    editor.SwitchWindow(new GuiWindowMenu());

                    break;

                case 2:
                    if (editor.MusicPlayer.IsPlaying)
                        editor.MusicPlayer.Pause();
                    else
                    {
                        if (currentTime.Value >= currentTime.Max - 1)
                            currentTime.Value = 0;
                        editor.MusicPlayer.Play();
                    }

                    break;

                case 3:
                    navEnabled = navEnabled == "Options" ? "" : "Options";
                    UpdateNav();

                    break;

                case 4:
                    navEnabled = navEnabled == "Timing" ? "" : "Timing";
                    UpdateNav();

                    break;

                case 5:
                    ExportOffset.Text = ((long)currentTime.Value).ToString();
                    Settings.settings["exportOffset"] = currentTime.Value;

                    break;

                case 6:
                    TimingsWindow.ShowWindow();

                    break;

                case 7:
                    BookmarksWindow.ShowWindow();

                    break;

                case 8:
                    navEnabled = navEnabled == "Patterns" ? "" : "Patterns";
                    UpdateNav();

                    break;

                case 9:
                    var selectedM = editor.SelectedNotes.ToList();

                    if (selectedM.Count > 0)
                    {
                        editor.UndoRedoManager.Add("MIRROR", () =>
                        {
                            foreach (var note in selectedM)
                                note.X = 4 - note.X;
                        }, () =>
                        {
                            foreach (var note in selectedM)
                                note.X = 4 - note.X;
                        });
                    }

                    break;

                case 10:
                    var result = editor.TweenSelected();

                    if (result != null && result.Count > 0)
                    {
                        var notes = new List<Note>();

                        for (int i = 0; i < result.Count; i++)
                            notes.Add(new(result[i].X, (long)result[i].Y));

                        editor.SelectedNotes.Clear();
                        editor.UpdateSelection();
                        editor.SelectedPoint = null;

                        editor.UndoRedoManager.Add("TWEEN", () =>
                        {
                            foreach (var note in notes)
                                editor.Notes.Remove(note);

                            editor.SortNotes();
                        }, () =>
                        {
                            editor.Notes.AddRange(notes);

                            editor.SortNotes();
                        });
                    }

                    break;

                case 15:
                    var selectedS = editor.SelectedNotes.ToList();

                    if (float.TryParse(ScaleBox.Text, out var scale) && selectedS.Count > 0)
                    {
                        var scalef = scale / 100f;

                        var values = new Vector2[selectedS.Count];
                        for (int i = 0; i < values.Length; i++)
                        {
                            var old = selectedS[i].X;
                            var cur = (old - 2) * scalef + 2;

                            values[i] = (MathHelper.Clamp(cur, MainWindow.Bounds.X, MainWindow.Bounds.Y), old);
                        }

                        editor.UndoRedoManager.Add($"SCALE {scale}%", () =>
                        {
                            for (int i = 0; i < selectedS.Count; i++)
                                selectedS[i].X = values[i].Y;
                        }, () =>
                        {
                            for (int i = 0; i < selectedS.Count; i++)
                                selectedS[i].X = values[i].X;
                        });
                    }

                    break;

                case 16:
                    editor.ImportProperties();

                    break;

                case 19:
                    navEnabled = navEnabled == "Review" ? "" : "Review";
                    UpdateNav();

                    break;

                case 20:
                    editor.CopyBookmarks();

                    break;

                case 21:
                    editor.PasteBookmarks();

                    break;

                case 24:
                    if (editor.SaveMap(true))
                        ShowToast("SAVED", Settings.settings["color1"]);

                    break;
            }

            base.OnButtonClicked(id);
        }

        public override void OnMouseClick(Point pos, bool right = false)
        {
            if (Timeline.HoveringBookmark != null && !right)
            {
                MainWindow.Instance.MusicPlayer.Pause();
                Settings.settings["currentTime"].Value = Timeline.HoveringBookmark.Ms;
            }

            base.OnMouseClick(pos, right);
        }

        public void Update()
        {
            Timeline.Update();
            Track?.Update();
        }

        private void UpdateNav()
        {
            var optionsNav = navEnabled == "Options";
            var timingNav = navEnabled == "Timing";
            var patternsNav = navEnabled == "Patterns";
            var reviewNav = navEnabled == "Review";

            OptionsNav.Text = $"OPTIONS {(optionsNav ? "<" : ">")}";
            TimingNav.Text = $"TIMING {(timingNav ? "<" : ">")}";
            PatternsNav.Text = $"PATTERNS {(patternsNav ? "<" : ">")}";
            ReviewNav.Text = $"REVIEW {(reviewNav ? "<" : ">")}";

            Autoplay.Visible = optionsNav;
            ApproachSquares.Visible = optionsNav;
            GridNumbers.Visible = optionsNav;
            GridLetters.Visible = optionsNav;
            Quantum.Visible = optionsNav;
            QuantumGridLines.Visible = optionsNav;
            QuantumGridSnap.Visible = optionsNav;
            Metronome.Visible = optionsNav;
            SeparateClickTools.Visible = optionsNav;
            JumpOnPaste.Visible = optionsNav;
            TrackHeight.Visible = optionsNav;
            TrackCursorPos.Visible = optionsNav;
            TrackHeightLabel.Visible = optionsNav;
            CursorPosLabel.Visible = optionsNav;

            ExportOffset.Visible = timingNav;
            SfxOffset.Visible = timingNav;
            UseCurrentMs.Visible = timingNav;
            OpenTimings.Visible = timingNav;
            ImportIni.Visible = timingNav;
            ExportOffsetLabel.Visible = timingNav;
            SfxOffsetLabel.Visible = timingNav;

            Mirror.Visible = patternsNav;
            ScaleBox.Visible = patternsNav;
            ScaleButton.Visible = patternsNav;
            ScaleLabel.Visible = patternsNav;
            ApplyOnPaste.Visible = patternsNav;
            TweenDivisor.Visible = patternsNav;
            TweenButton.Visible = patternsNav;
            TweenFunction.Visible = patternsNav;
            TweenMode.Visible = patternsNav;
            VisualizeTween.Visible = patternsNav;
            TweenDivisorLabel.Visible = patternsNav;

            OpenBookmarks.Visible = reviewNav;
            CopyBookmarks.Visible = reviewNav;
            PasteBookmarks.Visible = reviewNav;

            OnResize(new Vector2i((int)Rect.Width, (int)Rect.Height));
        }

        public override void OnResize(Vector2i size)
        {
            Rect = new RectangleF(0, 0, size.X, size.Y);

            base.OnResize(size);

            var heightdiff = size.Y / 1080f;

            switch (navEnabled)
            {
                case "Options":
                    TimingNav.Rect.Y = TrackCursorPos.Rect.Bottom + 20 * heightdiff;
                    PatternsNav.Rect.Y = TimingNav.Rect.Bottom + 10 * heightdiff;
                    ReviewNav.Rect.Y = PatternsNav.Rect.Bottom + 10 * heightdiff;
                    break;

                case "Timing":
                    PatternsNav.Rect.Y = ImportIni.Rect.Bottom + 20 * heightdiff;
                    ReviewNav.Rect.Y = PatternsNav.Rect.Bottom + 10 * heightdiff;
                    break;

                case "Patterns":
                    ReviewNav.Rect.Y = VisualizeTween.Rect.Bottom + 20 * heightdiff;
                    break;
            }


            CopyButton.Rect.Location = new PointF(Grid.Rect.X, Grid.Rect.Bottom + 8);
            BackButton.Rect.Location = new PointF(Grid.Rect.X, CopyButton.Rect.Bottom + 8);
            SaveButton.Rect.Location = new PointF(BackButton.Rect.Right + 5, BackButton.Rect.Y);
            ClickModeLabel.Rect.Location = new PointF(Grid.Rect.X, BackButton.Rect.Bottom + 10 * heightdiff);

            Timeline.Rect = new RectangleF(0, Rect.Height - 64f, Rect.Width - 576f, 64f);
            PlayPause.Rect = new RectangleF(Rect.Width - 576f, Rect.Height - 64f, 64f, 64f);
            Tempo.Rect = new RectangleF(Rect.Width - 512f, Rect.Height - 64f, 512f, 64f);

            AutoAdvance.TextSize = AutoAdvance.OriginTextSize;
            AutoAdvance.Rect = new RectangleF(Rect.Width - 236f, Rect.Height / 2f - 220f, 40f, 40f);
            BeatSnapDivisor.Rect = new RectangleF(Rect.Width - 256f, AutoAdvance.Rect.Y + 98f, 256f, 40f);
            QuantumSnapDivisor.Rect = new RectangleF(Rect.Width - 256f, BeatSnapDivisor.Rect.Y + 72f, 256f, 40f);
            MasterVolume.Rect = new RectangleF(Rect.Width - 64f, Rect.Height - 320f, 40f, 256f);
            SfxVolume.Rect = new RectangleF(Rect.Width - 128f, Rect.Height - 320f, 40f, 256f);

            BeatDivisorLabel.Rect.Location = new PointF(BeatSnapDivisor.Rect.X + BeatSnapDivisor.Rect.Width / 2f, BeatSnapDivisor.Rect.Y - 20f);
            SnappingLabel.Rect.Location = new PointF(QuantumSnapDivisor.Rect.X + QuantumSnapDivisor.Rect.Width / 2f, QuantumSnapDivisor.Rect.Y - 20f);
            TempoLabel.Rect.Location = new PointF(Tempo.Rect.X + Tempo.Rect.Width / 2f, Tempo.Rect.Bottom - 28f);
            MusicLabel.Rect.Location = new PointF(MasterVolume.Rect.X + MasterVolume.Rect.Width / 2f, MasterVolume.Rect.Y - 6f);
            SfxLabel.Rect.Location = new PointF(SfxVolume.Rect.X + SfxVolume.Rect.Width / 2f, SfxVolume.Rect.Y - 6f);
            MusicValueLabel.Rect.Location = new PointF(MasterVolume.Rect.X + MasterVolume.Rect.Width / 2f, MasterVolume.Rect.Bottom - 20f);
            SfxValueLabel.Rect.Location = new PointF(SfxVolume.Rect.X + SfxVolume.Rect.Width / 2f, SfxVolume.Rect.Bottom - 20f);

            var currentTime = Settings.settings["currentTime"];
            var progress = currentTime.Value / currentTime.Max;

            CurrentTimeLabel.Rect.Location = new PointF(Timeline.Rect.X + Timeline.Rect.Height / 2f, Timeline.Rect.Bottom - 28f);
            CurrentMsLabel.Rect.Location = new PointF(Timeline.Rect.X + Timeline.Rect.Height / 2f + (Timeline.Rect.Width - Timeline.Rect.Height) * progress, Timeline.Rect.Y - 4f);
            TotalTimeLabel.Rect.Location = new PointF(Timeline.Rect.X - Timeline.Rect.Height / 2f + Timeline.Rect.Width, Timeline.Rect.Bottom - 28f);
            NotesLabel.Rect.Location = new PointF(Timeline.Rect.X + Timeline.Rect.Width / 2f, Timeline.Rect.Bottom - 28f);

            TimingNav.Update();
            PatternsNav.Update();
            ReviewNav.Update();

            CopyButton.Update();
            BackButton.Update();
            SaveButton.Update();
            ClickModeLabel.Update();

            Timeline.Update();
            PlayPause.Update();
            Tempo.Update();

            AutoAdvance.Update();
            BeatSnapDivisor.Update();
            QuantumSnapDivisor.Update();
            MasterVolume.Update();
            SfxVolume.Update();

            BeatDivisorLabel.Update();
            SnappingLabel.Update();
            TempoLabel.Update();
            MusicLabel.Update();
            SfxLabel.Update();
            MusicValueLabel.Update();
            SfxValueLabel.Update();

            CurrentTimeLabel.Update();
            CurrentMsLabel.Update();
            TotalTimeLabel.Update();
            NotesLabel.Update();
        }

        public void ShowToast(string text, Color color)
        {
            toastTime = 0f;

            ToastLabel.Text = text;
            ToastLabel.Color = color;
        }
    }
}
