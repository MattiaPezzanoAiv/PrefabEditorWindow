using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

using UnityEngine.UIElements;

public class PrefabsSelectorWindow : EditorWindow
{
    [System.Serializable]
    public struct Settings
    {
        public List<string> Paths;
    }

    private const string c_settingsPath = "/Editor/PrefabSelection/Settings.txt";

    private const float c_sideMenuWidthPerc = 0.2f;
    private const float c_mainMenuWidthPerc = 0.8f;
    private const float c_height = 500f;
    private const float c_itemSize = 80;
    private const float c_labelHeight = 15;

    private float SideMenuWidth => this.position.width * c_sideMenuWidthPerc;
    private float MainManuWidth => this.position.width * c_mainMenuWidthPerc;


    [MenuItem("MP/Prefabs Selector")]
    public static void Init()
    {
        GetWindow<PrefabsSelectorWindow>().Show();
    }

    private Settings? m_settings;

    private void LoadSettings()
    {
        var basePath = Application.dataPath;

        bool dirty = false;
        if (!Directory.Exists(basePath + "/Editor"))
        {
            Directory.CreateDirectory(basePath + "/Editor");
            dirty = true;
        }

        if (!Directory.Exists(basePath + "/Editor/PrefabSelection"))
        {
            Directory.CreateDirectory(basePath + "/Editor/PrefabSelection");
            dirty = true;
        }

        if (dirty)
        {
            AssetDatabase.Refresh();
            dirty = false;
        }

        Settings settings;
        if (!File.Exists(basePath + c_settingsPath))
        {
            using (var sw = new StreamWriter(File.Create(basePath + c_settingsPath)))
            {
                settings.Paths = new List<string>();
                var json = JsonUtility.ToJson(settings, true);
                sw.Write(json);
            }
        }
        else
        {
            var json = File.ReadAllText(basePath + c_settingsPath);
            settings = JsonUtility.FromJson<Settings>(json);
        }

        m_settings = settings;

        if (dirty)
        {
            AssetDatabase.Refresh();
        }

        RefreshGuids();
    }

    private void EnsureSettingsAreLoaded()
    {
        if (m_settings == null)
        {
            LoadSettings();
        }
    }

    private int m_sideSelection;

    private Vector2 m_settingsScroll;

    private Vector2 m_prefabsScroll;

    private string m_settingsSearchString;

    private Dictionary<string, List<string>> m_assetGuidsMap;

    private List<bool> m_assetsFoldout;

    private void OnGUI()
    {
        EnsureSettingsAreLoaded();

        EditorGUILayout.BeginHorizontal();
        {
            var sideStyle = new GUIStyle();
            var c = new Color(0.6f, 0.6f, 0.6f);
            var tex = new Texture2D(2, 2);
            tex.SetPixels(new Color[] { c, c, c, c });
            tex.Apply();
            sideStyle.normal.background = tex;
            sideStyle.fixedWidth = SideMenuWidth;
            EditorGUILayout.BeginVertical(sideStyle, GUILayout.Height(this.position.height));
            {
                DrawSideMenu();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.Width(MainManuWidth), GUILayout.Height(this.position.height));
            {
                if (m_sideSelection == 0)
                {
                    DrawMainContentMenu();
                }
                else if (m_sideSelection == 1)
                {
                    DrawMainSettingsMenu();
                }
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndHorizontal();

    }

    private void DrawSideMenu()
    {
        m_sideSelection = GUILayout.SelectionGrid(m_sideSelection, new string[] { "Prefabs", "Settings" }, 1, GUILayout.MaxWidth(SideMenuWidth));

        EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);

        if(m_sideSelection == 1)
        {
            if (GUILayout.Button("+", GUILayout.Width(20)))
            {
                m_settings.Value.Paths.Add(string.Empty);
            }

            if (GUILayout.Button("Save", GUILayout.Width(40)))
            {
                SaveSettings();
            }

            if (GUILayout.Button("Reload", GUILayout.Width(55)))
            {
                LoadSettings();
            }
        }
        else if (m_sideSelection == 0)
        {
            if (GUILayout.Button("Reload", GUILayout.Width(55)))
            {
                LoadSettings();
            }
        }
    }

    private void InstantiateNewItem(GameObject obj)
    {
        Instantiate(obj);
    }

    private void DrawMainContentMenu()
    {
        void DrawItem(GameObject obj)
        {
            var texture = AssetPreview.GetAssetPreview(obj);

            var layoutStyle = new GUIStyle { margin = new RectOffset { left = 5, right = 5 } };
            EditorGUILayout.BeginVertical(layoutStyle, GUILayout.Width(c_itemSize));
            {
                var labelStyle = new GUIStyle();
                var c = new Color(0.2f, 0.2f, 0.2f);
                var tex = new Texture2D(2, 2);
                tex.SetPixels(new Color[] { c, c, c, c });
                tex.Apply();
                labelStyle.normal.background = tex;
                labelStyle.normal.textColor = Color.white;
                labelStyle.fontStyle = FontStyle.BoldAndItalic;
                labelStyle.alignment = TextAnchor.MiddleCenter;
                EditorGUILayout.SelectableLabel(obj.name, labelStyle, GUILayout.Width(c_itemSize), GUILayout.Height(20));

                var buttonStyle = new GUIStyle();
                buttonStyle.normal.background = tex; 
                if (GUILayout.Button(texture, buttonStyle, GUILayout.Width(c_itemSize), GUILayout.Height(c_itemSize)))
                {
                    InstantiateNewItem(obj);
                }
            }
            EditorGUILayout.EndVertical();
        }

        if(m_assetGuidsMap == null)
        {
            RefreshGuids();
        }

        m_prefabsScroll = EditorGUILayout.BeginScrollView(m_prefabsScroll);

        int maxCols = (int)Mathf.Round(MainManuWidth / (c_itemSize)) - 1;
        int idx = 0;
        foreach (var guids in m_assetGuidsMap)
        {
            EditorGUILayout.Space(20);

            bool foldout = m_assetsFoldout[idx];
            foldout = EditorGUILayout.Foldout(
                foldout, 
                guids.Key, 
                true, 
                new GUIStyle { fontStyle = FontStyle.Bold, fontSize = 20, contentOffset = new Vector2(0, -5) });

            m_assetsFoldout[idx++] = foldout;

            if(!foldout)
            {
                // separator line
                EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
                continue;
            }

            int i = 0;
            int j = 0;
            EditorGUILayout.BeginHorizontal();
            foreach (var guid in guids.Value)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null)
                {
                    continue;
                }

                DrawItem(asset);

                i++;
                if (i >= maxCols)
                {
                    i = 0;
                    j++;

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(5);
                    EditorGUILayout.BeginHorizontal();
                }
            }
            EditorGUILayout.EndHorizontal();

            // separator line
            EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
        }

        EditorGUILayout.EndScrollView();
    }

    private void RefreshGuids()
    {
        m_assetGuidsMap = new Dictionary<string, List<string>>();
        m_assetsFoldout = new List<bool>();

        var stringArray = new string[] { string.Empty };
        foreach (var path in m_settings.Value.Paths)
        {
            stringArray[0] = $"Assets/{path}";
            var guids = AssetDatabase.FindAssets("t:prefab", stringArray).ToList();
            var folders = path.Split('/');
            m_assetGuidsMap.Add(folders[folders.Length - 1], guids);
            m_assetsFoldout.Add(true);
        }
    }

    private void DrawMainSettingsMenu()
    {
        EditorGUILayout.LabelField("All paths are reative to Assets folder.");

        m_settingsSearchString = GUILayout.TextField(
            m_settingsSearchString, 
            GUI.skin.FindStyle("ToolbarSeachTextField"), 
            GUILayout.Width(MainManuWidth * .8f));

        m_settingsScroll = EditorGUILayout.BeginScrollView(m_settingsScroll, GUILayout.MaxHeight(c_height));
        for (int i = 0; i < m_settings.Value.Paths.Count; i++)
        {
            if(!string.IsNullOrEmpty(m_settingsSearchString) && !StringContainsCaseInsensitive(m_settings.Value.Paths[i], m_settingsSearchString))
            {
                continue;
            }

            EditorGUILayout.BeginHorizontal();
            {
                EditorGUI.BeginChangeCheck();
                m_settings.Value.Paths[i] = EditorGUILayout.TextField(m_settings.Value.Paths[i], GUILayout.Width(MainManuWidth * .8f));

                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    m_settings.Value.Paths.RemoveAt(i);
                    i--;
                    continue;
                }

                if (GUILayout.Button("^", GUILayout.Width(20)))
                {
                    var selection = Selection.activeObject;
                    if(selection)
                    {
                        var path = AssetDatabase.GetAssetPath(selection).Replace("Assets/", "");
                        m_settings.Value.Paths[i] = path;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            // check if this path exists
            EditorGUILayout.BeginHorizontal();
            {
                var subPath = m_settings.Value.Paths[i].Replace("Assets/", "");
                if (!Directory.Exists($"{Application.dataPath}/{subPath}"))
                {
                    var guiStyle = new GUIStyle();
                    guiStyle.normal.textColor = Color.red;
                    EditorGUILayout.LabelField("     Path not found.", guiStyle);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void SaveSettings()
    {
        var json = JsonUtility.ToJson(m_settings);
        File.WriteAllText(Application.dataPath + c_settingsPath, json);

        RefreshGuids();
    }

    private bool StringContainsCaseInsensitive(string source, string searchString)
    {
        return source.IndexOf(searchString, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
