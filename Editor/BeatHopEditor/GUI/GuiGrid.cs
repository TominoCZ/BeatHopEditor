using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using AvaloniaEdit.Utils;
using OpenTK.Graphics;
using SharpFont.Cache;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BeatHopEditor.GUI
{
    internal class GuiGrid : WindowControl
    {
        public Note? HoveringNote;
        public Note? DraggingNote;

        public bool Hovering;
        public bool Dragging;

        private readonly Note startNote = new(2f, 0);

        private Vector2 lastPlaced;
        private Vector2 lastPos;
        private Vector2 startPos;
        private RectangleF prevRect;

        private readonly Dictionary<string, int> Indices = new()
        {
            {"rectLength", 0 },
            {"loopLength", 0 },
            {"lineLength", 0 },
        };

        public GuiGrid(float sizex, float sizey) : base(0f, 0f, sizex, sizey)
        {
            Dynamic = true;
            prevRect = Rect;

            InstanceSetup();
            Init();
        }

        public override void InstanceSetup()
        {
            ClearBuffers();

            VaOs = new VertexArrayHandle[6];
            VbOs = new BufferHandle[12];
            VertexCounts = new int[6];

            var noteVerts = GLU.OutlineAsTriangles(0, 0, 75, 75, 2, 1f, 1f, 1f, 1f);
            var fillVerts = GLU.Rect(0, 0, 75, 75, 1f, 1f, 1f, 0.15f);
            noteVerts = noteVerts.Concat(fillVerts).ToArray();

            var previewVerts = GLU.OutlineAsTriangles(0, 0, 65, 65, 2, 1f, 1f, 1f, 0.09375f);
            var previewFillVerts = GLU.Rect(0, 0, 65, 65, 1f, 1f, 1f, 0.125f);
            previewVerts = previewVerts.Concat(previewFillVerts).ToArray();

            var approachVerts = GLU.OutlineAsTriangles(0, 0, 1, 1, 0.0125f, 1f, 1f, 1f, 1f);
            var selectVerts = GLU.OutlineAsTriangles(0, 0, 83, 83, 2, 1f, 1f, 1f, 1f);
            var hoverVerts = GLU.OutlineAsTriangles(0, 0, 83, 83, 2, 1f, 1f, 1f, 0.25f);

            var beatVerts = GLU.Rect(0, 0, 500, 1, 1f, 1f, 1f, 0.8f);

            AddToBuffers(noteVerts, 0);
            AddToBuffers(approachVerts, 1);
            AddToBuffers(selectVerts, 2);
            AddToBuffers(hoverVerts, 3);
            AddToBuffers(previewVerts, 4);
            AddToBuffers(beatVerts, 5);
        }

        private readonly List<Vector4> previewNoteOffsets = new();

        public override void GenerateOffsets()
        {
            var editor = MainWindow.Instance;
            var mouse = editor.Mouse;

            var cellSize = Rect.Width / 5f;
            var noteSize = cellSize * 0.75f;
            var cellGap = (cellSize - noteSize) / 2f;

            var currentTime = Settings.settings["currentTime"].Value;
            var totalTime = Settings.settings["currentTime"].Max;

            var approachSquares = Settings.settings["approachSquares"];
            var gridNumbers = Settings.settings["gridNumbers"];
            var noteColors = Settings.settings["noteColors"];

            var separateClickTools = Settings.settings["separateClickTools"];
            var selectTool = Settings.settings["selectTool"];

            var beatOffsets = new List<Vector4>();
            var approachOffsets = new List<Vector4>();
            var noteOffsets = new List<Vector4>();
            var selectOffsets = new List<Vector4>();
            var hoverOffset = new Vector4(-1920, 0, 0, 0);

            var isHoveringNote = false;
            Note? nextNote = null;

            var approachRate = editor.GridStep;
            var trackHeight = Settings.settings["trackHeight"].Value + 64f;
            var numThreshold = trackHeight - noteSize / 2f + 8f;

            var beatDivisor = Settings.settings["beatDivisor"].Value + 1f;
            double multiplier = beatDivisor % 1 == 0 ? 1f : 1f / (beatDivisor % 1);
            int divisor = (int)Math.Round(beatDivisor * multiplier);

            for (int i = 0; i < editor.TimingPoints.Count; i++)
            {
                var point = editor.TimingPoints[i];

                var interval = 60000f / point.BPM * multiplier;
                var subInterval = interval / divisor;
                var startMs = Math.Max(point.Ms, (int)((point.Ms - currentTime) / interval) * interval);
                var nextMs = i + 1 < editor.TimingPoints.Count ? Math.Min(editor.TimingPoints[i + 1].Ms, totalTime) : totalTime;

                if (point.BPM > 0 && nextMs > currentTime && startMs - currentTime <= approachRate)
                {
                    {
                        var linear = 1 - Math.Min(1f, (startMs - currentTime) / 1600f * editor.Zoom);
                        var y = (linear * 1.5f - 0.5f) * Rect.Y + cellSize - cellGap;

                        if (y < Rect.Y)
                            beatOffsets.Add((Rect.X, (float)y, 0.5f, 1));
                    }

                    for (double j = startMs; j < nextMs; j += interval)
                    {
                        if (j - currentTime > approachRate)
                            break;

                        for (int k = 0; k < divisor; k++)
                        {
                            if (j + k * subInterval > nextMs)
                                break;

                            bool half = divisor % 2 == 0 && k == divisor / 2;

                            var linear = 1 - Math.Min(1f, (j + k * subInterval - currentTime) / approachRate);
                            var y = (linear * 1.5f - 0.5f) * Rect.Y + cellSize - cellGap;

                            if (y < Rect.Y)
                                beatOffsets.Add((Rect.X, (float)y, 0.5f, k == 0 ? 1 : (half ? 2 : 0)));
                        }
                    }
                }
            }

            for (int i = 0; i < editor.Notes.Count; i++)
            {
                var note = editor.Notes[i];
                var passed = currentTime > note.Ms + 1;
                var visible = !passed && note.Ms - currentTime <= approachRate;

                if (!passed)
                    nextNote ??= note;

                if (!visible)
                {
                    if (passed && nextNote != null)
                        break;
                    continue;
                }

                var x = Rect.X + note.X * cellSize + cellGap;

                var linear = 1 - Math.Min(1f, (note.Ms - currentTime) / approachRate);
                var progress = Math.Min(1f, (float)Math.Pow(linear, 2f));
                var y = (linear * 1.5f - 0.5f) * Rect.Y + cellGap;

                var noteRect = new RectangleF(x, y, noteSize, noteSize);
                var c = i % noteColors.Count;

                if (approachSquares)
                {
                    var outlineSize = 4 + noteSize + noteSize * (1 - progress) * 2 + 0.5f;

                    approachOffsets.Add((x - outlineSize / 2f + noteSize / 2f, Rect.Y + cellGap - outlineSize / 2f + noteSize / 2f, 2 * (int)outlineSize + progress, c));
                }

                if (note.Selected)
                    selectOffsets.Add((x - 4, y - 4, progress, 6));

                if (!isHoveringNote && noteRect.Contains(mouse) && (!separateClickTools || selectTool) && mouse.Y > trackHeight)
                {
                    HoveringNote = note;
                    hoverOffset = (x - 4, y - 4, 1, 5);
                    isHoveringNote = true;
                }

                noteOffsets.Add((x, y, 2f + progress, c));

                if (gridNumbers && y > numThreshold)
                {
                    var numText = $"{i + 1:##,###}";
                    var width = FontRenderer.GetWidth(numText, 24, "main");
                    var height = FontRenderer.GetHeight(24, "main");

                    color2Texts.AddRange(FontRenderer.Print((int)(noteRect.X + noteRect.Width / 2f - width / 2f), (int)(noteRect.Y + noteRect.Height / 2f - height / 2f + 3f),
                        numText, 24, "main"));
                    for (int j = 0; j < numText.Length; j++)
                        alphas.Add(1 - progress);
                }
            }

            if (!isHoveringNote)
                HoveringNote = null;
            
            //render fake note
            if (Hovering && (!separateClickTools || !selectTool) && (HoveringNote == null || separateClickTools))
                AddPreviewNote(mouse.X, mouse.Y, 9, true);

            GL.UseProgram(Shader.InstancedProgram);
            RegisterData(5, beatOffsets.ToArray());

            GL.UseProgram(Shader.GridInstancedProgram);
            RegisterData(0, noteOffsets.ToArray());
            RegisterData(1, approachOffsets.ToArray());

            GL.UseProgram(Shader.InstancedProgram);
            RegisterData(2, selectOffsets.ToArray());
            RegisterData(3, new Vector4[1] { hoverOffset });
            RegisterData(4, previewNoteOffsets.ToArray());
        }

        private int offset;

        public override void Render(float mousex, float mousey, float frametime)
        {
            Update();

            // render background
            GL.UseProgram(Shader.Program);

            GL.BindVertexArray(VaO);
            offset = Indices["rectLength"] + Indices["loopLength"] + Indices["lineLength"];
            GL.DrawArrays(PrimitiveType.Triangles, 0, offset);

            // render keybinds
            GL.UseProgram(Shader.FontTexProgram);
            FontRenderer.SetActive("main");

            GL.Uniform4f(TexColorLocation, 0.2f, 0.2f, 0.2f, 1f);
            FontRenderer.RenderData("main", color1Texts.ToArray());

            // render dynamic elements
            GenerateOffsets();

            // undo program switch
            GL.UseProgram(Shader.Program);
        }

        public override void RenderTexture()
        {
            GL.Uniform4f(TexColorLocation, 1f, 1f, 1f, 1f);
            FontRenderer.RenderData("main", color2Texts.ToArray(), alphas.ToArray());

            // layer autoplay cursor on top
            GL.UseProgram(Shader.Program);

            GL.BindVertexArray(VaO);
            GL.DrawArrays(PrimitiveType.TriangleFan, offset, 16);
            GL.DrawArrays(PrimitiveType.LineLoop, offset + 16, 16);

            GL.UseProgram(Shader.FontTexProgram);
        }

        private List<float> rects = new();
        private List<float> loops = new();
        private List<float> lines = new();
        private List<Vector4> color1Texts = new();
        private List<Vector4> color2Texts = new();
        private List<float> alphas = new();

        public override Tuple<float[], float[]> GetVertices()
        {
            rects = new();
            loops = new();
            lines = new();
            color1Texts = new();
            color2Texts = new();
            alphas = new();

            var editor = MainWindow.Instance;

            var cellSize = Rect.Width / 5f;

            var currentTime = Settings.settings["currentTime"].Value;
            var quantumLines = Settings.settings["quantumGridLines"] && Settings.settings["enableQuantum"];

            rects.AddRange(GLU.Rect(Rect, 0.1f, 0.1f, 0.1f, Settings.settings["gridOpacity"] / 255f));
            loops.AddRange(GLU.OutlineAsTriangles(Rect, 1, 0.2f, 0.2f, 0.2f));

            var divisor = quantumLines ? Settings.settings["quantumSnapping"].Value + 1f : 1f;

            for (int i = 0; i < 5; i++)
            {
                var x = Rect.X + Rect.Width / 5f * i;

                for (int j = 0; j < divisor - 0.5f; j++)
                {
                    var xf = x + j * Rect.Width / 5f / divisor;

                    if (i > 0 || j > 0)
                        lines.AddRange(GLU.FadingLine(xf, Rect.Y + Rect.Height, xf, 0, 1, 0.35f, 0.35f, 0.35f));
                }
            }

            lines.AddRange(GLU.FadingLine(Rect.X - 2, Rect.Bottom, Rect.X - 2, 0, 4, 0.5f, 0.5f, 0.5f));
            lines.AddRange(GLU.FadingLine(Rect.Right + 2, Rect.Bottom, Rect.Right + 2, 0, 4, 0.5f, 0.5f, 0.5f));

            //render grid letters
            if (Settings.settings["gridLetters"])
            {
                var copy = new Dictionary<Keys, int>(MainWindow.Instance.KeyMapping);

                foreach (var key in copy)
                {
                    var letter = key.Key.ToString().Replace("KeyPad", "");

                    var x = Rect.X + key.Value * cellSize + cellSize / 2f;

                    var width = FontRenderer.GetWidth(letter, 38, "main");
                    var height = FontRenderer.GetHeight(38, "main");

                    color1Texts.AddRange(FontRenderer.Print((int)(x - width / 2f), (int)(Rect.Y + Rect.Height / 2f - height / 2f + 5f), letter, 38, "main"));
                }
            }

            Indices["rectLength"] = rects.Count / 6;
            Indices["loopLength"] = loops.Count / 6;
            Indices["lineLength"] = lines.Count / 6;

            rects.AddRange(loops);
            rects.AddRange(lines);

            //process notes
            Note? last = null;
            Note? next = null;

            var approachRate = editor.GridStep;

            for (int i = 0; i < editor.Notes.Count; i++)
            {
                var note = editor.Notes[i];
                var passed = currentTime > note.Ms + 1;
                var visible = !passed && note.Ms - currentTime <= approachRate;

                if (passed)
                    last = note;
                else
                    next ??= note;

                if (!visible && passed && next != null)
                    break;
            }

            //render autoplay cursor
            if (Settings.settings["autoplay"])
            {
                last ??= startNote;
                next ??= last;

                var timeDiff = next.Ms - last.Ms;
                var timePos = currentTime - last.Ms;

                var progress = timeDiff == 0 ? 1 : (float)timePos / timeDiff;
                progress = (float)Math.Sin(progress * MathHelper.PiOver2);

                var width = (float)Math.Sin(progress * MathHelper.Pi) * 4f + 8;

                var lx = Rect.X + last.X * cellSize;
                var nx = Rect.X + next.X * cellSize;
                var x = cellSize / 2f + lx + (nx - lx) * progress;
                var y = Rect.Y + Rect.Height / 2f;

                rects.AddRange(GLU.Circle(x, y, width, 16, 0, 1f, 1f, 1f, 0.25f));
                rects.AddRange(GLU.Circle(x, y, width + 1f, 16, 0, 1f, 1f, 1f, 1f));
            }

            return new Tuple<float[], float[]>(rects.ToArray(), Array.Empty<float>());
        }

        public override void OnMouseClick(Point pos, bool right = false)
        {
            var editor = MainWindow.Instance;

            if (editor.CurrentWindow.Track != null && editor.CurrentWindow.Track.HoveringPoint != null)
                return;

            var separateClickTools = Settings.settings["separateClickTools"];
            var selectTool = Settings.settings["selectTool"];

            Dragging = Hovering && (HoveringNote != null || (!separateClickTools || !selectTool));

            if (Dragging)
            {
                if (HoveringNote == null || (separateClickTools && !selectTool))
                {
                    var gridPos = editor.PointToGridSpace(pos.X, pos.Y - Rect.Width / 10f);
                    var note = new Note(gridPos.X, (long)(gridPos.Y >= 0 && pos.Y < Rect.Y ? gridPos.Y : Settings.settings["currentTime"].Value));

                    editor.UndoRedoManager.Add("ADD NOTE", () =>
                    {
                        editor.Notes.Remove(note);
                        editor.SortNotes();
                    }, () =>
                    {
                        editor.Notes.Add(note);
                        editor.SortNotes();
                    });

                    if (Settings.settings["autoAdvance"] && gridPos.Y == Settings.settings["currentTime"].Value)
                        editor.Advance();

                    lastPlaced = gridPos;
                }
                else if (HoveringNote != null)
                {
                    DraggingNote = HoveringNote;
                    lastPos = (HoveringNote.X, HoveringNote.Ms);
                    startPos = new Vector2(HoveringNote.X, 0);

                    var selected = editor.SelectedNotes.ToList();

                    if (editor.ShiftHeld)
                    {
                        selected = new List<Note> { selected[0] };

                        var first = selected[0];
                        var last = HoveringNote;
                        var min = Math.Min(first.Ms, last.Ms);
                        var max = Math.Max(first.Ms, last.Ms);

                        foreach (var note in editor.Notes)
                            if (note.Ms >= min && note.Ms <= max && !selected.Contains(note))
                                selected.Add(note);
                    }
                    else if (editor.CtrlHeld)
                    {
                        if (selected.Contains(HoveringNote))
                            selected.Remove(HoveringNote);
                        else
                            selected.Add(HoveringNote);
                    }
                    else if (!selected.Contains(HoveringNote))
                        selected = new List<Note>() { HoveringNote };

                    editor.SelectedNotes = selected.ToList();
                    editor.UpdateSelection();
                }
            }
        }

        public override void OnMouseMove(Point pos)
        {
            if (Dragging)
            {
                var editor = MainWindow.Instance;

                if (DraggingNote == null)
                {
                    var gridPos = editor.PointToGridSpace(pos.X, pos.Y - Rect.Width / 10f);

                    if (gridPos.X != lastPlaced.X || (gridPos.Y != Settings.settings["currentTime"].Value && gridPos.Y != lastPlaced.Y))
                    {
                        var note = new Note(gridPos.X, (long)(gridPos.Y >= 0 ? gridPos.Y : Settings.settings["currentTime"].Value));

                        editor.UndoRedoManager.Add("ADD NOTE", () =>
                        {
                            editor.Notes.Remove(note);
                            editor.SortNotes();
                        }, () =>
                        {
                            editor.Notes.Add(note);
                            editor.SortNotes();
                        });

                        if (Settings.settings["autoAdvance"] && gridPos.Y == Settings.settings["currentTime"].Value)
                            editor.Advance();

                        lastPlaced = gridPos;
                    }
                }
                else
                {
                    var newPos = editor.PointToGridSpace(pos.X, pos.Y - Rect.Width / 10f);

                    if (newPos.X != lastPos.X || (newPos.Y != Settings.settings["currentTime"].Value && newPos.Y != lastPos.Y))
                    {
                        var xDiff = newPos.X - DraggingNote.X;
                        var tDiff = (long)newPos.Y - DraggingNote.Ms;

                        var maxX = DraggingNote.X;
                        var minX = DraggingNote.X;

                        foreach (var note in editor.SelectedNotes)
                        {
                            maxX = Math.Max(note.X, maxX);
                            minX = Math.Min(note.X, minX);
                        }

                        xDiff = Math.Max(MainWindow.Bounds.X, minX + xDiff) - minX;
                        xDiff = Math.Min(MainWindow.Bounds.Y, maxX + xDiff) - maxX;

                        foreach (var note in editor.SelectedNotes)
                        {
                            note.X += xDiff;
                            if (newPos.Y > 0)
                                note.Ms += tDiff;
                        }

                        lastPos = newPos;
                    }
                }
            }
        }

        public override void OnMouseUp(Point pos)
        {
            if (DraggingNote != null && new Vector2(DraggingNote.X, 0) != startPos)
            {
                var editor = MainWindow.Instance;
                var selected = editor.SelectedNotes.ToList();
                var oldPos = new List<Vector2>();
                var newPos = new List<Vector2>();

                var posDiff = new Vector2(DraggingNote.X, 0) - startPos;

                for (int i = 0; i < selected.Count; i++)
                {
                    var xy = new Vector2(selected[i].X, 0);

                    oldPos.Add(xy - posDiff);
                    newPos.Add(xy);
                }

                editor.UndoRedoManager.Add($"MOVE NOTE{(selected.Count > 1 ? "S" : "")}", () =>
                {
                    for (int i = 0; i < selected.Count; i++)
                        selected[i].X = oldPos[i].X;
                }, () =>
                {
                    for (int i = 0; i < selected.Count; i++)
                        selected[i].X = newPos[i].X;
                }, false);
            }

            Dragging = false;
            DraggingNote = null;
            lastPlaced = (-1, -1);
        }

        public void ClearPreviewNotes()
        {
            previewNoteOffsets.Clear();
        }

        // mouse: mouse.X, mouse.Y
        // other: note.X, note.Ms
        public void AddPreviewNote(float x, float y, int c, bool mouse = false)
        {
            var editor = MainWindow.Instance;

            var cellSize = Rect.Width / 5f;
            var noteSize = cellSize * 0.65f;
            var cellGap = (cellSize - noteSize) / 2f;

            var currentTime = Settings.settings["currentTime"].Value;

            if (mouse)
            {
                var pos = editor.PointToGridSpace(x, y - cellSize / 2);
                x = Rect.X + pos.X * cellSize + cellGap;
                var linear = 1 - (pos.Y - currentTime) / editor.GridStep;
                var yf = (linear * 1.5f - 0.5f) * Rect.Y + cellGap;

                if (pos.Y - currentTime < 0 || Rect.Contains(x, y))
                    yf = Rect.Y + cellGap;

                previewNoteOffsets.Add((x, yf, 1, c));
            }
            else
            {
                var linear = 1 - Math.Min(1f, (y - currentTime) / editor.GridStep);
                x = x * cellSize + Rect.X + cellGap;
                y = (linear * 1.5f - 0.5f) * Rect.Y + cellGap;

                if (y > 0)
                    previewNoteOffsets.Add((x, y, 1, c));
            }
        }
    }
}
