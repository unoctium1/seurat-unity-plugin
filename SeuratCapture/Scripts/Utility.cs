using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class Utility
{
    public delegate void DrawFoldoutSection();

	public static bool DrawMethodGroup(bool foldout, string label, DrawFoldoutSection method)
	{
		bool draw = EditorGUILayout.Foldout(foldout, label);
		if (draw)
		{
			EditorGUI.indentLevel++;
			method();
			EditorGUI.indentLevel--;
		}
		return draw;
	}
}
