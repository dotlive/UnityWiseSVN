using DevLocker.VersionControl.WiseSVN.ContextMenus;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DevLocker.VersionControl.WiseSVN
{
	/// <summary>
	/// Popup that shows found branches. User can open "Repo Browser" or "Show log" for the selected file.
	/// </summary>
	public class SVNBranchSelectorWindow : EditorWindow
	{
		private bool m_Initialized = false;

		private Object m_TargetAsset;
		private string m_BranchFilter;
		private Vector2 m_BranchesScroll;

		private const string WindowSizePrefsKey = "SVNBranchSelectorWindow";

		private SVNBranchesDatabase Database => SVNBranchesDatabase.Instance;

		private GUIStyle BorderStyle;

		private GUIContent RefreshBranchesContent;


		private GUIStyle WindowTitleStyle;
		private GUIStyle ToolbarTitleStyle;
		private GUIStyle ToolbarLabelStyle;

		private GUIStyle SearchFieldStyle;
		private GUIStyle SearchFieldCancelStyle;
		private GUIStyle SearchFieldCancelEmptyStyle;

		private GUIContent RepoBrowserContent;
		private GUIContent ShowLogContent;
		private GUIContent SwitchBranchContent;
		private GUIContent CheckForConflictsContent;

		private GUIStyle MiniIconButtonlessStyle;

		private GUIStyle BranchLabelStyle;

		private readonly string[] LoadingDots = new[] { ".  ", ".. ", "..." };

		[MenuItem("Assets/SVN/Branch Selector", false, -490)]
		private static void OpenBranchesSelector()
		{
			var window = CreateInstance<SVNBranchSelectorWindow>();
			window.titleContent = new GUIContent("Branch Selector");

			var assetPath = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
			window.m_TargetAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);

			window.ShowUtility();
		}

		private void InitializeStyles()
		{
			BorderStyle = new GUIStyle(GUI.skin.box);
			BorderStyle.padding = new RectOffset(1, 1, 1, 1);
			BorderStyle.margin = new RectOffset();

			const string refreshBranchesHint = "Refresh branches cache database.\n\nNOTE: Single scan may take up to a few minutes, depending on your network connection and the complexity of your repository.";
			RefreshBranchesContent = new GUIContent(EditorGUIUtility.FindTexture("Refresh"), refreshBranchesHint);

			WindowTitleStyle = new GUIStyle(EditorStyles.toolbarButton);
			WindowTitleStyle.font = EditorStyles.boldFont;
			WindowTitleStyle.normal.background = null;


			ToolbarLabelStyle = new GUIStyle(EditorStyles.toolbarButton);
			ToolbarLabelStyle.normal.background = null;
			ToolbarLabelStyle.alignment = TextAnchor.MiddleLeft;
			var padding = ToolbarLabelStyle.padding;
			padding.bottom = 2;
			ToolbarLabelStyle.padding = padding;

			ToolbarTitleStyle = new GUIStyle(ToolbarLabelStyle);
			ToolbarTitleStyle.font = EditorStyles.boldFont;

			SearchFieldStyle = GUI.skin.GetStyle("ToolbarSeachTextField");
			SearchFieldCancelStyle = GUI.skin.GetStyle("ToolbarSeachCancelButton");
			SearchFieldCancelEmptyStyle = GUI.skin.GetStyle("ToolbarSeachCancelButtonEmpty");

			RepoBrowserContent = new GUIContent(Resources.Load<Texture2D>("Editor/BranchesIcons/SVN-RepoBrowser"), "Repo-Browser in this branch at the target asset.");
			ShowLogContent = new GUIContent(Resources.Load<Texture2D>("Editor/BranchesIcons/SVN-ShowLog"), "Show Log in this branch at the target asset.");
			SwitchBranchContent = new GUIContent(Resources.Load<Texture2D>("Editor/BranchesIcons/SVN-Switch"), "Switch working copy to another branch.\nOpens TortoiseSVN dialog.");
			CheckForConflictsContent = new GUIContent(Resources.Load<Texture2D>("Editor/BranchesIcons/SVN-CheckForConflicts"), "Scan all branches for potential conflicts.\nThis will look for any changes made to the target asset in the branches.");

			if (RepoBrowserContent.image == null) RepoBrowserContent.text = "R";
			if (ShowLogContent.image == null) ShowLogContent.text = "L";
			if (SwitchBranchContent.image == null) SwitchBranchContent.text = "S";
			if (CheckForConflictsContent.image == null) CheckForConflictsContent.text = "C";

			MiniIconButtonlessStyle = new GUIStyle(GUI.skin.button);
			MiniIconButtonlessStyle.hover.background = MiniIconButtonlessStyle.normal.background;
			MiniIconButtonlessStyle.normal.background = null;
			MiniIconButtonlessStyle.padding = new RectOffset();
			MiniIconButtonlessStyle.margin = new RectOffset();
			wantsMouseMove = true;	// Needed for the hover effects.

			BranchLabelStyle = new GUIStyle(GUI.skin.label);
			BranchLabelStyle.alignment = TextAnchor.MiddleLeft;
			var margin = BranchLabelStyle.margin;
			margin.top += 2;
			BranchLabelStyle.margin = margin;
		}

		// This is initialized on first OnGUI rather upon creation because it gets overriden.
		private void InitializePositionAndSize()
		{
			Vector2 size = new Vector2(350f, 300);
			minSize = size;

			var sizeData = EditorPrefs.GetString(WindowSizePrefsKey);
			if (!string.IsNullOrEmpty(sizeData)) {
				var sizeArr = sizeData.Split(';');
				size.x = float.Parse(sizeArr[0]);
				size.y = float.Parse(sizeArr[1]);
			}

			// TODO: How will this behave with two monitors?
			var center = new Vector2(Screen.currentResolution.width, Screen.currentResolution.height) / 2f;
			Rect popupRect = new Rect(center - size / 2, size);

			position = popupRect;
		}

		void OnEnable()
		{
			Database.DatabaseChanged -= Repaint;
			Database.DatabaseChanged += Repaint;
		}

		private void OnDisable()
		{
			Database.DatabaseChanged -= Repaint;
			EditorPrefs.SetString(WindowSizePrefsKey, $"{position.width};{position.height}");
		}

		void OnGUI()
		{
			if (!m_Initialized) {
				InitializeStyles();
				InitializePositionAndSize();

				m_Initialized = true;
			}

			EditorGUILayout.BeginVertical(BorderStyle);

			DrawContent();

			EditorGUILayout.EndVertical();
		}

		private void DrawContent()
		{
			using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {

				GUILayout.Label("Asset:", ToolbarTitleStyle, GUILayout.Width(60f));

				var prevColor = GUI.backgroundColor;
				GUI.backgroundColor = m_TargetAsset == null ? new Color(0.93f, 0.40f, 0.40f) : prevColor;

				m_TargetAsset = EditorGUILayout.ObjectField(m_TargetAsset, m_TargetAsset ? m_TargetAsset.GetType() : typeof(Object), false, GUILayout.ExpandWidth(true));

				GUI.backgroundColor = prevColor;

				GUILayout.Space(24f);

				if (GUILayout.Button(CheckForConflictsContent, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false))) {
					// TODO: ... start procedure...
				}
			}

			using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {

				GUILayout.Label("Search:", ToolbarTitleStyle, GUILayout.Width(60f));

				m_BranchFilter = EditorGUILayout.TextField(m_BranchFilter, SearchFieldStyle);

				if (GUILayout.Button(" ", string.IsNullOrEmpty(m_BranchFilter) ? SearchFieldCancelEmptyStyle : SearchFieldCancelStyle)) {
					m_BranchFilter = "";
					GUI.FocusControl("");
					Repaint();
				}

				if (GUILayout.Button(RefreshBranchesContent, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false))) {
					Database.InvalidateDatabase();
				}
			}


			using (new EditorGUILayout.VerticalScope()) {

				if (Database.IsUpdating) {
					EditorGUILayout.LabelField("Scanning branches for Unity projects...", GUILayout.ExpandHeight(true));
				} else if (m_TargetAsset == null) {
					EditorGUILayout.LabelField("Please select target asset....", GUILayout.ExpandHeight(true));
				} else {
					DrawBranchesList();
				}
			}

			using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true))) {
				DrawStatusBar();
			}
		}

		private void DrawBranchesList()
		{
			using (var scrollView = new EditorGUILayout.ScrollViewScope(m_BranchesScroll)) {
				m_BranchesScroll = scrollView.scrollPosition;

				// For hover effects to work.
				if (Event.current.type == EventType.MouseMove) {
					Repaint();
				}

				// TODO: Sort list by folder depths: compare by lastIndexOf('/'). If equal, by string.

				foreach (var branchProject in Database.BranchProjects) {
					if (!string.IsNullOrEmpty(m_BranchFilter) && branchProject.BranchName.IndexOf(m_BranchFilter, System.StringComparison.OrdinalIgnoreCase) == -1)
						continue;

					using (new EditorGUILayout.HorizontalScope(/*BranchRowStyle*/)) {

						float buttonSize = 24f;
						bool repoBrowser = GUILayout.Button(RepoBrowserContent, MiniIconButtonlessStyle, GUILayout.Height(buttonSize), GUILayout.Width(buttonSize));
						bool showLog = GUILayout.Button(ShowLogContent, MiniIconButtonlessStyle, GUILayout.Height(buttonSize), GUILayout.Width(buttonSize));
						bool switchBranch = GUILayout.Button(SwitchBranchContent, MiniIconButtonlessStyle, GUILayout.Height(buttonSize), GUILayout.Width(buttonSize));

						GUILayout.Label(new GUIContent(branchProject.BranchRelativePath, branchProject.BranchURL), BranchLabelStyle);

						if (repoBrowser) {
							SVNContextMenusManager.RepoBrowser(branchProject.UnityProjectURL + "/" + AssetDatabase.GetAssetPath(m_TargetAsset));
						}

						if (showLog) {
							SVNContextMenusManager.ShowLog(branchProject.UnityProjectURL + "/" + AssetDatabase.GetAssetPath(m_TargetAsset));
						}

						if (switchBranch) {
							bool confirm = EditorUtility.DisplayDialog("Switch Operation",
								"Unity needs to be closed while switching. Do you want to close it?\n\n" +
								"Reason: if Unity starts crunching assets while SVN is downloading files, the Library may get corrupted.",
								"Yes!", "No"
								);
							if (confirm && UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
								var localPath = WiseSVNIntegration.WorkingCopyRootPath();
								var targetUrl = branchProject.BranchURL;

								if (branchProject.BranchURL != branchProject.UnityProjectURL) {
									bool useBranchRoot = EditorUtility.DisplayDialog("Switch what?",
										"What do you want to switch?\n" +
										"- Working copy root (the whole checkout)\n" +
										"- Unity project folder",
										"Working copy root", "Unity project");
									if (!useBranchRoot) {
										localPath = WiseSVNIntegration.ProjectRoot;
										targetUrl = branchProject.UnityProjectURL;
									}
								}

								SVNContextMenusManager.Switch(localPath, targetUrl);
								EditorApplication.Exit(0);
							}
						}
					}

					var rect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true), GUILayout.Height(1f));
					EditorGUI.DrawRect(rect, Color.black);
				}

			}
		}

		private void DrawStatusBar()
		{
			if (Database.LastError != ListOperationResult.Success) {
				GUILayout.Label($"Error scanning branches: {ObjectNames.NicifyVariableName(Database.LastError.ToString())}", ToolbarLabelStyle, GUILayout.ExpandWidth(false));
				return;
			}

			if (Database.IsUpdating) {
				int dots = ((int)EditorApplication.timeSinceStartup) % 3;
				GUILayout.Label($"Scanning{LoadingDots[dots]}", ToolbarLabelStyle, GUILayout.ExpandWidth(false));
				Repaint();
				return;
			}

			GUILayout.Label($"Branches: {Database.BranchProjects.Count}", ToolbarLabelStyle, GUILayout.ExpandWidth(false));
		}
	}
}