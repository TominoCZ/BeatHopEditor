﻿using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Graphics.OpenGL;
using System.Drawing;
using OpenTK.Mathematics;
using OpenTK.Graphics;
using System.Buffers;

namespace BeatHopEditor.GUI
{
    internal class GuiTrack : WindowControl
    {
        private float lastPlayedTick;

        public Note? LastPlayed;
        public Note? HoveringNote;
        public Note? DraggingNote;

        public TimingPoint? HoveringPoint;
        public TimingPoint? DraggingPoint;
        
        public bool Hovering;

        public PointF DragStartPoint;
        public long DragStartMs;

        public bool DraggingTrack;
        public bool RightDraggingTrack;

        private bool replay;

        private const float cellSize = 64f;
        private const float noteSize = cellSize * 0.65f;
        private const float cellGap = (cellSize - noteSize) / 2f;
        private float GapF => Rect.Height - noteSize - cellGap;

        public float StartPos = 0f;
        public float EndPos = 1f;

        private Vector4 PosSet = new();

        private readonly Dictionary<string, int> Indices = new()
        {
            {"rectLength", 0 },
            {"loopLength", 0 },
            {"lineLength", 0 },
        };

        private bool selecting;
        private RectangleF selectHitbox;

        private readonly ArrayPool<Vector4> Pool = ArrayPool<Vector4>.Shared;


        public GuiTrack() : base(0, 0, MainWindow.Instance.ClientSize.X, 0)
        {
            var yoffset = MainWindow.Instance.CurrentWindow?.YOffset ?? 0f;

            Rect.Height = yoffset - 32;
            OriginRect = new RectangleF(0, 0, Rect.Width, yoffset - 32);

            Dynamic = true;

            InstanceSetup();
            Init();
        }

        public override void InstanceSetup()
        {
            ClearBuffers();

            VaOs = new VertexArrayHandle[15];
            VbOs = new BufferHandle[30];
            VertexCounts = new int[15];

            // notes
            var noteRect = new RectangleF(0, cellGap, noteSize, noteSize);

            // normal
            var noteVerts = new List<float>();
            noteVerts.AddRange(GLU.Rect(noteRect, 1f, 1f, 1f, 1f / 20f));
            noteVerts.AddRange(GLU.OutlineAsTriangles(noteRect, 1, 1f, 1f, 1f, 1f));
            var noteLocationVerts = GLU.Rect(0, cellGap, noteSize / 5, noteSize, 1f, 1f, 1f, 1f);
            // text line
            var textLineVerts = GLU.Line(0.5f, Rect.Height + 3, 0.5f, Rect.Height + 28, 1, 1f, 1f, 1f, 1f);

            for (int j = 0; j < 4; j++)
            {
                var x = (j + 1) * noteSize / 5;

                noteVerts.AddRange(GLU.Line(x, cellGap, x, cellGap + noteSize, 2, 1f, 1f, 1f, 1f * 0.45f));
            }

            AddToBuffers(noteVerts.ToArray(), 0);
            AddToBuffers(noteLocationVerts.ToArray(), 1);
            AddToBuffers(textLineVerts, 13);

            // note select box
            var noteSelectVerts = GLU.OutlineAsTriangles(-4, cellGap - 4, noteSize + 8, noteSize + 8, 1, 1f, 1f, 1f, 1f);
            AddToBuffers(noteSelectVerts, 4);

            // note hover box
            var noteHoverVerts = GLU.OutlineAsTriangles(-4, cellGap - 4, noteSize + 8, noteSize + 8, 1, 1f, 1f, 1f, 1f);
            AddToBuffers(noteHoverVerts, 5);

            // drag line
            var dragVerts = GLU.Line(0, 0, 0, Rect.Height, 1, 1f, 1f, 1f, 1f);
            AddToBuffers(dragVerts, 6);

            // start bpm
            var startBpmVerts = GLU.Line(0, 0, 0, Rect.Height + 58, 1, 1f, 1f, 1f, 1f);
            AddToBuffers(startBpmVerts, 7);

            // full bpm
            var fullBpmVerts = GLU.Line(0, Rect.Height, 0, Rect.Height - GapF, 1, 1f, 1f, 1f, 1f);
            AddToBuffers(fullBpmVerts, 8);

            // half bpm
            var halfBpmVerts = GLU.Line(0, Rect.Height - 3 * GapF / 5, 0, Rect.Height, 1, 1f, 1f, 1f, 1f);
            AddToBuffers(halfBpmVerts, 9);

            // sub bpm
            var subBpmVerts = GLU.Line(0, Rect.Height - 3 * GapF / 10, 0, Rect.Height, 1, 1f, 1f, 1f, 1f);
            AddToBuffers(subBpmVerts, 10);

            // point hover box
            var pointHoverVerts = GLU.OutlineAsTriangles(-4, Rect.Height, 80, 60, 1, 1f, 1f, 1f, 1f);
            AddToBuffers(pointHoverVerts, 11);

            // point select box
            var pointSelectVerts = GLU.OutlineAsTriangles(-4, Rect.Height, 80, 60, 1, 1f, 1f, 1f, 1f);
            AddToBuffers(pointSelectVerts, 12);
        }

        public override void GenerateOffsets()
        {
            color1Texts.Clear();
            color2Texts.Clear();

            var editor = MainWindow.Instance;
            var currentTime = Settings.settings["currentTime"].Value;

            var mouse = editor.Mouse;
            var noteStep = editor.NoteStep;

            var totalTime = Settings.settings["currentTime"].Max;
            var sfxOffset = Settings.settings["sfxOffset"];
            var beatDivisor = Settings.settings["beatDivisor"].Value + 1f;

            var posX = currentTime / 1000f * noteStep;
            var cursorX = Rect.Width * Settings.settings["cursorPos"].Value / 100f;

            var noteColors = Settings.settings["noteColors"];
            var colorCount = noteColors.Count;
            
            var noteOffsets = Pool.Rent(editor.Notes.Count);
            var textLineOffsets = Pool.Rent(editor.Notes.Count);
            var noteLocationOffsets = Pool.Rent(editor.Notes.Count);

            var noteHoverOffset = new Vector4(-1920, 0, 0, 0);
            var noteSelectOffsets = new List<Vector4>();
            var noteDragOffsets = new List<Vector4>();

            selecting = editor.RightHeld && RightDraggingTrack;
            var selected = selecting ? new List<Note>() : editor.SelectedNotes;

            var minMs = (-cursorX - noteSize) * 1000f / noteStep + currentTime;
            var maxMs = (Rect.Width - cursorX) * 1000f / noteStep + currentTime;

            HoveringNote = null;
            HoveringPoint = null;
            Note? closest = null;
            float? lastRendered = null;

            // notes
            for (int i = 0; i < editor.Notes.Count; i++)
            {
                var note = editor.Notes[i];
                var a = note.Ms < currentTime - 1 ? 0.35f : 1f;

                var x = cursorX - posX + note.Ms / 1000f * noteStep;
                var gridX = x + note.X * noteSize / 5;

                int c = i % colorCount;

                noteOffsets[i] = (x, 0, a, c);
                noteLocationOffsets[i] = (gridX, 0, a, c);
                textLineOffsets[i] = (x, 0, a, 4);

                var noteRect = new RectangleF(x, cellGap, noteSize, noteSize);
                var hovering = HoveringNote == null && DraggingNote == null && noteRect.Contains(mouse.X, mouse.Y);

                bool noteSelected = selecting && selectHitbox.IntersectsWith(noteRect);
                if (noteSelected)
                    selected.Add(note);

                if (note.Ms <= currentTime - sfxOffset)
                    closest = note;

                if (selecting)
                    note.Selected = noteSelected;
                noteSelected |= note.Selected;

                if (hovering)
                {
                    noteHoverOffset = (x, 0, 1, 5);
                    HoveringNote = note;
                }
                else if (noteSelected)
                {
                    if (DraggingNote == null)
                        noteSelectOffsets.Add((x, 0, 1, 6));
                    else
                    {
                        var dragX = cursorX - posX + note.DragStartMs / 1000f * noteStep;
                        noteDragOffsets.Add((dragX, 0, 1, 7));
                    }
                }


                if (note.Ms < minMs || note.Ms > maxMs)
                    continue;

                if (lastRendered == null || x - 8 > lastRendered)
                {
                    var numText = $"Note {i + 1:##,###}";
                    var msText = $"{note.Ms:##,##0}ms";

                    color1Texts.AddRange(FontRenderer.Print((int)x + 3, (int)Rect.Height, numText, 16, "main"));
                    color2Texts.AddRange(FontRenderer.Print((int)x + 3, (int)Rect.Height + 15, msText, 16, "main"));

                    lastRendered = x;
                }
            }

            GL.UseProgram(Shader.NoteInstancedProgram);
            RegisterData(1, noteLocationOffsets);
            RegisterData(0, noteOffsets);

            Pool.Return(noteOffsets, true);
            Pool.Return(noteLocationOffsets, true);

            GL.UseProgram(Shader.InstancedProgram);
            RegisterData(13, textLineOffsets);

            Pool.Return(textLineOffsets, true);

            RegisterData(4, noteSelectOffsets.ToArray());
            RegisterData(5, new Vector4[1] { noteHoverOffset });
            RegisterData(6, noteDragOffsets.ToArray());

            if (selecting)
                editor.SelectedNotes = selected;

            //play hit sound
            if (LastPlayed != closest)
            {
                LastPlayed = closest;

                if (closest != null && editor.MusicPlayer.IsPlaying)
                    editor.SoundPlayer.Play("hit");
            }


            // bpm lines
            double multiplier = beatDivisor % 1 == 0 ? 1f : 1f / (beatDivisor % 1);
            int divisor = (int)Math.Round(beatDivisor * multiplier);
            bool doubleDiv = divisor % 2 == 0;
            int divOff = divisor - 1 - (doubleDiv ? 1 : 0);

            int numPoints = editor.TimingPoints.Count;
            int numFull = 0, numHalf = 0, numSub = 0;

            Vector3i[] metrics = new Vector3i[numPoints];
            Vector3i current = (0, 0, 0);

            for (int i = 0; i < numPoints; i++)
            {
                var point = editor.TimingPoints[i];
                if (point.BPM == 0 || point.Ms > totalTime)
                    continue;

                double nextMs = i + 1 < numPoints ? Math.Min(editor.TimingPoints[i + 1].Ms, totalTime) : totalTime;
                double totalMs = nextMs - point.Ms;

                double stepMs = 60000 / point.BPM * multiplier;

                int full = (int)(totalMs / stepMs);
                int half = doubleDiv ? (int)(totalMs / (stepMs / 2) - full) : 0;
                int sub = (int)(totalMs / (stepMs / divisor) - full - half);

                metrics[i] = (full, half, sub);

                numFull += full;
                numHalf += half;
                numSub += sub;
            }

            var startBpmOffsets = Pool.Rent(numPoints);
            var fullBpmOffsets = Pool.Rent(numFull);
            var halfBpmOffsets = Pool.Rent(numHalf);
            var subBpmOffsets = Pool.Rent(numSub);

            var pointHoverOffset = new Vector4(-1920, 0, 0, 0);
            var pointSelectOffset = new Vector4(-1920, 0, 0, 0);

            for (int i = 0; i < numPoints; i++)
            {
                var point = editor.TimingPoints[i];
                if (point.BPM == 0)
                    continue;

                var pointMetrics = metrics[i];

                double stepMs = 60000 / point.BPM * multiplier;
                double halfStep = stepMs / 2;
                double stepSmall = stepMs / divisor;
                double curStep = stepSmall;
                float lineX = cursorX - posX + point.Ms / 1000f * noteStep;
                startBpmOffsets[i] = (lineX, 0, 1, 8);
                double x;

                var pointRect = new RectangleF(lineX, Rect.Height, 72, 52);
                var hovering = HoveringPoint == null && DraggingPoint == null && pointRect.Contains(mouse);
                var pointSelected = editor.SelectedPoint == point;

                var numText = $"{point.BPM:##,###.###} BPM";
                var msText = $"{point.Ms:##,##0}ms";

                color1Texts.AddRange(FontRenderer.Print((int)lineX + 3, (int)Rect.Height + 28, numText, 16, "main"));
                color2Texts.AddRange(FontRenderer.Print((int)lineX + 3, (int)Rect.Height + 43, msText, 16, "main"));

                if (hovering)
                {
                    pointHoverOffset = (lineX, 0, 1, 5);
                    HoveringPoint = point;
                }
                else if (pointSelected)
                    pointSelectOffset = (lineX, 0, 1, 6);

                for (int j = 0; j < pointMetrics.X; j++)
                {
                    x = lineX + stepMs * (j + 1) / 1000f * noteStep;
                    fullBpmOffsets[current.X + j] = ((float)x, 0, 1, 1);
                }

                for (int j = 0; j < pointMetrics.Y; j++)
                {
                    x = lineX + (stepMs * j + halfStep) / 1000f * noteStep;
                    halfBpmOffsets[current.Y + j] = ((float)x, 0, 1, 2);
                }

                for (int j = 0; j < pointMetrics.Z; j++)
                {
                    int cur = j % divOff;

                    if (cur == 0 && j > 0)
                        curStep += stepSmall;
                    else if (cur + 1 == divisor / 2 && doubleDiv)
                        curStep += stepSmall;

                    x = lineX + (stepSmall * j + curStep) / 1000f * noteStep;
                    subBpmOffsets[current.Z + j] = ((float)x, 0, 1, 0);
                }

                current += pointMetrics;
            }

            RegisterData(7, startBpmOffsets);
            RegisterData(8, fullBpmOffsets);
            RegisterData(9, halfBpmOffsets);
            RegisterData(10, subBpmOffsets);
            RegisterData(11, new Vector4[1] { pointHoverOffset });
            RegisterData(12, new Vector4[1] { pointSelectOffset });

            Pool.Return(startBpmOffsets, true);
            Pool.Return(fullBpmOffsets, true);
            Pool.Return(halfBpmOffsets, true);
            Pool.Return(subBpmOffsets, true);
        }

        private float prevHeight = 0;

        public override void Render(float mousex, float mousey, float frametime)
        {
            //Console.WriteLine(frametime * 1000);
            if (prevHeight != Rect.Height)
            {
                InstanceSetup();

                prevHeight = Rect.Height;
            }

            Update();

            // render background
            GL.UseProgram(Shader.Program);

            GL.BindVertexArray(VaO);
            var offset = 30;
            GL.DrawArrays(PrimitiveType.Triangles, 0, offset);

            // render waveform
            if (Settings.settings["waveform"])
                Waveform.Render(PosSet, Rect.Height);

            // render dynamic elements
            GenerateOffsets();

            // render static elements
            GL.UseProgram(Shader.Program);

            GL.BindVertexArray(VaO);
            var length = Indices["rectLength"] + Indices["loopLength"] + Indices["lineLength"] - 30;
            GL.DrawArrays(PrimitiveType.Triangles, offset, length);

            var editor = MainWindow.Instance;

            var currentTime = Settings.settings["currentTime"].Value;
            var sfxOffset = Settings.settings["sfxOffset"];
            var beatDivisor = Settings.settings["beatDivisor"].Value + 1f;

            //play metronome
            if (Settings.settings["metronome"])
            {
                var ms = currentTime - sfxOffset;
                var bpm = editor.GetCurrentBpm(currentTime);
                var interval = 60000f / bpm.BPM / beatDivisor;
                var remainder = (ms - bpm.Ms) % interval;
                var closestMs = ms - remainder;

                if (lastPlayedTick != closestMs && remainder >= 0 && editor.MusicPlayer.IsPlaying)
                {
                    lastPlayedTick = closestMs;

                    editor.SoundPlayer.Play("metronome");
                }
            }
        }

        public override void RenderTexture()
        {
            var color1 = Settings.settings["color1"];
            var color2 = Settings.settings["color2"];

            GL.Uniform4f(TexColorLocation, color1.R / 255f, color1.G / 255f, color1.B / 255f, color1.A / 255f);
            FontRenderer.RenderData("main", color1Texts.ToArray());
            GL.Uniform4f(TexColorLocation, color2.R / 255f, color2.G / 255f, color2.B / 255f, color2.A / 255f);
            FontRenderer.RenderData("main", color2Texts.ToArray());
        }

        private List<float> vertices = new();

        private List<float> loops = new();
        private List<float> lines = new();

        private List<Vector4> color1Texts = new();
        private List<Vector4> color2Texts = new();

        public override Tuple<float[], float[]> GetVertices()
        {
            loops = new();
            lines = new();

            vertices = new(GLU.Rect(Rect, 0.15f, 0.15f, 0.15f, Settings.settings["trackOpacity"] / 255f));
            loops.AddRange(GLU.OutlineAsTriangles(Rect, 1, 0.2f, 0.2f, 0.2f));

            var editor = MainWindow.Instance;
            var mouse = editor.Mouse;
            var noteStep = editor.NoteStep;

            var sc2 = Settings.settings["color2"];
            var color2 = new float[] { sc2.R / 255f, sc2.G / 255f, sc2.B / 255f };

            var currentTime = Settings.settings["currentTime"].Value;
            var totalTime = Settings.settings["currentTime"].Max;

            var posX = currentTime / 1000f * noteStep;
            var maxX = totalTime / 1000f * noteStep;
            var cursorX = Rect.Width * Settings.settings["cursorPos"].Value / 100f;
            var endX = cursorX - posX + maxX + 1;

            StartPos = Math.Max(0, (-cursorX * 1000f / noteStep + currentTime) / totalTime);
            EndPos = Math.Min(1, ((Rect.Width - cursorX) * 1000f / noteStep + currentTime) / totalTime);

            PosSet = (-posX + cursorX, endX, StartPos, EndPos);
            
            HoveringNote = null;
            HoveringPoint = null;

            selecting = editor.RightHeld && RightDraggingTrack;
            selectHitbox = new RectangleF();

            if (selecting)
            {
                var offsetMs = DragStartMs - currentTime;
                var startX = DragStartPoint.X + offsetMs / 1000f * noteStep;

                var my = MathHelper.Clamp(mouse.Y, 0f, Rect.Height);
                float x = Math.Min(mouse.X, startX);
                float y = Math.Min(my, DragStartPoint.Y);
                float w = Math.Max(mouse.X, startX) - x;
                float h = Math.Min(Rect.Height, Math.Max(my, DragStartPoint.Y) - y);

                selectHitbox = new RectangleF(x, y, w, h);

                vertices.AddRange(GLU.Rect(selectHitbox, 0f, 1f, 0.2f, 0.2f));
                loops.AddRange(GLU.OutlineAsTriangles(selectHitbox, 1, 0f, 1f, 0.2f, 1f));
            }

            //render static lines
            lines.AddRange(GLU.Line(cursorX - posX, 0, cursorX - posX, Rect.Height, 1, color2));
            lines.AddRange(GLU.Line(endX, 0, endX, Rect.Height, 1, 1f, 0f, 0f));
            lines.AddRange(GLU.Line(cursorX, 4, cursorX, Rect.Height - 4, 1, 1f, 1f, 1f, 0.75f));

            Indices["rectLength"] = vertices.Count / 6;
            Indices["loopLength"] = loops.Count / 6;
            Indices["lineLength"] = lines.Count / 6;

            vertices.AddRange(loops);
            vertices.AddRange(lines);

            return new Tuple<float[], float[]>(vertices.ToArray(), Array.Empty<float>());
        }

        public override void OnMouseClick(Point pos, bool right = false)
        {
            if (right)
                OnMouseUp(pos);

            var startMs = (long)Settings.settings["currentTime"].Value;
            var editor = MainWindow.Instance;

            var replayf = editor.MusicPlayer.IsPlaying && !right;
            replay = false;

            if (replayf)
                editor.MusicPlayer.Pause();

            if (HoveringNote != null && !right)
            {
                DraggingNote = HoveringNote;

                DragStartPoint = pos;
                DragStartMs = startMs;

                var selected = editor.SelectedNotes.ToList();

                if (editor.ShiftHeld && selected.Count > 0)
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

                foreach (var note in editor.SelectedNotes)
                    note.DragStartMs = note.Ms;
            }
            else if (HoveringPoint != null && !right)
            {
                DraggingPoint = HoveringPoint;

                DragStartPoint = pos;
                DragStartMs = startMs;

                editor.SelectedPoint = HoveringPoint;

                DraggingPoint.DragStartMs = DraggingPoint.Ms;
            }
            else
            {
                replay = replayf;

                DragStartPoint = pos;
                DragStartMs = startMs;
            }

            RightDraggingTrack |= right && Rect.Contains(pos);
            DraggingTrack |= !right;
        }

        public override void OnMouseMove(Point pos)
        {
            if (DraggingTrack)
            {
                var editor = MainWindow.Instance;
                var currentTime = Settings.settings["currentTime"];
                var divisor = Settings.settings["beatDivisor"].Value;
                var cursorPos = Settings.settings["cursorPos"].Value;

                var cellStep = editor.NoteStep;

                var offset = (pos.X - DragStartPoint.X) / cellStep * 1000f;
                var cursorms = (pos.X - Rect.Width * cursorPos / 100f - noteSize / 2f) / cellStep * 1000f + currentTime.Value;

                if (DraggingNote != null)
                {
                    offset = DraggingNote.DragStartMs - cursorms;
                    offset = Math.Abs(offset) / 1000f * cellStep <= 5f ? 0 : offset;
                    var currentBpm = editor.GetCurrentBpm(cursorms).BPM;

                    if (currentBpm > 0)
                    {
                        var stepX = 60f / currentBpm * cellStep;
                        var stepXSmall = stepX / divisor;

                        var threshold = MathHelper.Clamp(stepXSmall / 1.75f, 1f, 12f);
                        var snappedMs = editor.GetClosestBeat(DraggingNote.Ms);

                        if (Math.Abs(snappedMs - cursorms) / 1000f * cellStep <= threshold)
                            offset = DraggingNote.DragStartMs - snappedMs;
                    }

                    foreach (var note in editor.SelectedNotes)
                        note.Ms = (long)MathHelper.Clamp(note.DragStartMs - offset, 0f, currentTime.Max);

                    editor.SortNotes();
                }
                else if (DraggingPoint != null)
                {
                    offset = DraggingPoint.DragStartMs - cursorms;
                    var currentBpm = editor.GetCurrentBpm(cursorms).BPM;

                    var stepX = 60f / currentBpm * cellStep;
                    var stepXSmall = stepX / divisor;

                    var threshold = MathHelper.Clamp(stepXSmall / 1.75f, 1f, 12f);
                    var snappedMs = editor.GetClosestBeat(DraggingPoint.Ms, true);
                    var snappedNote = editor.GetClosestNote(DraggingPoint.Ms);

                    if (Math.Abs(snappedNote - cursorms) < Math.Abs(snappedMs - cursorms))
                        snappedMs = snappedNote;
                    if (Math.Abs(snappedMs - cursorms) / 1000f * cellStep <= threshold)
                        offset = DraggingPoint.DragStartMs - snappedMs;
                    if (Math.Abs(cursorms) / 1000f * cellStep <= threshold)
                        offset = DraggingPoint.DragStartMs;

                    DraggingPoint.Ms = (long)Math.Min(DraggingPoint.DragStartMs - offset, currentTime.Max);

                    editor.SortTimings(false);
                }
                else
                {
                    var finalTime = DragStartMs - offset;

                    if (editor.GetCurrentBpm(finalTime).BPM > 0)
                        finalTime = editor.GetClosestBeat(finalTime);

                    finalTime = MathHelper.Clamp(finalTime, 0f, currentTime.Max);

                    currentTime.Value = finalTime;
                }
            }
        }

        public override void OnMouseUp(Point pos)
        {
            if (DraggingTrack)
            {
                var editor = MainWindow.Instance;

                if (DraggingNote != null && DraggingNote.DragStartMs != DraggingNote.Ms)
                {
                    var selected = editor.SelectedNotes.ToList();
                    var startList = new List<long>();
                    var msList = new List<long>();

                    for (int i = 0; i < selected.Count; i++)
                    {
                        startList.Add(selected[i].DragStartMs);
                        msList.Add(selected[i].Ms);
                    }

                    editor.UndoRedoManager.Add($"MOVE NOTE{(selected.Count > 1 ? "S" : "")}", () =>
                    {
                        for (int i = 0; i < selected.Count; i++)
                            selected[i].Ms = startList[i];

                        editor.SortNotes();
                    }, () =>
                    {
                        for (int i = 0; i < selected.Count; i++)
                            selected[i].Ms = msList[i];

                        editor.SortNotes();
                    }, false);
                }
                else if (DraggingPoint != null && DraggingPoint.DragStartMs != DraggingPoint.Ms)
                {
                    var point = DraggingPoint;
                    var ms = point.Ms;
                    var msStart = point.DragStartMs;

                    editor.UndoRedoManager.Add("MOVE POINT", () =>
                    {
                        point.Ms = msStart;

                        editor.SortTimings();
                    }, () =>
                    {
                        point.Ms = ms;

                        editor.SortTimings();
                    }, false);

                    TimingsWindow.Instance?.ResetList();
                }

                DraggingTrack = false;
                DraggingNote = null;
                DraggingPoint = null;

                if (replay)
                    editor.MusicPlayer.Play();
            }

            if (RightDraggingTrack)
                RightDraggingTrack = false;
        }
    }
}
