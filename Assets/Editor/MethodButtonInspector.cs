using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HuntingGame.Tools;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Call DrawButtons from an existing custom editor to retain its custom UI.</summary>
[CustomEditor(typeof(MonoBehaviour), true, isFallback = true), CanEditMultipleObjects]
public class MethodButtonInspector : Editor
{
    static readonly Dictionary<Type, MethodInfo[]> Cache = new Dictionary<Type, MethodInfo[]>();

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        DrawButtons(targets);
    }

    public static MethodInfo[] FindMethods(Type type)
    {
        if (Cache.TryGetValue(type, out var cached)) return cached;
        var methods = new List<MethodInfo>();
        var overrides = new HashSet<MethodInfo>();
        for (Type current = type; current != null && current != typeof(Object); current = current.BaseType)
            foreach (var method in current.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (Attribute.IsDefined(method, typeof(MethodButtonAttribute), true) && overrides.Add(method.GetBaseDefinition()))
                    methods.Add(method);
        return Cache[type] = methods.OrderBy(m => m.Name, StringComparer.Ordinal).ToArray();
    }

    public static bool Supported(MethodInfo method) => !method.IsStatic && !method.ContainsGenericParameters
        && method.ReturnType == typeof(void) && method.GetParameters().Length == 0
        && !Attribute.IsDefined(method, typeof(System.Runtime.CompilerServices.AsyncStateMachineAttribute));

    public static bool Allowed(MethodButtonMode mode, bool playing) => mode == MethodButtonMode.Both
        || (mode == MethodButtonMode.PlayMode ? playing : !playing);

    public static void DrawButtons(Object[] selected)
    {
        if (selected == null || selected.Length == 0 || selected[0] == null) return;
        var methods = FindMethods(selected[0].GetType());
        if (methods.Length == 0) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        foreach (var method in methods)
        {
            var attribute = (MethodButtonAttribute)Attribute.GetCustomAttribute(method, typeof(MethodButtonAttribute), true);
            string label = string.IsNullOrEmpty(attribute.Label) ? ObjectNames.NicifyVariableName(method.Name) : attribute.Label;
            if (!Supported(method))
            {
                EditorGUILayout.HelpBox(label + ": use a non-async, parameterless instance method returning void.", MessageType.Warning);
                continue;
            }
            string suffix = attribute.Mode == MethodButtonMode.Both ? "" : " (" + (attribute.Mode == MethodButtonMode.PlayMode ? "Play mode" : "Edit mode") + ")";
            bool compatible = selected.All(o => o != null && method.DeclaringType.IsInstanceOfType(o));
            using (new EditorGUI.DisabledScope(!compatible || EditorApplication.isCompiling
                || EditorApplication.isPlaying != EditorApplication.isPlayingOrWillChangePlaymode
                || !Allowed(attribute.Mode, Application.isPlaying)))
                if (GUILayout.Button(label + suffix))
                {
                    // Undo covers serialized state on the inspected targets. Methods creating
                    // or destroying other objects must use Unity's Undo API themselves.
                    if (!Application.isPlaying) Undo.RecordObjects(selected, label);
                    foreach (Object item in selected)
                    {
                        if (item == null) continue;
                        try { method.Invoke(item, null); }
                        catch (Exception error) { Debug.LogException(error is TargetInvocationException && error.InnerException != null ? error.InnerException : error, item); }
                        finally
                        {
                            if (!Application.isPlaying && item != null)
                            {
                                EditorUtility.SetDirty(item);
                                if (PrefabUtility.IsPartOfPrefabInstance(item)) PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                            }
                        }
                    }
                    GUI.changed = true;
                }
        }
    }
}

[CustomEditor(typeof(ScriptableObject), true, isFallback = true), CanEditMultipleObjects]
public class ScriptableMethodButtonInspector : MethodButtonInspector { }