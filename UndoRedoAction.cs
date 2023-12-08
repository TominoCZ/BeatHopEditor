﻿using System;

namespace SoundSpaceHopEditor
{
	class UndoRedoAction
	{
		public Action Undo;
		public Action Redo;

		public bool Undone;

		public string Label;

		public UndoRedoAction(string label, Action undo, Action redo)
		{
			Label = label;

			Undo = undo;
			Redo = redo;
		}
	}
}