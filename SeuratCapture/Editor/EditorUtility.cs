using UnityEditor;

namespace Seurat
{
	public static class EditorUtility
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
}
