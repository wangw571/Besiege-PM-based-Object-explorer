using PluginManager.Plugin;
using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEngine;

namespace UniversalObjExplorerNameSpace
{
    [OnGameInit]
    public class UniversalObjExplorer : MonoBehaviour
    {
        public UniversalObjExplorer()
        {
        }

        public void Start()
        {
            DontDestroyOnLoad(this.gameObject);
            ObjectExplorer.Initialize();
        }
    }    
    [OnGameInit]
    public class ObjectExplorer : SingleInstance<ObjectExplorer>
    {
        public int UpdateRate = 1000;
        public override string Name
        {
            get { return "Object Explorer"; }
        }

        public string WindowTitle = "Object Explorer";

        public HierarchyPanel HierarchyPanel { get; private set; }
        public InspectorPanel InspectorPanel { get; private set; }

        private readonly int windowID = Util.GetWindowID();
        private Rect windowRect = new Rect(20, 20, 800, 600);

        public bool IsVisible = true;

        private void Start()
        {
            this.transform.parent = Instance.transform;

            HierarchyPanel = gameObject.AddComponent<HierarchyPanel>();
            InspectorPanel = gameObject.AddComponent<InspectorPanel>();

        }

        private void Update()
        {
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.O))
            {
                IsVisible = !IsVisible;
                HierarchyPanel.UpdateRate = UpdateRate;
            }
        }

        private void OnGUI()
        {
            GUI.skin = ModGUI.Skin;

            if (IsVisible)
            {
                windowRect = GUILayout.Window(windowID, windowRect, DoWindow,
                  WindowTitle);
                windowRect = Util.PreventOffScreenWindow(windowRect);
            }
        }

        private void DoWindow(int id)
        {
            GUILayout.BeginHorizontal();

            HierarchyPanel.Display();
            InspectorPanel.Display();

            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0, 0, windowRect.width, GUI.skin.window.padding.top));
        }
    }
    [OnGameInit]
    public class HierarchyPanel : MonoBehaviour
    {
        public int UpdateRate = 1000;
        private const string SEARCH_FIELD_NAME = "search_field";
        private const string SEARCH_FIELD_DEFAULT = "Search";

        private HashSet<HierarchyEntry> inspectorEntries
          = new HashSet<HierarchyEntry>();
        private HashSet<HierarchyEntry> searchFilteredEntries
          = new HashSet<HierarchyEntry>();
        private Vector2 hierarchyScroll = Vector2.zero;

        private string searchFieldText = SEARCH_FIELD_DEFAULT;
        private bool isSearching = false;

        private bool shouldUpdate = false;

        private void Start()
        {
            RefreshGameObjectList();

            //shouldUpdate = Configuration.GetBool("objExpAutoUpdate", false);
            StartCoroutine(AutoUpdate());

            //Configuration.OnConfigurationChange += OnConfigurationChange;
            //SceneManager.sceneLoaded += OnSceneLoaded;
        }
        int i = 0;
        private void FixedUpdate()
        {
            ++i;
            if (i >= UpdateRate)
            {
                RefreshGameObjectList();
                i = 0;
            }
        }
        //private void OnConfigurationChange(object s, ConfigurationEventArgs e)
        //{
        //    var old = shouldUpdate;
        //    shouldUpdate = Configuration.GetBool("objExpAutoUpdate", false);
        //    if (!old)
        //    {
        //        StartCoroutine(AutoUpdate());
        //    }
        //}

        private System.Collections.IEnumerator AutoUpdate()
        {
            while (shouldUpdate)
            {
                yield return new WaitForSeconds(2f);
                if (ObjectExplorer.Instance.IsVisible)
                {
                    //RefreshGameObjectList();
                    Debug.Log("Update?");
                }
            }
        }

        public void Display()
        {
            GUILayout.BeginVertical();

            #region Buttons
            GUILayout.BeginHorizontal(
              GUILayout.Width(Elements.Settings.HierarchyPanelWidth));

            // Search field
            // Set the name of the  search field so we can later detect if it's in focus
            GUI.SetNextControlName(SEARCH_FIELD_NAME);
            const int SEARCH_FIELD_WIDTH = 160;
            var oldSearchText = searchFieldText;
            searchFieldText = GUILayout.TextField(oldSearchText,
              Elements.InputFields.ThinNoTopBotMargin,
              GUILayout.Width(SEARCH_FIELD_WIDTH));
            if (oldSearchText != searchFieldText)
            {
                RefreshSearchList();
            }

            // Expand/collapse all entries button
            bool allCollapsed = AreAllCollapsed(inspectorEntries);
            const int BUTTON_COLLAPSE_WIDTH = 90;
            if (GUILayout.Button(allCollapsed ? "Expand All" : "Collapse All",
              Elements.Buttons.ThinNoTopBotMargin, GUILayout.Width(BUTTON_COLLAPSE_WIDTH)))
            {
                if (allCollapsed)
                {
                    ExpandAll(inspectorEntries);
                }
                else
                {
                    CollapseAll(inspectorEntries);
                }
            }

            // Refresh list button
            if (GUILayout.Button("Refresh", Elements.Buttons.ThinNoTopBotMargin))
            {
                RefreshGameObjectList();
            }

            // Only during repaint, to avoid errors
            if (Event.current.type == EventType.Repaint)
            {
                // If the current focused control is the search textfield
                if (GUI.GetNameOfFocusedControl() == SEARCH_FIELD_NAME)
                {
                    // Clear the default value
                    if (searchFieldText == SEARCH_FIELD_DEFAULT)
                    {
                        isSearching = true;
                        searchFieldText = "";
                        RefreshSearchList();
                    }
                }
                else
                {
                    // If searchfield is not in focus and it is empty, restore default text
                    if (searchFieldText.Length < 1)
                    {
                        isSearching = false;
                        searchFieldText = SEARCH_FIELD_DEFAULT;
                    }
                }
            }

            GUILayout.EndHorizontal();
            #endregion

            hierarchyScroll = GUILayout.BeginScrollView(hierarchyScroll, false, true,
              GUILayout.Width(Elements.Settings.HierarchyPanelWidth));

            DoShowEntries(inspectorEntries);

            GUILayout.EndScrollView();

            GUILayout.EndVertical();
        }

        private bool AreAllCollapsed(IEnumerable<HierarchyEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (entry.IsExpanded)
                {
                    return false;
                }
                if (entry.HasChildren)
                {
                    if (!AreAllCollapsed(entry.Children))
                        return false;
                }
            }

            return true;
        }

        private void ExpandAll(IEnumerable<HierarchyEntry> entries)
        {
            foreach (var entry in entries)
            {
                entry.IsExpanded = true;
                if (entry.HasChildren)
                {
                    ExpandAll(entry.Children);
                }
            }
        }

        private void CollapseAll(IEnumerable<HierarchyEntry> entries)
        {
            foreach (var entry in entries)
            {
                entry.IsExpanded = false;
                if (entry.HasChildren)
                {
                    CollapseAll(entry.Children);
                }
            }
        }

        private void DoShowEntries(IEnumerable<HierarchyEntry> entries,
          int iterationDepth = 0)
        {
            foreach (var entry in entries)
            {
                if (entry.Transform == null)
                {
                    // The object has been deleted
                    continue;
                }
                if (isSearching && !searchFilteredEntries.Contains(entry))
                {
                    // The search filtered this object out
                    continue;
                }

                GUILayout.BeginHorizontal();
                Elements.Tools.Indent(iterationDepth);

                if (Elements.Tools.DoCollapseArrow(entry.IsExpanded && entry.HasChildren,
                  entry.HasChildren))
                {
                    entry.IsExpanded = !entry.IsExpanded;
                }

                if (GUILayout.Button(entry.Transform.name,
                  Elements.Buttons.LogEntryLabel))
                {
                    ObjectExplorer.Instance.InspectorPanel.SelectedGameObject
                      = entry.Transform.gameObject;
                }

                GUILayout.EndHorizontal();

                if (entry.IsExpanded)
                {
                    DoShowEntries(entry.Children, iterationDepth + 1);
                }
            }
        }

        public void RefreshGameObjectList()
        {
            var newEntries = new HashSet<HierarchyEntry>();
            foreach (var transform in FindObjectsOfType<Transform>())
            {
                if (transform.parent == null)
                {
                    var entry = new HierarchyEntry(transform);
                    newEntries.Add(entry);
                }
            }
            CopyIsExpanded(inspectorEntries, newEntries);
            inspectorEntries = newEntries;
            if (isSearching)
            {
                RefreshSearchList();
            }
        }

        private void CopyIsExpanded(HashSet<HierarchyEntry> src,
          HashSet<HierarchyEntry> dest)
        {
            foreach (var entry in src)
            {
                HierarchyEntry newEntry = null;
                if ((newEntry = dest.FirstOrDefault(e => e.Transform == entry.Transform))
                  != null)
                {
                    newEntry.IsExpanded = entry.IsExpanded;
                    CopyIsExpanded(entry.Children, newEntry.Children);
                }
            }
        }

        private void RefreshSearchList()
        {
            searchFilteredEntries.Clear();
            foreach (var entry in inspectorEntries)
            {
                searchFilteredEntries.UnionWith(Flatten(entry));
            }
            searchFilteredEntries.RemoveWhere(
              e => !EntryOrChildrenContain(e, searchFieldText));
        }

        private HashSet<HierarchyEntry> Flatten(HierarchyEntry root)
        {
            var flattened = new HashSet<HierarchyEntry>() { root };
            var children = root.Children;
            if (children != null)
            {
                foreach (var child in children)
                {
                    flattened.UnionWith(Flatten(child));
                }
            }
            return flattened;
        }

        private bool EntryOrChildrenContain(HierarchyEntry entry, string text)
        {
            string toSearch = text.ToLower();
            if (entry.Transform == null) return false;
            if (entry.Transform.name.ToLower().Contains(toSearch)) return true;
            foreach (var child in entry.Children)
            {
                if (EntryOrChildrenContain(child, text)) return true;
            }
            return false;
        }
    }
    [OnGameInit]
    public class InspectorPanel : MonoBehaviour
    {
        enum FieldType
        {
            Normal, VectorX, VectorY, VectorZ, ColorR, ColorG, ColorB, ColorA
        }

        private const string FIELD_EDIT_INPUT_NAME = "field_edit_input";

        private MemberValue activeMember;
        private FieldType activeMemberFieldType = FieldType.Normal;
        private object activeMemberNewValue;

        private bool layerTextInputActive;
        private string layerTextInputNewValue;

        private GameObject _selectedGameObject;
        // null indicates no object selected
        public GameObject SelectedGameObject
        {
            get
            {
                return _selectedGameObject;
            }
            set
            {
                _selectedGameObject = value;
                entries.Clear();

                if (value != null)
                {
                    foreach (var component in value.GetComponents<Component>())
                    {
                        entries.Add(new ComponentEntry(component));
                    }
                }
            }
        }

        public bool IsGameObjectSelected
        {
            get { return SelectedGameObject != null; }
        }

        private Dictionary<string, bool> filter = new Dictionary<string, bool>()
    {
      { "Instance", true },
      { "Static", false },
      { "Public", true },
      { "NonPublic", false },
      { "Inherited", false },
      { "Has Setter", true }, // TODO: reconsider default
    };

        private readonly HashSet<ComponentEntry> entries = new HashSet<ComponentEntry>();
        private Vector2 inspectorScroll = Vector2.zero;

        void OnGUI()
        {
            if (activeMember != null && Event.current.keyCode == KeyCode.Return)
            {
                object @object = activeMember.GetValue();

                if (@object is string || @object is bool)
                {
                    activeMember.SetValue(activeMemberNewValue);
                }
                else if (@object is int)
                {
                    int i;
                    if (activeMemberNewValue != null
                      && int.TryParse(activeMemberNewValue.ToString(), out i))
                    {
                        activeMember.SetValue(i);
                    }
                }
                else if (@object is Vector3)
                {
                    var vector3 = (Vector3)activeMember.GetValue();
                    float v;
                    if (activeMemberNewValue != null
                      && float.TryParse(activeMemberNewValue.ToString(), out v))
                    {
                        switch (activeMemberFieldType)
                        {
                            case FieldType.VectorX: vector3.x = v; break;
                            case FieldType.VectorY: vector3.y = v; break;
                            case FieldType.VectorZ: vector3.z = v; break;
                        }
                    }
                    activeMember.SetValue(vector3);
                }
                else if (@object is Color)
                {
                    var color = (Color)activeMember.GetValue();
                    float v;
                    if (activeMemberNewValue != null
                      && float.TryParse(activeMemberNewValue.ToString(), out v))
                    {
                        switch (activeMemberFieldType)
                        {
                            case FieldType.ColorR: color.r = v; break;
                            case FieldType.ColorG: color.g = v; break;
                            case FieldType.ColorB: color.b = v; break;
                            case FieldType.ColorA: color.a = v; break;
                        }
                    }
                    activeMember.SetValue(color);
                }
                else if (@object is Quaternion)
                {
                    var quat = (Quaternion)activeMember.GetValue();
                    float v;
                    if (activeMemberNewValue != null
                      && float.TryParse(activeMemberNewValue.ToString(), out v))
                    {
                        switch (activeMemberFieldType)
                        {
                            case FieldType.ColorR: quat.x = v; break;
                            case FieldType.ColorG: quat.y = v; break;
                            case FieldType.ColorB: quat.z = v; break;
                            case FieldType.ColorA: quat.w = v; break;
                        }
                    }
                    activeMember.SetValue(quat);
                }
                else if (@object is float)
                {
                    float f;
                    if (activeMemberNewValue != null
                      && float.TryParse(activeMemberNewValue.ToString(), out f))
                    {
                        activeMember.SetValue(f);
                    }
                }

                // Reset variables
                activeMember = null;
                activeMemberFieldType = FieldType.Normal;
                activeMemberNewValue = null;
            }

            if (layerTextInputActive && Event.current.keyCode == KeyCode.Return)
            {
                int i;
                if (int.TryParse(layerTextInputNewValue, out i))
                {
                    SelectedGameObject.layer = i;
                    layerTextInputActive = false;
                }
            }
        }

        private bool tagExpanded, layerExpanded;
        public void Display()
        {
            float panelWidth = Elements.Settings.InspectorPanelWidth;

            GUILayout.BeginHorizontal(GUILayout.Width(panelWidth),
              GUILayout.ExpandWidth(false));

            GUILayout.BeginVertical();

            if (IsGameObjectSelected)
            {
                // Text field to change game object's name
                SelectedGameObject.name = GUILayout.TextField(SelectedGameObject.name,
                  Elements.InputFields.ThinNoTopBotMargin, GUILayout.Width(panelWidth),
                  GUILayout.ExpandWidth(false));
            }
            else
            {
                GUILayout.TextField("Select a game object in the hierarchy",
                  Elements.InputFields.ThinNoTopBotMargin, GUILayout.Width(panelWidth),
                  GUILayout.ExpandWidth(false));
            }

            inspectorScroll = GUILayout.BeginScrollView(inspectorScroll,
              GUILayout.Width(panelWidth), GUILayout.ExpandWidth(false));

            if (IsGameObjectSelected)
            {
                GUILayout.BeginHorizontal();
                if (Elements.Tools.DoCollapseArrow(tagExpanded, false))
                    tagExpanded = !tagExpanded;
                GUILayout.Label("Tag: " + SelectedGameObject.tag,
                  Elements.Labels.LogEntry, GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (Elements.Tools.DoCollapseArrow(layerExpanded))
                    layerExpanded = !layerExpanded;
                GUILayout.Label("Layer: " + SelectedGameObject.layer,
                  Elements.Labels.LogEntry, GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();

                if (layerExpanded)
                {
                    if (layerTextInputActive)
                    {
                        layerTextInputNewValue = GUILayout.TextField(
                          layerTextInputNewValue, Elements.InputFields.ComponentField);
                    }
                    else
                    {
                        string newLayer = GUILayout.TextField(
                          SelectedGameObject.layer.ToString(),
                          Elements.InputFields.ComponentField);
                        if (newLayer != SelectedGameObject.layer.ToString())
                        {
                            layerTextInputActive = true;
                            layerTextInputNewValue = newLayer;
                        }
                    }
                }
            }

            GUILayout.Label("Components:", Elements.Labels.Title);

            foreach (var entry in new HashSet<ComponentEntry>(entries))
            {
                if (entry.Component == null)
                {
                    entries.Remove(entry);
                    continue;
                }
                DisplayComponent(entry);
            }

            GUILayout.EndScrollView();

            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            DisplayFilters();
            DisplayUtilityButtons();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private void DisplayFilters()
        {
            foreach (var pair in new Dictionary<string, bool>(filter))
            {
                var style = pair.Value ? Elements.Buttons.Default
                                       : Elements.Buttons.Disabled;
                if (GUILayout.Button(pair.Key, style))
                {
                    filter[pair.Key] = !filter[pair.Key];
                }
            }
        }

        private void DisplayUtilityButtons()
        {

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Destroy"))
            {
                Destroy(SelectedGameObject);
            }
            if (GUILayout.Button("Focus"))
            {
                var mo = MouseOrbit.Instance;
                if (mo != null && IsGameObjectSelected)
                    mo.target = SelectedGameObject.transform;
            }
            if (GUILayout.Button("Select focused"))
            {
                var mo = MouseOrbit.Instance;
                if (mo != null && mo.target != null)
                {
                    SelectedGameObject = mo.target.gameObject;
                }
            }
        }

        private void DisplayComponent(ComponentEntry entry)
        {
            GUILayout.BeginHorizontal();
            if (Elements.Tools.DoCollapseArrow(entry.IsExpanded))
            {
                entry.IsExpanded = !entry.IsExpanded;
            }
            GUILayout.TextField(entry.Component.GetType().Name,
              Elements.Labels.LogEntry);
            GUILayout.EndHorizontal();

            if (entry.IsExpanded)
            {
                ShowFields("Properties", entry.Properties);
                ShowFields("Fields", entry.Fields);
            }
        }

        private void ShowFields(string title, IEnumerable<MemberValue> fields)
        {
            GUILayout.BeginHorizontal();
            Elements.Tools.Indent();
            GUILayout.TextField(title, Elements.Labels.LogEntryTitle);
            GUILayout.EndHorizontal();

            bool hasDisplayedFields = false;
            foreach (var member in fields)
            {
                // Don't display obsolete members
                if (member.IsObsolete) continue;

                // Filters
                if (!filter["Static"] && member.IsStatic) continue;
                if (!filter["Instance"] && !member.IsStatic) continue;
                if (!filter["Public"] && member.IsPublic) continue;
                if (!filter["NonPublic"] && !member.IsPublic) continue;
                if (!filter["Inherited"] && member.IsInherited) continue;
                if (filter["Has Setter"] && !member.HasSetter) continue;

                hasDisplayedFields = true;

                GUILayout.BeginHorizontal();
                Elements.Tools.Indent();

                object value = member.GetValue();
                string name = member.Name;
                Type type = member.Type;

                GUILayout.BeginVertical();

                GUILayout.BeginHorizontal();

                bool canModifyType = IsSupported(value);
                if (Elements.Tools.DoCollapseArrow(member.IsExpanded, canModifyType))
                {
                    member.IsExpanded = !member.IsExpanded;
                }

                var typeStyle = new GUIStyle(Elements.Labels.LogEntry)
                {
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Elements.Colors.TypeText }
                };
                var valueStyle = new GUIStyle(Elements.Labels.LogEntry)
                {
                    normal = { textColor = Elements.Colors.DefaultText * .8f }
                };

                GUILayout.Label(type.Name, typeStyle, GUILayout.ExpandWidth(false));
                GUILayout.Label(" " + name + ":", Elements.Labels.LogEntry,
                  GUILayout.ExpandWidth(false));
                GUILayout.Label(" " + (value == null ? "null" : value.ToString()),
                  valueStyle, GUILayout.ExpandWidth(false));

                GUILayout.EndHorizontal();

                if (member.IsExpanded)
                {
                    GUILayout.BeginHorizontal();
                    Elements.Tools.Indent();

                    if (value is string)
                    {
                        DoInputField(member, value as string, FieldType.Normal);
                    }
                    else if (value is bool)
                    {
                        if (GUILayout.Button(value.ToString(),
                          Elements.Buttons.ComponentField, GUILayout.Width(60)))
                        {
                            member.SetValue(!(bool)value);
                        }
                    }
                    else if (value is Vector3)
                    {
                        var vec3Value = (Vector3)value;

                        // X
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("X: ", Elements.Labels.LogEntry,
                          GUILayout.ExpandWidth(false));
                        DoInputField(member, vec3Value.x, FieldType.VectorX, 80);
                        GUILayout.EndHorizontal();
                        // Y
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Y: ", Elements.Labels.LogEntry,
                          GUILayout.ExpandWidth(false));
                        DoInputField(member, vec3Value.y, FieldType.VectorY, 80);
                        GUILayout.EndHorizontal();
                        // Z
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Z: ", Elements.Labels.LogEntry,
                          GUILayout.ExpandWidth(false));
                        DoInputField(member, vec3Value.z, FieldType.VectorZ, 80);
                        GUILayout.EndHorizontal();
                    }
                    else if (value is Color)
                    {
                        var colorValue = (Color)value;

                        // R
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("R: ", Elements.Labels.LogEntry,
                          GUILayout.ExpandWidth(false));
                        DoInputField(member, colorValue.r, FieldType.ColorR, 80);
                        GUILayout.EndHorizontal();
                        // G
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("G: ", Elements.Labels.LogEntry,
                          GUILayout.ExpandWidth(false));
                        DoInputField(member, colorValue.g, FieldType.ColorG, 80);
                        GUILayout.EndHorizontal();
                        // B
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("B: ", Elements.Labels.LogEntry,
                          GUILayout.ExpandWidth(false));
                        DoInputField(member, colorValue.b, FieldType.ColorB, 80);
                        GUILayout.EndHorizontal();
                        // A
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("A: ", Elements.Labels.LogEntry,
                          GUILayout.ExpandWidth(false));
                        DoInputField(member, colorValue.a, FieldType.ColorA, 80);
                        GUILayout.EndHorizontal();
                    }
                    else if (value is Quaternion)
                    {
                        var quatValue = (Quaternion)value;

                        // X
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("X: ", Elements.Labels.LogEntry,
                          GUILayout.ExpandWidth(false));
                        DoInputField(member, quatValue.x, FieldType.ColorR, 80);
                        GUILayout.EndHorizontal();
                        // Y
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Y: ", Elements.Labels.LogEntry,
                          GUILayout.ExpandWidth(false));
                        DoInputField(member, quatValue.y, FieldType.ColorG, 80);
                        GUILayout.EndHorizontal();
                        // Z
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Z: ", Elements.Labels.LogEntry,
                          GUILayout.ExpandWidth(false));
                        DoInputField(member, quatValue.z, FieldType.ColorB, 80);
                        GUILayout.EndHorizontal();
                        // W
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("W: ", Elements.Labels.LogEntry,
                          GUILayout.ExpandWidth(false));
                        DoInputField(member, quatValue.w, FieldType.ColorA, 80);
                        GUILayout.EndHorizontal();
                    }
                    else if (value is int)
                    {
                        DoInputField(member, (int)value, FieldType.Normal);
                    }
                    else if (value is float)
                    {
                        DoInputField(member, (float)value, FieldType.Normal);
                    }
                    GUILayout.EndHorizontal();
                }

                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            // Display "None" if there were no fields displayed
            if (hasDisplayedFields) return;

            GUILayout.BeginHorizontal();
            Elements.Tools.Indent();
            GUILayout.Label("None");
            GUILayout.EndHorizontal();
        }

        private void DoInputField(MemberValue member, object value, FieldType type,
          float width = 0)
        {
            GUILayoutOption widthOption = width > 0 ? GUILayout.Width(width)
                                                    : GUILayout.ExpandWidth(true);

            // If this one is selected
            if (activeMember == member && activeMemberFieldType == type)
            {
                GUI.SetNextControlName(FIELD_EDIT_INPUT_NAME + member.ID);
                activeMemberNewValue = GUILayout.TextField((string)activeMemberNewValue,
                  Elements.InputFields.ComponentField, widthOption);
            }
            else
            {
                string oldValue = value.ToString();

                GUI.SetNextControlName(FIELD_EDIT_INPUT_NAME + member.ID);
                string newValue = GUILayout.TextField(oldValue.ToString(),
                  Elements.InputFields.ComponentField, widthOption);

                // Input was changed
                if (oldValue != newValue)
                {
                    // Set current member to the active one
                    activeMember = member;
                    activeMemberFieldType = type;
                    activeMemberNewValue = newValue;
                }
            }
        }

        public bool IsSupported(object value)
        {
            return value is string || value is bool || value is Vector3
                || value is int || value is float || value is Color
                || value is Quaternion;
        }
    }
    [OnGameInit]
    public class HierarchyEntry
    {
        public readonly Transform Transform;
        public HashSet<HierarchyEntry> Children;

        public bool HasChildren { get { return Children.Count > 0; } }
        public bool IsExpanded { get; set; }

        public HierarchyEntry(Transform transform)
        {
            Transform = transform;
            Children = new HashSet<HierarchyEntry>();

            UpdateChildrenList();
        }

        private void UpdateChildrenList()
        {
            Children.Clear();

            foreach (Transform child in Transform)
            {
                Children.Add(new HierarchyEntry(child));
            }
        }

        public override int GetHashCode()
        {
            return Transform.GetHashCode();
        }
    }
    [OnGameInit]
    public class ComponentEntry
    {
        public readonly Component Component;
        public bool IsExpanded;

        public List<MemberValue> Properties = new List<MemberValue>();
        public List<MemberValue> Fields = new List<MemberValue>();

        public ComponentEntry(Component component)
        {
            Component = component;
            IsExpanded = false;

            foreach (var property in component.GetType().GetProperties(
              BindingFlags.Instance | BindingFlags.Static |
              BindingFlags.Public | BindingFlags.NonPublic |
              BindingFlags.DeclaredOnly))
            {
                Properties.Add(new MemberValue(Component, property, false));
            }

            foreach (var property in component.GetType().BaseType.GetProperties(
              BindingFlags.Instance | BindingFlags.Static |
              BindingFlags.Public | BindingFlags.NonPublic))
            {
                Properties.Add(new MemberValue(Component, property, true));
            }

            foreach (var field in component.GetType().GetFields(
              BindingFlags.Instance | BindingFlags.Static |
              BindingFlags.Public | BindingFlags.NonPublic |
              BindingFlags.DeclaredOnly))
            {
                Fields.Add(new MemberValue(Component, field, false));
            }

            foreach (var field in component.GetType().BaseType.GetFields(
              BindingFlags.Instance | BindingFlags.Static |
              BindingFlags.Public | BindingFlags.NonPublic))
            {
                Fields.Add(new MemberValue(Component, field, true));
            }
        }
    }

    enum EntryType
    {
        Field,
        Property
    }
    [OnGameInit]
    public class MemberValue
    {
        private static int nextID;
        public Type Type { get { return entryType == EntryType.Field ? FieldInfo.FieldType : PropertyInfo.PropertyType; } }
        public bool IsObsolete { get { return Attribute.IsDefined(info, typeof(ObsoleteAttribute)); } }
        public string Name { get { return info.Name; } }
        public bool IsExpanded { get; set; }
        public object Dummy { get; set; }

        public bool IsPublic
        {
            get
            {
                if (entryType == EntryType.Field) return FieldInfo.IsPublic;
                return PropertyInfo.GetGetMethod(true).IsPublic;
            }
        }

        public bool IsStatic
        {
            get
            {
                if (entryType == EntryType.Field) return FieldInfo.IsStatic;
                return PropertyInfo.GetGetMethod(true).IsStatic;
            }
        }

        public bool IsInherited { get; private set; }

        public bool HasSetter
        {
            get
            {
                if (entryType == EntryType.Field) return true;
                return !ReferenceEquals(PropertyInfo.GetSetMethod(true), null);
            }
        }

        private FieldInfo FieldInfo { get { return info as FieldInfo; } }
        private PropertyInfo PropertyInfo { get { return info as PropertyInfo; } }

        public readonly int ID = nextID++;

        private readonly EntryType entryType;
        private readonly Component component;
        private readonly MemberInfo info;

        public MemberValue(Component component, PropertyInfo property, bool inherited)
          : this(component, EntryType.Property, property, inherited)
        { }

        public MemberValue(Component component, FieldInfo field, bool inherited)
          : this(component, EntryType.Field, field, inherited)
        { }

        private MemberValue(Component component, EntryType entryType,
          MemberInfo info, bool inherited)
        {
            this.component = component;
            this.entryType = entryType;
            this.info = info;

            Dummy = GetValue();
            IsInherited = inherited;
        }

        public void SetValue(object value)
        {
            Dummy = value;
            if (entryType == EntryType.Field)
            {
                FieldInfo.SetValue(component, value);
            }
            else
            {
                PropertyInfo.SetValue(component, value, null);
            }
        }

        public object GetValue()
        {
            return entryType == EntryType.Field ?
              FieldInfo.GetValue(component) :
              PropertyInfo.GetValue(component, null);
        }

    }
    [OnGameInit]
    public static class Util
    {
        private static int currentWindowID = int.MaxValue;

        /// <summary>
        /// Returns a window id that is guaranteed to not conflict with another id
        /// received from this method.
        /// Use this instead of declaring constant ids!
        /// </summary>
        /// <remarks>
        /// Do not use this method as an argument to GUI.Window directly!
        /// Call this method once and store and re-use the id!
        /// </remarks>
        /// <returns>The generated window id</returns>
        public static int GetWindowID()
        {
            return currentWindowID--;
        }

        internal static Rect PreventOffScreenWindow(Rect windowRect)
        {
            if (windowRect.x < (-windowRect.width + 50))
                windowRect.x = -windowRect.width + 50;

            if (windowRect.x > (Screen.width - 50))
                windowRect.x = Screen.width - 50;

            if (windowRect.y < 0)
                windowRect.y = 0;

            if (windowRect.y > (Screen.height - 50))
                windowRect.y = Screen.height - 50;

            return windowRect;
        }

    }
}