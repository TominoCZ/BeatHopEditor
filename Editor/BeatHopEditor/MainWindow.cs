using OpenTK.Graphics.OpenGL;
using BeatHopEditor.GUI;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Json;
using System.Reflection;
using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Drawing;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Desktop;
using OpenTK.Mathematics;
using SkiaSharp;
//using Discord;
//using Activity = Discord.Activity;
using MouseButton = OpenTK.Windowing.GraphicsLibraryFramework.MouseButton;
using OpenTK.Windowing.Common.Input;
using System.Runtime.InteropServices;
using BigInteger = System.Numerics.BigInteger;
using System.IO.Compression;
using OpenTK.Graphics;
using BeatHopEditor.Types;
using Microsoft.VisualBasic.FileIO;

namespace BeatHopEditor
{
    internal class MainWindow : GameWindow
    {
        public static readonly Vector2 Bounds = new(0, 4);

        public static MainWindow Instance;
        public MusicPlayer MusicPlayer = new();
        public SoundPlayer SoundPlayer = new();
        public UndoRedoManager UndoRedoManager = new();

        public GuiWindow CurrentWindow;

        public List<Map> Maps = new();
        public Map CurrentMap;
        private Map prevMap;

        public List<Note> Notes = new();
        public List<Note> SelectedNotes = new();

        public List<TimingPoint> TimingPoints = new();
        public TimingPoint? SelectedPoint;

        public List<Bookmark> Bookmarks = new();

        public readonly Dictionary<Keys, int> KeyMapping = new();

        public Point Mouse = new(-1, -1);

        public float Tempo = 1f;
        public float Zoom = 1f;
        public float NoteStep => 500f * Zoom;
        public float GridStep => 1600f / Zoom;

        public bool CtrlHeld;
        public bool AltHeld;
        public bool ShiftHeld;
        public bool RightHeld;

        public string? FileName;
        public string SoundID = "-1";

        public static Avalonia.Controls.Window DefaultWindow = new BackgroundWindow();

        //private Discord.Discord discord;
        //private ActivityManager activityManager;
        //private bool discordEnabled = File.Exists("discord_game_sdk.dll");



        private bool closing = false;

        // hacky workaround for fullscreen being awful
        private bool isFullscreen = false;
        private Vector2i startSize = new(1920, 1080);

        private void SwitchFullscreen()
        {
            isFullscreen ^= true;

            WindowState = isFullscreen ? WindowState.Normal : WindowState.Maximized;
            WindowBorder = isFullscreen ? WindowBorder.Hidden : WindowBorder.Resizable;

            if (isFullscreen)
            {
                Size = startSize;
                Location = (0, 0);
            }
        }



        private static WindowIcon GetWindowIcon()
        {
            var bytes = File.ReadAllBytes("assets/textures/Icon.ico");
            var bmp = SKBitmap.Decode(bytes, new SKImageInfo(256, 256, SKColorType.Rgba8888));
            var image = new OpenTK.Windowing.Common.Input.Image(bmp.Width, bmp.Height, bmp.Bytes);

            return new WindowIcon(image);
        }

        private const string cacheFile = "assets/temp/cache.txt";

        public MainWindow() : base(GameWindowSettings.Default, new NativeWindowSettings()
        {
            Size = (1280, 720),
            Title = $"Beat Hop Map Editor {Assembly.GetExecutingAssembly().GetName().Version}",
            NumberOfSamples = 32,
            WindowState = WindowState.Maximized,
            Icon = GetWindowIcon(),
            Flags = ContextFlags.Debug,

            APIVersion = new Version(3, 3)
        })
        {
            ActionLogging.Register("Required OpenGL version: 3.3");
            ActionLogging.Register("Current OpenGL version: " + (GL.GetString(StringName.Version) ?? "N/A"));

            string version = GL.GetString(StringName.Version) ?? "";
            string sub = version[..version.IndexOf(" ")];

            if (string.IsNullOrWhiteSpace(version) || Version.Parse(sub) < APIVersion)
                throw new Exception("Unsupported OpenGL version (Minimum: 3.3)");

            Shader.Init();

            Instance = this;

            //DiscordInit();
            //SetActivity("Sitting in the menu");

            Settings.Load();

            CheckForUpdates();

            if (File.Exists(cacheFile) && !string.IsNullOrWhiteSpace(File.ReadAllText(cacheFile)))
                LoadCache();

            OnMouseWheel(new MouseWheelEventArgs());
            SwitchWindow(new GuiWindowMenu());
        }

        public void SetVSync(VSyncMode mode)
        {
            if (Context.IsCurrent)
                VSync = mode;
        }

        protected override void OnLoad()
        {
            GL.ClearColor(0f, 0f, 0f, 1f);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            if (closing)
                return;

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            if (MusicPlayer.IsPlaying && CurrentWindow is GuiWindowEditor)
                Settings.settings["currentTime"].Value = (float)MusicPlayer.CurrentTime.TotalMilliseconds;

            var mouse = MouseState;
            if (mouse.Delta.Length != 0)
                CurrentWindow?.OnMouseMove(Mouse);
            Mouse.X = (int)mouse.X;
            Mouse.Y = (int)mouse.Y;

            try
            {
                CurrentWindow?.Render(Mouse.X, Mouse.Y, (float)args.Time);
            }
            catch (Exception ex)
            {
                ActionLogging.Register($"Failed to render frame - {ex.GetType().Name}\n{ex.StackTrace}", "ERROR");
            }

            if (CurrentMap != prevMap && CurrentWindow is GuiWindowEditor editor)
            {
                editor.Timeline.GenerateOffsets();
                prevMap = CurrentMap;
            }

            GL.BindBuffer(BufferTargetARB.ArrayBuffer, BufferHandle.Zero);
            GL.BindVertexArray(VertexArrayHandle.Zero);

            var err = GL.GetError();
            if (err != OpenTK.Graphics.OpenGL.ErrorCode.NoError)
                ActionLogging.Register($"OpenGL Error: '{err}'", "WARN");

            SwapBuffers();
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            var w = Math.Max(e.Width, 800);
            var h = Math.Max(e.Height, 600);
            Size = new Vector2i(w, h);

            base.OnResize(new ResizeEventArgs(w, h));
            GL.Viewport(0, 0, w, h);

            Shader.UploadOrtho(Shader.Program, w, h);
            Shader.UploadOrtho(Shader.TexProgram, w, h);
            Shader.UploadOrtho(Shader.FontTexProgram, w, h);
            Shader.UploadOrtho(Shader.NoteInstancedProgram, w, h);
            Shader.UploadOrtho(Shader.InstancedProgram, w, h);
            Shader.UploadOrtho(Shader.GridInstancedProgram, w, h);
            Shader.UploadOrtho(Shader.WaveformProgram, w, h);

            CurrentWindow?.OnResize(Size);

            OnRenderFrame(new FrameEventArgs());
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            CurrentWindow?.OnMouseUp(Mouse);
            if (e.Button == MouseButton.Right)
                RightHeld = false;
        }

        private bool ClickLocked = false;

        public void LockClick() => ClickLocked = true;
        public void UnlockClick() => ClickLocked = false;

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (!ClickLocked)
                CurrentWindow?.OnMouseClick(Mouse, e.Button == MouseButton.Right);
            RightHeld = RightHeld || e.Button == MouseButton.Right;
        }

        protected override void OnMouseLeave()
        {
            CurrentWindow?.OnMouseLeave(Mouse);

            Mouse = new Point(-1, -1);
        }

        protected override void OnKeyUp(KeyboardKeyEventArgs e)
        {
            CtrlHeld = e.Control;
            AltHeld = e.Alt;
            ShiftHeld = e.Shift;
        }

        protected override void OnKeyDown(KeyboardKeyEventArgs e)
        {
            CtrlHeld = e.Control;
            AltHeld = e.Alt;
            ShiftHeld = e.Shift;

            if (e.Key == Keys.F11)
            {
                SwitchFullscreen();
                return;
            }

            if (e.Key == Keys.F4 && AltHeld)
                Close();

            if (!FocusingBox())
            {
                if (CurrentWindow is GuiWindowEditor editor)
                {
                    if (e.Key == Keys.Space && !editor.Timeline.Dragging)
                    {
                        if (MusicPlayer.IsPlaying)
                            MusicPlayer.Pause();
                        else
                        {
                            var currentTime = Settings.settings["currentTime"];

                            if (currentTime.Value >= currentTime.Max - 1)
                                currentTime.Value = 0;

                            MusicPlayer.Play();
                        }
                    }

                    if (e.Key == Keys.Left || e.Key == Keys.Right)
                    {
                        if (MusicPlayer.IsPlaying)
                            MusicPlayer.Pause();

                        Advance(e.Key == Keys.Left);
                    }

                    if (e.Key == Keys.Escape)
                    {
                        SelectedNotes.Clear();
                        UpdateSelection();
                        SelectedPoint = null;
                    }

                    var keybind = Settings.CompareKeybind(e.Key, CtrlHeld, AltHeld, ShiftHeld);

                    if (keybind.Contains("gridKey"))
                    {
                        var rep = keybind.Replace("gridKey", "");

                        var x = int.Parse(rep);
                        var ms = GetClosestBeat(Settings.settings["currentTime"].Value);

                        var note = new Note(x, (long)(ms >= 0 ? ms : Settings.settings["currentTime"].Value));

                        UndoRedoManager.Add("ADD NOTE", () =>
                        {
                            Notes.Remove(note);
                            SortNotes();
                        }, () =>
                        {
                            Notes.Add(note);
                            SortNotes();
                        });

                        if (Settings.settings["autoAdvance"])
                            Advance();

                        return;
                    }

                    if (keybind.Contains("pattern"))
                    {
                        var index = int.Parse(keybind.Replace("pattern", ""));

                        if (ShiftHeld)
                            BindPattern(index);
                        else if (CtrlHeld)
                            UnbindPattern(index);
                        else
                            CreatePattern(index);

                        return;
                    }

                    switch (keybind)
                    {
                        case "selectAll":
                            SelectedPoint = null;
                            SelectedNotes = Notes.ToList();
                            UpdateSelection();

                            break;

                        case "save":
                            if (SaveMap(true))
                                editor.ShowToast("SAVED", Settings.settings["color1"]);

                            break;

                        case "saveAs":
                            if (SaveMap(true, true))
                                editor.ShowToast("SAVED", Settings.settings["color1"]);

                            break;

                        case "undo":
                            UndoRedoManager.Undo();

                            break;

                        case "redo":
                            UndoRedoManager.Redo();

                            break;

                        case "copy":
                            try
                            {
                                if (SelectedNotes.Count > 0)
                                {
                                    var copied = SelectedNotes.ToList();

                                    Clipboard.SetData(copied);

                                    editor.ShowToast("COPIED NOTES", Settings.settings["color1"]);
                                }
                            }
                            catch { editor.ShowToast("FAILED TO COPY", Settings.settings["color1"]); }

                            break;

                        case "paste":
                            try
                            {
                                var copied = Clipboard.GetData().ToList();

                                if (copied.Count > 0)
                                {
                                    var offset = copied.Min(n => n.Ms);

                                    copied.ForEach(n => n.Ms = (long)Settings.settings["currentTime"].Value + n.Ms - offset);

                                    if (Settings.settings["applyOnPaste"])
                                    {
                                        if (float.TryParse(editor.ScaleBox.Text, out var scale))
                                        {
                                            var scalef = scale / 100f;

                                            foreach (var note in copied.ToList())
                                                note.X = MathHelper.Clamp((note.X - 2) * scalef + 2, Bounds.X, Bounds.Y);
                                        }
                                    }

                                    UndoRedoManager.Add($"PASTE NOTE{(copied.Count > 0 ? "S" : "")}", () =>
                                    {
                                        SelectedNotes.Clear();
                                        SelectedPoint = null;

                                        for (int i = 0; i < copied.Count; i++)
                                            Notes.Remove(copied[i]);
                                        UpdateSelection();

                                        SortNotes();
                                    }, () =>
                                    {
                                        SelectedNotes = copied.ToList();
                                        SelectedPoint = null;

                                        Notes.AddRange(copied);
                                        UpdateSelection();

                                        SortNotes();
                                    });
                                }
                            }
                            catch { editor.ShowToast("FAILED TO PASTE", Settings.settings["color1"]); }

                            break;

                        case "cut":
                            try
                            {
                                if (SelectedNotes.Count > 0)
                                {
                                    var copied = SelectedNotes.ToList();

                                    Clipboard.SetData(copied);

                                    UndoRedoManager.Add($"CUT NOTE{(copied.Count > 1 ? "S" : "")}", () =>
                                    {
                                        Notes.AddRange(copied);

                                        SortNotes();
                                    }, () =>
                                    {
                                        foreach (var note in copied)
                                            Notes.Remove(note);

                                        SortNotes();
                                    });

                                    SelectedNotes.Clear();
                                    UpdateSelection();
                                    SelectedPoint = null;

                                    editor.ShowToast("CUT NOTES", Settings.settings["color1"]);
                                }
                            }
                            catch { editor.ShowToast("FAILED TO CUT", Settings.settings["color1"]); }

                            break;

                        case "delete":
                            if (SelectedNotes.Count > 0)
                            {
                                var toRemove = SelectedNotes.ToList();

                                UndoRedoManager.Add($"DELETE NOTE{(toRemove.Count > 1 ? "S" : "")}", () =>
                                {
                                    Notes.AddRange(toRemove);

                                    SortNotes();
                                }, () =>
                                {
                                    foreach (var note in toRemove)
                                        Notes.Remove(note);

                                    SortNotes();
                                });

                                SelectedNotes.Clear();
                                UpdateSelection();
                                SelectedPoint = null;
                            }
                            else if (SelectedPoint != null)
                            {
                                var clone = SelectedPoint;

                                UndoRedoManager.Add("DELETE POINT", () =>
                                {
                                    TimingPoints.Add(clone);

                                    SortTimings();
                                }, () =>
                                {
                                    TimingPoints.Remove(clone);

                                    SortTimings();
                                });
                            }

                            break;

                        case "hFlip":
                            var selectedH = SelectedNotes.ToList();

                            editor.ShowToast("MIRROR", Settings.settings["color1"]);

                            UndoRedoManager.Add("MIRROR", () =>
                            {
                                foreach (var note in selectedH)
                                    note.X = 4 - note.X;
                            }, () =>
                            {
                                foreach (var note in selectedH)
                                    note.X = 4 - note.X;
                            });

                            break;

                        case "switchClickTool":
                            Settings.settings["selectTool"] ^= true;

                            break;

                        case "quantum":
                            Settings.settings["enableQuantum"] ^= true;

                            break;

                        case "openTimings":
                            TimingsWindow.ShowWindow();

                            break;

                        case "openBookmarks":
                            BookmarksWindow.ShowWindow();

                            break;

                        case "openDirectory":
                            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                                // if mac
                                Process.Start("open", $"\"{Environment.CurrentDirectory}\"");
                            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                                // if windows
                                Process.Start("explorer.exe", Environment.CurrentDirectory);
                            else // linux probably
                                ActionLogging.Register($"Open dir not implemented on platform {RuntimeInformation.OSDescription}", "WARN");

                            break;
                    }
                }
            }

            CurrentWindow?.OnKeyDown(e.Key, e.Control);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            var keyboard = KeyboardState;

            CtrlHeld = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
            AltHeld = keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt);
            ShiftHeld = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

            if (CurrentWindow is GuiWindowEditor)
            {
                if (ShiftHeld)
                {
                    var setting = Settings.settings["beatDivisor"];
                    var step = setting.Step * (CtrlHeld ? 1 : 2) * e.OffsetY;

                    setting.Value = MathHelper.Clamp(setting.Value + step, 0f, setting.Max);
                }
                else if (CtrlHeld)
                {
                    var step = Zoom < 0.1f || (Zoom == 0.1f && e.OffsetY < 0) ? 0.01f : 0.1f;

                    Zoom = (float)Math.Round(Zoom + e.OffsetY * step, 2);
                    if (Zoom > 0.1f)
                        Zoom = (float)Math.Round(Zoom * 10) / 10;

                    Zoom = MathHelper.Clamp(Zoom, 0.01f, 10f);
                }
                else
                {
                    if (MusicPlayer.IsPlaying)
                        MusicPlayer.Pause();

                    float delta = e.OffsetY * (Settings.settings["reverseScroll"] ? -1 : 1);

                    var setting = Settings.settings["currentTime"];
                    var currentTime = setting.Value;
                    var totalTime = setting.Max;

                    var closest = GetClosestBeatScroll(currentTime, delta < 0);
                    var bpm = GetCurrentBpm(0);

                    currentTime = closest >= 0 || bpm.BPM > 0 ? closest : currentTime + delta / 10f * 1000f / Zoom * 0.5f;

                    if (GetCurrentBpm(setting.Value).BPM == 0 && GetCurrentBpm(currentTime).BPM != 0)
                        currentTime = GetCurrentBpm(currentTime).Ms;

                    currentTime = MathHelper.Clamp(currentTime, 0f, totalTime);

                    setting.Value = currentTime;
                }
            }
            else if (CurrentWindow is GuiWindowMenu menu)
            {
                if (menu.MapSelectBackdrop.Rect.Contains(Mouse))
                    menu.ScrollMaps(e.OffsetY > 0);
                else
                {
                    var setting = Settings.settings["changelogPosition"];

                    setting.Value = MathHelper.Clamp(setting.Value + setting.Step * e.OffsetY, 0f, setting.Max);
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            closing = true;

            bool cancel = false;

            Map temp = CurrentMap;

            List<Map> tempSave = new();
            List<Map> tempKeep = new();

            foreach (Map map in Maps.ToList())
            {
                if (map.IsSaved())
                    tempKeep.Add(map);
                else
                    tempSave.Add(map);
            }

            foreach (Map map in tempSave)
                cancel |= !map.Close(false);

            if (!cancel)
            {
                foreach (Map map in tempKeep)
                    map.Close(false, false, false);
            }
            else
                temp?.MakeCurrent();

            e.Cancel = cancel;

            if (CurrentWindow is GuiWindowMenu menu)
                menu.AssembleMapList();

            Settings.Save();

            TimingsWindow.Instance?.Close();
            BookmarksWindow.Instance?.Close();

            if (!e.Cancel)
            {
                MusicPlayer.Dispose();
                CurrentWindow?.Dispose();
            }

            closing = !e.Cancel;
        }








        public Vector2 PointToGridSpace(float mousex, float mousey)
        {
            if (CurrentWindow is GuiWindowEditor editor)
            {
                var quantum = Settings.settings["enableQuantum"];
                var rect = editor.Grid.Rect;

                var increment = quantum ? 1f / (Settings.settings["quantumSnapping"].Value + 1f) : 1f;
                var x = (mousex - rect.X - rect.Width / 10f) / rect.Width * 5f;

                if (Settings.settings["quantumGridSnap"])
                    x = (float)Math.Floor((x + increment / 2) / increment) * increment;

                var ms = ((2 / 3f) - mousey / rect.Y * (2 / 3f)) * GridStep + Settings.settings["currentTime"].Value;
                if (mousey > rect.Y)
                    ms = Settings.settings["currentTime"].Value;

                return ((float)MathHelper.Clamp(x, Bounds.X, Bounds.Y), GetClosestBeat(ms));
            }

            return (0, 0);
        }

        public void UpdateSelection()
        {
            for (int i = 0; i < Notes.Count; i++)
                Notes[i].Selected = false;
            for (int i = 0; i < SelectedNotes.Count; i++)
                SelectedNotes[i].Selected = true;
        }

        public long GetClosestNote(float currentMs)
        {
            long closestMs = -1;

            for (int i = 0; i < Notes.Count; i++)
            {
                var note = Notes[i];

                if (Math.Abs(note.Ms - currentMs) < Math.Abs(closestMs - currentMs))
                    closestMs = note.Ms;
            }

            return closestMs;
        }

        public long GetClosestBeat(float currentMs, bool draggingPoint = false)
        {
            long closestMs = -1;
            var point = GetCurrentBpm(currentMs, draggingPoint);

            if (point.BPM > 0)
            {
                var interval = 60 / point.BPM * 1000f / (Settings.settings["beatDivisor"].Value + 1f);
                var offset = point.Ms % interval;

                closestMs = (long)Math.Round((long)Math.Round((currentMs - offset) / interval) * interval + offset);
            }

            return (long)Math.Min(closestMs, Settings.settings["currentTime"].Max);
        }

        public long GetClosestBeatScroll(float currentMs, bool negative = false, int iterations = 1)
        {
            var closestMs = GetClosestBeat(currentMs);

            if (GetCurrentBpm(closestMs).BPM == 0)
                return -1;

            for (int i = 0; i < iterations; i++)
            {
                var currentPoint = GetCurrentBpm(currentMs, negative);
                var interval = 60000 / currentPoint.BPM / (Settings.settings["beatDivisor"].Value + 1f);

                if (negative)
                {
                    closestMs = GetClosestBeat(currentMs, true);

                    if (closestMs >= currentMs)
                        closestMs = GetClosestBeat(closestMs - (long)interval);
                }
                else
                {
                    if (closestMs <= currentMs)
                        closestMs = GetClosestBeat(closestMs + (long)interval);

                    if (GetCurrentBpm(currentMs).Ms != GetCurrentBpm(closestMs).Ms)
                        closestMs = GetCurrentBpm(closestMs, false).Ms;
                }

                currentMs = closestMs;
            }

            if (closestMs < 0)
                return -1;

            return (long)MathHelper.Clamp(closestMs, 0, Settings.settings["currentTime"].Max);
        }

        public TimingPoint GetCurrentBpm(float currentMs, bool draggingPoint = false)
        {
            var currentPoint = new TimingPoint(0, 0);

            for (int i = 0; i < TimingPoints.Count; i++)
            {
                var point = TimingPoints[i];

                if (point.Ms < currentMs || (!draggingPoint && point.Ms == currentMs))
                    currentPoint = point;
            }

            return currentPoint;
        }

        public void Advance(bool reverse = false)
        {
            var currentMs = Settings.settings["currentTime"];
            var bpm = GetCurrentBpm(currentMs.Value).BPM;

            if (bpm > 0)
                currentMs.Value = GetClosestBeatScroll(currentMs.Value, reverse);
        }




        public void BindPattern(int index)
        {
            var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = ".";

            string pattern = "";
            long minDist = 0;

            for (int i = 0; i + 1 < SelectedNotes.Count; i++)
            {
                var dist = Math.Abs(SelectedNotes[i].Ms - SelectedNotes[i + 1].Ms);

                if (dist > 0)
                    minDist = minDist > 0 ? Math.Min(minDist, dist) : dist;
            }

            foreach (var note in SelectedNotes)
            {
                var offset = SelectedNotes[0].Ms;

                var x = note.X.ToString(culture);
                var time = (minDist > 0 ? Math.Round((double)(note.Ms - offset) / minDist) : 0).ToString(culture);

                pattern += $",{x}|{time}";
            }

            if (pattern.Length > 0)
                pattern = pattern[1..];

            if (CurrentWindow is GuiWindowEditor editor)
                editor.ShowToast($"BOUND PATTERN TO KEY {index}", Settings.settings["color1"]);

            Settings.settings["patterns"][index] = pattern;
        }

        public void UnbindPattern(int index)
        {
            if (CurrentWindow is GuiWindowEditor editor)
            {
                Settings.settings["patterns"][index] = "";
                editor.ShowToast($"UNBOUND PATTERN {index}", Settings.settings["color1"]);
            }
        }

        public void CreatePattern(int index)
        {
            var pattern = Settings.settings["patterns"][index];

            if (pattern == "")
                return;

            var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = ".";

            string[] patternSplit = pattern.split(',');
            var toAdd = new List<Note>();

            foreach (var note in patternSplit)
            {
                string[] noteSplit = note.Split('|');
                var x = float.Parse(noteSplit[0], culture);
                var y = float.Parse(noteSplit[1], culture);
                var time = int.Parse(noteSplit[2], culture);
                var ms = GetClosestBeatScroll((long)Settings.settings["currentTime"].Value, false, time);

                toAdd.Add(new Note(x, ms));
            }

            UndoRedoManager.Add("ADD PATTERN", () =>
            {
                foreach (var note in toAdd)
                    Notes.Remove(note);

                SortNotes();
            }, () =>
            {
                Notes.AddRange(toAdd);

                SortNotes();
            });
        }




        public bool PromptImport(string id, bool create = false)
        {
            var dialog = new OpenFileDialog()
            {
                Title = "Select Audio File",
                Filter = "Audio Files (*.mp3;*.ogg;*.wav;*.flac;*.egg;*.m4a;*.asset)|*.mp3;*.ogg;*.wav;*.flac;*.egg;*.m4a;*.asset"
            };

            if (Settings.settings["audioPath"] != "")
                dialog.InitialDirectory = Settings.settings["audioPath"];

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                Settings.settings["audioPath"] = Path.GetDirectoryName(dialog.FileName) ?? "";
                if (string.IsNullOrWhiteSpace(id))
                    id = Path.GetFileNameWithoutExtension(dialog.FileName);

                if (dialog.FileName != $"{Directory.GetCurrentDirectory()}\\cached\\{id}.asset")
                    File.Copy(dialog.FileName, $"cached/{id}.asset", true);

                MusicPlayer.Load($"cached/{id}.asset");
                if (create)
                    SoundID = id;

                return true;
            }

            return false;
        }

        public bool LoadAudio(string id)
        {
            try
            {
                if (!Directory.Exists("cached/"))
                    Directory.CreateDirectory("cached/");

                if (!File.Exists($"cached/{id}.asset"))
                {
                    if (Settings.settings["skipDownload"])
                    {
                        var message = MessageBox.Show($"No asset with id '{id}' is present in cache.\n\nWould you like to import a file with this id?", "Warning", "OK", "Cancel");

                        return message == DialogResult.OK && PromptImport(id);
                    }
                    else
                        WebClient.DownloadFile($"https://assetdelivery.roblox.com/v1/asset/?id={id}", $"cached/{id}.asset", true);
                }

                MusicPlayer.Load($"cached/{id}.asset");

                return true;
            }
            catch (Exception e)
            {
                var message = MessageBox.Show($"Failed to download asset with id '{id}':\n\n{e.Message}\n\nWould you like to import a file with this id instead?", "Error", "OK", "Cancel");

                if (message == DialogResult.OK)
                    return PromptImport(id);
            }

            return false;
        }

        public string ParseProperties()
        {
            var timingfinal = new JsonArray();

            foreach (var point in TimingPoints)
                timingfinal.Add(new JsonArray() { point.BPM, point.Ms });

            var bookmarkfinal = new JsonArray();

            foreach (var bookmark in Bookmarks)
                bookmarkfinal.Add(new JsonArray() { bookmark.Text, bookmark.Ms, bookmark.EndMs });

            var json = new JsonObject(Array.Empty<KeyValuePair<string, JsonValue>>())
            {
                {"timings", timingfinal },
                {"bookmarks", bookmarkfinal },
                {"currentTime", Settings.settings["currentTime"].Value },
                {"beatDivisor", Settings.settings["beatDivisor"].Value },
                {"exportOffset", Settings.settings["exportOffset"] }
            };

            return json.ToString();
        }

        public bool IsSaved()
        {
            return FileName != null && File.Exists(FileName) && File.ReadAllText(FileName) == Map.Save(SoundID, Notes);
        }

        public bool SaveMap(bool forced, bool fileForced = false, bool reload = true)
        {
            if (FileName != null && !File.Exists(FileName))
                FileName = null;

            if (FileName != null)
                Settings.settings["lastFile"] = FileName;
            Settings.Save(reload);

            var data = Map.Save(SoundID, Notes);

            if (forced || (FileName == null && (Notes.Count > 0 || TimingPoints.Count > 0)) || (FileName != null && data != File.ReadAllText(FileName)))
            {
                var result = DialogResult.No;

                if (!forced)
                    result = MessageBox.Show($"{Path.GetFileNameWithoutExtension(FileName) ?? "Untitled Song"} ({SoundID})\n\nWould you like to save before closing?", "Warning", "Yes", "No", "Cancel");

                if (forced || result == DialogResult.Yes)
                {
                    if (FileName == null || fileForced)
                    {
                        var dialog = new SaveFileDialog()
                        {
                            Title = "Save Map As",
                            Filter = "Text Documents(*.txt)|*.txt"
                        };

                        if (Settings.settings["defaultPath"] != "")
                            dialog.InitialDirectory = Settings.settings["defaultPath"];

                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            Settings.settings["defaultPath"] = Path.GetDirectoryName(dialog.FileName) ?? "";

                            File.WriteAllText(dialog.FileName, data);
                            SaveProperties(dialog.FileName);
                            FileName = dialog.FileName;
                        }
                        else
                            return false;
                    }
                    else
                    {
                        File.WriteAllText(FileName, data);
                        SaveProperties(FileName);
                    }
                }
                else if (result == DialogResult.Cancel)
                    return false;
            }

            return true;
        }

        public void SaveProperties(string filePath)
        {
            var file = Path.ChangeExtension(filePath, ".ini");

            File.WriteAllText(file, ParseProperties());
            Settings.settings["lastFile"] = filePath;
        }

        public bool LoadMap(string pathOrData, bool file = false, bool autosave = false, bool ss = false)
        {
            CurrentMap?.Save();
            
            foreach (Map map in Maps)
            {
                if (file && pathOrData == map.RawFileName && map.IsSaved())
                {
                    map.MakeCurrent();
                    SwitchWindow(new GuiWindowEditor());

                    return true;
                }
            }
            
            CurrentMap = new Map();

            SoundID = "-1";
            FileName = file ? pathOrData : null;

            Notes.Clear();
            SelectedNotes.Clear();
            UpdateSelection();
            SelectedPoint = null;

            TimingPoints.Clear();
            Bookmarks.Clear();

            UndoRedoManager.Clear();
            MusicPlayer.Reset();

            if (pathOrData == "")
                return false;

            var data = file ? File.ReadAllText(pathOrData) : pathOrData;

            try
            {
                while (true)
                    data = WebClient.DownloadString(data);
            }
            catch { }

            try
            {
                string id = Map.Parse(data, Notes, ss);

                SortNotes();

                if (LoadAudio(id))
                {
                    SoundID = id;

                    Settings.settings["currentTime"] = new SliderSetting(0f, (float)MusicPlayer.TotalTime.TotalMilliseconds, (float)MusicPlayer.TotalTime.TotalMilliseconds / 2000f);
                    Settings.settings["beatDivisor"].Value = 3f;
                    Settings.settings["tempo"].Value = 0.9f;
                    Settings.settings["exportOffset"] = 0;

                    Tempo = 1f;
                    Zoom = 1f;

                    if (file)
                    {
                        var propertyFile = Path.ChangeExtension(FileName, ".ini");

                        if (File.Exists(propertyFile))
                            LoadProperties(File.ReadAllText(propertyFile));
                    }
                    else if (autosave)
                        LoadProperties(Settings.settings["autosavedProperties"]);

                    SortTimings();
                    SortBookmarks();

                    CurrentMap.Save();
                    CurrentMap.MakeCurrent();

                    Maps.Add(CurrentMap);
                    CacheMaps();

                    SwitchWindow(new GuiWindowEditor());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load map data", "Warning", "OK");
                ActionLogging.Register($"Failed to load map data - {ex.Message}\n\n{ex.StackTrace}\n\n", "WARN");
                Console.WriteLine(ex);

                return false;
            }

            return SoundID != "-1";
        }

        public void CacheMaps()
        {
            string[] data = new string[Maps.Count];

            for (int i = 0; i < Maps.Count; i++)
                data[i] = Maps[i].ToString();

            string text = string.Join("\r\0", data);

            if (!Directory.Exists("assets/temp"))
                Directory.CreateDirectory("assets/temp");
            File.WriteAllText(cacheFile, text);
        }

        public void LoadCache()
        {
            Maps.Clear();
            string[] data = File.ReadAllText(cacheFile).Split("\r\0");

            for (int i = 0; i < data.Length; i++)
            {
                Map map = new();

                if (map.FromString(data[i]))
                    Maps.Add(map);
            }
        }

        public void LoadLegacyProperties(string text)
        {
            var lines = text.Split('\n');
            var oldVer = false; // pre 1.7

            foreach (var line in lines)
            {
                var split = line.Split('=');

                switch (split[0])
                {
                    case "BPM":
                        var points = split[1].Split(',');

                        foreach (var point in points)
                        {
                            var pointsplit = point.Split('|');
                            if (pointsplit.Length == 1)
                            {
                                pointsplit = new string[] { pointsplit[0], "0" };
                                oldVer = true;
                            }

                            if (pointsplit.Length == 2 && float.TryParse(pointsplit[0], out var bpm) && long.TryParse(pointsplit[1], out var ms))
                                TimingPoints.Add(new TimingPoint(bpm, ms));
                        }

                        SortTimings();

                        break;

                    case "Bookmarks":
                        var bookmarks = split[1].Split(',');

                        foreach (var bookmark in bookmarks)
                        {
                            var bookmarksplit = bookmark.Split('|');

                            if (bookmarksplit.Length == 2 && long.TryParse(bookmarksplit[1], out var ms))
                                Bookmarks.Add(new Bookmark(bookmarksplit[0], ms, ms));
                            else if (bookmarksplit.Length == 3 && long.TryParse(bookmarksplit[1], out var startMs) && long.TryParse(bookmarksplit[2], out var endMs))
                                Bookmarks.Add(new Bookmark(bookmarksplit[0], startMs, endMs));
                        }

                        SortBookmarks();

                        break;

                    case "Offset":
                        if (oldVer) // back when timing points didnt exist and the offset meant bpm/note offset
                        {
                            if (TimingPoints.Count > 0 && long.TryParse(split[1], out var bpmOffset))
                                TimingPoints[0].Ms = bpmOffset;
                        }
                        else
                        {
                            foreach (var note in Notes)
                                note.Ms += (long)Settings.settings["exportOffset"];

                            if (long.TryParse(split[1], out var offset))
                                Settings.settings["exportOffset"] = offset;

                            foreach (var note in Notes)
                                note.Ms -= (long)Settings.settings["exportOffset"];
                        }

                        break;

                    case "Time":
                        if (long.TryParse(split[1], out var time))
                            Settings.settings["currentTime"].Value = time;

                        break;

                    case "Divisor":
                        if (float.TryParse(split[1], out var divisor))
                            Settings.settings["beatDivisor"].Value = divisor - 1f;

                        break;
                }
            }
        }

        public void LoadProperties(string text)
        {
            try
            {
                var result = (JsonObject)JsonValue.Parse(text);

                foreach (var key in result)
                {
                    switch (key.Key)
                    {
                        case "timings":
                            foreach (JsonArray timing in key.Value)
                                TimingPoints.Add(new TimingPoint(timing[0], timing[1]));

                            break;

                        case "bookmarks":
                            foreach (JsonArray bookmark in key.Value)
                                Bookmarks.Add(new Bookmark(bookmark[0], bookmark[1], bookmark[^1]));

                            break;

                        case "currentTime":
                            Settings.settings["currentTime"].Value = key.Value;

                            break;

                        case "beatDivisor":
                            Settings.settings["beatDivisor"].Value = key.Value;

                            break;

                        case "exportOffset":
                            foreach (var note in Notes)
                                note.Ms += (long)Settings.settings["exportOffset"];

                            Settings.settings["exportOffset"] = key.Value;

                            foreach (var note in Notes)
                                note.Ms -= (long)Settings.settings["exportOffset"];

                            break;
                    }
                }
            }
            catch
            {
                try
                {
                    LoadLegacyProperties(text);
                }
                catch { }
            }
        }

        public void ImportProperties()
        {
            var dialog = new OpenFileDialog()
            {
                Title = "Select .ini File",
                Filter = "Map Property Files (*.ini)|*.ini"
            };

            if (Settings.settings["defaultPath"] != "")
                dialog.InitialDirectory = Settings.settings["defaultPath"];

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                TimingPoints.Clear();
                Bookmarks.Clear();

                Settings.settings["defaultPath"] = Path.GetDirectoryName(dialog.FileName) ?? "";

                LoadProperties(File.ReadAllText(dialog.FileName));

                if (CurrentWindow is GuiWindowEditor editor)
                    editor.Timeline.GenerateOffsets();
            }
        }

        public void CopyBookmarks()
        {
            string[] data = new string[Bookmarks.Count];

            for (int i = 0; i < Bookmarks.Count; i++)
            {
                var bookmark = Bookmarks[i];

                if (bookmark.Ms != bookmark.EndMs)
                    data[i] = $"{bookmark.Ms}-{bookmark.EndMs} ~ {bookmark.Text}";
                else
                    data[i] = $"{bookmark.Ms} ~ {bookmark.Text}";
            }

            if (data.Length == 0)
                return;

            Clipboard.SetText(string.Join("\n", data));

            if (CurrentWindow is GuiWindowEditor editor)
                editor.ShowToast("COPIED TO CLIPBOARD", Color.FromArgb(0, 255, 200));
        }

        public void PasteBookmarks()
        {
            var data = Clipboard.GetText();
            string[] bookmarks = data.Split('\n');

            List<Bookmark> tempBookmarks = new();

            for (int i = 0; i < bookmarks.Length; i++)
            {
                var split = bookmarks[i].Split(" ~ ");
                if (split.Length != 2)
                    continue;

                var subsplit = split[0].Split("-");

                if (subsplit.Length == 1 && long.TryParse(subsplit[0], out var ms))
                    tempBookmarks.Add(new Bookmark(split[1], ms, ms));
                else if (subsplit.Length == 2 && long.TryParse(subsplit[0], out var startMs) && long.TryParse(subsplit[1], out var endMs))
                    tempBookmarks.Add(new Bookmark(split[1], startMs, endMs));
            }

            if (tempBookmarks.Count > 0)
                Bookmarks = tempBookmarks.ToList();
        }

        private int currentAutosave;

        private void RunAutosave(int time)
        {
            currentAutosave = time;

            var delay = Task.Delay((int)(Settings.settings["autosaveInterval"] * 60000f)).ContinueWith(_ =>
            {
                if (currentAutosave == time)
                {
                    RunAutosave(time);
                    AttemptAutosave();
                }
            });
        }

        private void AttemptAutosave()
        {
            if (CurrentWindow is GuiWindowEditor editor && Notes.Count > 0)
            {
                if (FileName == null)
                {
                    Settings.settings["autosavedFile"] = Map.Save(SoundID, Notes);
                    Settings.settings["autosavedProperties"] = ParseProperties();
                    Settings.Save(false);

                    editor.ShowToast("AUTOSAVED", Settings.settings["color1"]);
                }
                else if (SaveMap(true, false, false))
                    editor.ShowToast("AUTOSAVED", Settings.settings["color1"]);
            }
        }




        private bool FocusingBox()
        {
            foreach (var control in CurrentWindow.Controls)
                if (control is GuiTextbox box && box.Focused)
                    return true;

            return false;
        }

        public void SetTempo(float newTempo)
        {
            var tempoA = Math.Min(newTempo, 0.9f);
            var tempoB = (newTempo - tempoA) * 2f;

            Tempo = tempoA + tempoB + 0.1f;
            MusicPlayer.Tempo = Tempo;
        }




        public void SortNotes()
        {
            Notes = new List<Note>(Notes.OrderBy(n => n.Ms));

            if (CurrentWindow is GuiWindowEditor editor)
                editor.Timeline.GenerateOffsets();
        }

        public void SortTimings(bool updateList = true)
        {
            TimingPoints = new List<TimingPoint>(TimingPoints.OrderBy(n => n.Ms));

            if (updateList)
                TimingsWindow.Instance?.ResetList();

            if (CurrentWindow is GuiWindowEditor editor)
                editor.Timeline.GenerateOffsets();
        }

        public void SortBookmarks(bool updateList = true)
        {
            Bookmarks = new List<Bookmark>(Bookmarks.OrderBy(n => n.Ms));

            if (updateList)
                BookmarksWindow.Instance?.ResetList();

            if (CurrentWindow is GuiWindowEditor editor)
                editor.Timeline.GenerateOffsets();
        }




        public List<Vector2>? TweenSelected()
        {
            if (SelectedNotes.Count <= 1)
                return null;

            var result = new List<Vector2>();

            var function = Settings.settings["tweenFunction"].Current;
            var mode = Settings.settings["tweenMode"].Current;
            var divisor = Settings.settings["tweenDivisor"];

            // https://easings.net/#
            double Run(double x) => $"{function}-{mode}" switch
            {
                "linear-in" => x,
                "linear-out" => x,
                "linear-in/out" => x,
                "sine-in" => 1 - Math.Cos(x * Math.PI / 2),
                "sine-out" => Math.Sin(x * Math.PI / 2),
                "sine-in/out" => -(Math.Cos(x * Math.PI) - 1) / 2,
                "quadratic-in" => Math.Pow(x, 2),
                "quadratic-out" => 1 - Math.Pow(1 - x, 2),
                "quadratic-in/out" => x < 0.5 ? 2 * Math.Pow(x, 2) : 1 - Math.Pow(-2 * x + 2, 2) / 2,
                "cubic-in" => Math.Pow(x, 3),
                "cubic-out" => 1 - Math.Pow(1 - x, 3),
                "cubic-in/out" => x < 0.5 ? 4 * Math.Pow(x, 3) : 1 - Math.Pow(-2 * x + 2, 3) / 2,
                _ => 0,
            };

            double interval = 1d / divisor;

            for (int i = 0; i < SelectedNotes.Count - 1; i++)
            {
                var note = SelectedNotes[i];
                var next = SelectedNotes[i + 1];

                var xDiff = next.X - note.X;
                var tDiff = next.Ms - note.Ms;

                for (double x = interval; x < 1 - interval / 2; x += interval)
                    result.Add(((float)(Run(x) * xDiff + note.X), (float)(x * tDiff + note.Ms)));
            }

            return result;
        }




        public void SwitchWindow(GuiWindow window)
        {
            if (CurrentWindow is GuiWindowEditor)
                CurrentMap.Save();

            if (window is GuiWindowEditor)
            {
                //SetActivity("Editing a map");
                RunAutosave(DateTime.Now.Millisecond);
            }
            //else if (window is GuiWindowMenu)
                //SetActivity("Sitting in the menu");

            BPMTapper.Instance?.Close();
            TimingsWindow.Instance?.Close();
            BookmarksWindow.Instance?.Close();

            CurrentWindow?.Dispose();
            CurrentWindow = window;

            Settings.Save();
        }




        public static void CheckForUpdates()
        {
            if (!Settings.settings["checkUpdates"])
                return;

            var versionInfo = FileVersionInfo.GetVersionInfo(Process.GetCurrentProcess().MainModule?.FileName ?? "");
            var currentVersion = versionInfo.FileVersion;

            Dictionary<string, string> links = new()
            {
                //{"BHME Player Version", "https://raw.githubusercontent.com/TominoCZ/BeatHopEditor/main/player_version" },
                //{"BHME Player Zip", "https://github.com/TominoCZ/BeatHopEditor/main/BHME%20Player.zip" },
                {"BHME Updater Version", "https://raw.githubusercontent.com/TominoCZ/BeatHopEditor/main/updater_version" },
                {"BHME Updater Zip", "https://raw.githubusercontent.com/TominoCZ/BeatHopEditor/main/BHME%20Updater.zip" },
                {"Editor Redirect", "https://github.com/TominoCZ/BeatHopEditor/releases/latest" }
            };

            static void ExtractFile(string path)
            {
                using (ZipArchive archive = ZipFile.OpenRead(path))
                {
                    foreach (var entry in archive.Entries)
                    {
                        try
                        {
                            entry.ExtractToFile(entry.FullName, true);
                        }
                        catch { ActionLogging.Register($"Failed to extract file: {entry.FullName}", "WARN"); }
                    }
                }

                File.Delete(path);
            }

            void Download(string file)
            {
                ActionLogging.Register($"Attempting to download file '{file}'");
                WebClient.DownloadFile(links[$"{file} Zip"], $"{file}.zip");
                ExtractFile($"{file}.zip");
            }

            void Run(string file, string tag)
            {
                ActionLogging.Register($"Searching for file '{file}'");
                if (File.Exists($"{file}.exe"))
                {
                    string current = FileVersionInfo.GetVersionInfo($"{file}.exe").FileVersion ?? "";
                    string version = WebClient.DownloadString(links[$"{file} Version"]).Trim();

                    if (current != version)
                    {
                        DialogResult diag = MessageBox.Show($"New {tag} version is available ({version}). Would you like to download the new version?", "Warning", "Yes", "No");

                        if (diag == DialogResult.Yes)
                            Download(file);
                    }
                }
                else
                {
                    DialogResult diag = MessageBox.Show($"{tag} is not present in this directory. Would you like to download it?", "Warning", "Yes", "No");

                    if (diag == DialogResult.Yes)
                        Download(file);
                }
            }

            try
            {
                //Run("BHME Player", "Map Player");
                Run("BHME Updater", "Auto Updater");

                var redirect = WebClient.GetRedirect(links["Editor Redirect"]);

                if (File.Exists("BHME Updater.exe") && redirect != "")
                {
                    var version = redirect[(redirect.LastIndexOf("/") + 1)..];

                    ActionLogging.Register("Checking version of editor");
                    if (version != currentVersion)
                    {
                        var diag = MessageBox.Show($"New Editor version is available ({version}). Would you like to download the new version?", "Warning", "Yes", "No");

                        if (diag == DialogResult.Yes)
                        {
                            ActionLogging.Register("Attempting to run updater");
                            Process.Start("BHME Updater.exe");
                        }
                    }
                }
            }
            catch
            {
                MessageBox.Show("Failed to check for updates", "Warning", "OK");
            }
        }

        //private void DiscordInit()
        //{
        //    if (!discordEnabled)
        //        return;

        //    try
        //    {
        //        discord = new Discord.Discord(1067849747710345346, (ulong)CreateFlags.NoRequireDiscord);
        //        activityManager = discord.GetActivityManager();
        //    }
        //    catch { discordEnabled = false; }
        //}

        //public void SetActivity(string status)
        //{
        //    if (!discordEnabled)
        //        return;

        //    var activity = new Activity
        //    {
        //        State = status,
        //        Details = $"Version {Assembly.GetExecutingAssembly().GetName().Version}",
        //        Timestamps = { Start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
        //        Assets = { LargeImage = "logo" },
        //        Instance = true,
        //    };

        //    activityManager.UpdateActivity(activity, (result) =>
        //    {
        //        Console.WriteLine($"{(result == Result.Ok ? "Activity success" : "Activity failed")}");

        //        if (result != Result.Ok)
        //            ActionLogging.Register($"Failed to update Discord Rich Presence activity - {status}", "WARN");
        //    });
        //}
    }
}
