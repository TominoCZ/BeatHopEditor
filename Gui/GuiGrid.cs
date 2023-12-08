using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;

namespace SoundSpaceHopEditor.Gui
{
	class GuiGrid : Gui
	{
		public Note MouseOverNote;

		private readonly Note _startNote = new Note(1, 0, 0);

		public GuiGrid(float sx, float sy) : base(EditorWindow.Instance.ClientSize.Width / 2f - sx / 2, EditorWindow.Instance.ClientSize.Height / 2f - sy / 2, sx, sy)
		{

		}

		public override void Render(float delta, float mouseX, float mouseY)
		{
			var editor = (GuiScreenEditor)EditorWindow.Instance.GuiScreen;

			var rect = ClientRectangle;
			var mouseOver = false;

			var cellSize = rect.Width / 5f;
			var noteSize = cellSize * 0.75f;

			var divVal = editor.GridDivider.Value + 1;
			if (divVal > 1)
			{
				var div = 1.0 / divVal;

				GL.LineWidth(1);
				GL.Begin(PrimitiveType.Lines);

				for (double x = 0; x - 0.005 <= 4; x += div)
				{
					var lx = (int)(rect.X + cellSize / 2 + x * cellSize);

					GL.Color3(0.1, 0.1, 0.1);
					GL.Vertex2(lx + 0.5, editor.Track.ClientRectangle.Bottom);
					GL.Color3(0.35f, 0.35f, 0.35f);
					GL.Vertex2(lx + 0.5, rect.Y);

					//Glu.RenderQuad(lx, 0, 1, rect.Y);
				}

				GL.End();
			}

			GL.LineWidth(4);
			GL.Begin(PrimitiveType.Lines);
			GL.Color3(0.1, 0.1, 0.1);
			GL.Vertex2((int)rect.Left - 2, editor.Track.ClientRectangle.Bottom);
			GL.Color3(0.5f, 0.5f, 0.5f);
			GL.Vertex2((int)rect.Left - 2, (int)rect.Bottom + 1);

			GL.Color3(0.1, 0.1, 0.1);
			GL.Vertex2((int)(rect.Right + 3), editor.Track.ClientRectangle.Bottom);
			GL.Color3(0.5f, 0.5f, 0.5f);
			GL.Vertex2((int)(rect.Right + 3), (int)rect.Bottom + 1);

			GL.Color3(0.1f, 0.1f, 0.1f);

			GL.End();
			GL.LineWidth(1);

			Glu.RenderQuad(rect.X, rect.Y, rect.Width, rect.Height);

			var gap = cellSize - noteSize;

			var audioTime = EditorWindow.Instance.MusicPlayer.CurrentTime.TotalMilliseconds;

			GL.Color3(0.2, 0.2, 0.2);

			for (int y = 0; y <= 1; y++)
			{
				var ly = y * cellSize;

				Glu.RenderQuad((int)(rect.X), (int)(rect.Y + ly), rect.Width + 1, 1);
			}

			for (int x = 0; x <= 5; x++)
			{
				var lx = x * cellSize;

				Glu.RenderQuad((int)(rect.X + lx), (int)(rect.Y), 1, rect.Height + 1);
			}

			/*
			if (divVal > 1)
			{
				var div = 1.0 / divVal;
				GL.Color3(0.25f, 0.5f, 0f);
				var h = rect.Height * 0.075;
				for (double x = 0; x - 0.005 <= 4; x += div)
				{
					var lx = cellSize / 2 + x * cellSize;

					Glu.RenderQuad((int)(rect.X + lx - 1), (int)(rect.Bottom) - h, 3, h);
				}
			}*/

			var fr = EditorWindow.Instance.FontRenderer;

			GL.Color3(0.2f, 0.2f, 0.2f);
			foreach (var pair in EditorWindow.Instance.KeyMapping)
			{
				if (pair.Key == Key.Y)
					continue;

				var letter = pair.Key == Key.Z ? "Y/Z" : pair.Key.ToString();

				var x = rect.X + pair.Value * cellSize + cellSize / 2;
				var y = rect.Y + cellSize / 2;

				var width = fr.GetWidth(letter, 38);
				var height = fr.GetHeight(38);

				fr.Render(letter, (int)(x - width / 2f), (int)(y - height / 2), 38);
			}

			Note last = null;
			Note next = null;

			float noteSpeed = 600 * EditorWindow.Instance.Zoom;
			var trackLength = Math.Abs(ClientRectangle.Y + ClientRectangle.Height / 2);
			var trackTimeLength = trackLength / noteSpeed;

			for (var index = 0; index < EditorWindow.Instance.Notes.Count; index++)
			{
				var note = EditorWindow.Instance.Notes[index];
				var passed = audioTime > note.Ms + 1;
				var visible = !passed && note.Ms - audioTime <= trackTimeLength * 1000;

				if (passed)
				{
					last = note;
				}
				else if (next == null)
				{
					next = note;
				}

				if (!visible)
				{
					if (passed && next != null)
					{
						break;
					}

					continue;
				}

				//var progress = (float)Math.Pow(1 - Math.Min(1, (note.Ms - audioTime) / 750.0), 2);
				var seconds = (float)(note.Ms - audioTime) / 1000;
				var progress = (float)(1 - seconds / trackTimeLength);//Math.Pow(1 - Math.Min(1, seconds / trackTimeLength), 0.75);

				var cellX = rect.X + note.X * cellSize;
				var cellY = rect.Y - seconds * noteSpeed;

				var x = cellX + gap / 2;
				var y = cellY + gap / 2;

				var noteRect = new RectangleF(x, y, noteSize, noteSize);
				GL.Color4(note.Color.R, note.Color.G, note.Color.B, progress * 0.15f);
				Glu.RenderQuad(noteRect);
				GL.Color4(note.Color.R, note.Color.G, note.Color.B, progress);
				Glu.RenderOutline(noteRect);

				if (divVal > 1)
				{
					var lx = (int)(cellX + cellSize / 2 - 1);
					Glu.RenderQuad(lx, cellY + gap/2 - 2, 3, 5);
					Glu.RenderQuad(lx, cellY + cellSize - gap / 2 - 3, 3, 5);
				}

				if (editor.ApproachSquares.Toggle)
				{
					var outlineSize = 4 + noteSize + noteSize * (1 - progress) * 2;
					Glu.RenderOutline(x - outlineSize / 2 + noteSize / 2, rect.Y + gap / 2 - outlineSize / 2 + noteSize / 2,
						outlineSize,
						outlineSize);
				}

				if (editor.GridNumbers.Toggle)
				{
					GL.Color4(1, 1, 1, progress);
					var s = $"{(index + 1):#,##}";
					var w = fr.GetWidth(s, 24);
					var h = fr.GetHeight(24);

					fr.Render(s, (int)(noteRect.X + noteRect.Width / 2 - w / 2f),
						(int)(noteRect.Y + noteRect.Height / 2 - h / 2f), 24);
				}

				if (!mouseOver)
				{
					MouseOverNote = null;
				}

				if (EditorWindow.Instance.SelectedNotes.Contains(note))
				{
					var outlineSize = noteSize + 8;

					GL.Color4(0, 0.5f, 1f, progress);
					Glu.RenderOutline(x - outlineSize / 2 + noteSize / 2, y - outlineSize / 2 + noteSize / 2,
						outlineSize, outlineSize);
				}

				if (!mouseOver && noteRect.Contains(mouseX, mouseY))
				{
					MouseOverNote = note;
					mouseOver = true;

					GL.Color3(0, 1, 0.25f);
					Glu.RenderOutline(x - 4, y - 4, noteSize + 8, noteSize + 8);
				}
			}

			//RENDER AUTOPLAY
			if (editor.Autoplay.Toggle)
			{
				RenderAutoPlay(last, next, cellSize, rect, audioTime);
			}
		}

		private void RenderAutoPlay(Note last, Note next, float cellSize, RectangleF rect, double audioTime)
		{
			if (last == null)
				last = _startNote;

			if (next == null)
				next = last;

			var timeDiff = next.Ms - last.Ms;
			var timePos = audioTime - last.Ms;

			var progress = timeDiff == 0 ? 1 : (float)timePos / timeDiff;

			progress = (float)Math.Sin(progress * MathHelper.PiOver2);

			var s = (float)Math.Sin(progress * MathHelper.Pi) * 8 + 16;

			var lx = rect.X + last.X * cellSize;
			var ly = rect.Y;

			var nx = rect.X + next.X * cellSize;
			var ny = rect.Y;

			var x = cellSize / 2 + lx + (nx - lx) * progress;
			var y = cellSize / 2 + ly + (ny - ly) * progress;

			GL.Color4(1, 1, 1, 0.25f);
			Glu.RenderCircle(x, y, s, 20);

			GL.PolygonMode(MaterialFace.Front, PolygonMode.Line);
			GL.Color4(1, 1, 1, 1f);
			GL.LineWidth(2);
			Glu.RenderCircle(x, y, s, 20);
			GL.LineWidth(1);
			GL.PolygonMode(MaterialFace.Front, PolygonMode.Fill);
		}
	}
}