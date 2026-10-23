using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Sprites.ProjectSettings
{
    static class SpriteEditorWindowProjectSettings
    {
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            List<(SpriteEditorWindowProjectSettingsAttribute, VisualElement)> settingsList = new ();
            HashSet<string> allKeywords = new ();
            var methods = TypeCache.GetMethodsWithAttribute<SpriteEditorWindowProjectSettingsAttribute>();
            List<(MethodInfo, SpriteEditorWindowProjectSettingsAttribute)> methodInfos = new ();
            foreach(var method in methods)
            {
                if (typeof(VisualElement).IsAssignableFrom(method.ReturnType) && method.IsStatic && method.GetParameters().Length == 0)
                {
                    var attribute = (SpriteEditorWindowProjectSettingsAttribute)Attribute.GetCustomAttribute(method, typeof(SpriteEditorWindowProjectSettingsAttribute));
                    if (attribute != null)
                    {
                        methodInfos.Add((method, attribute));
                    }
                }
            }

            var provider = new SettingsProvider("Project/Sprite Editor", SettingsScope.Project, allKeywords)
            {
                activateHandler = (_, root) =>
                {
                    settingsList.Clear();
                    foreach (var (method, attribute) in methodInfos)
                    {
                        if(method.Invoke(null, null) is VisualElement element)
                        {
                            var settingsElement = new VisualElement();
                            var settingHeader = new Label(attribute.name);
                            settingHeader.AddToClassList("uitoolkit-settings-advanced-header");
                            settingsElement.Add(settingHeader);
                            settingsElement.Add(element);
                            settingsList.Add((attribute, settingsElement));
                            foreach (var keyword in attribute.searchKeyWords)
                            {
                                allKeywords.Add(keyword);
                            }
                            element.style.paddingBottom = 15;
                        }
                    }
                    settingsList.Sort((a, b) => string.Compare(a.Item1.name, b.Item1.name, StringComparison.Ordinal));

                    var styleSheet = EditorGUIUtility.Load("UIPackageResources/Settings/UIToolkitSettingsView.uss") as StyleSheet;
                    var scrollView = new ScrollView(ScrollViewMode.Vertical);
                    if(styleSheet == null)
                        Debug.Log("Failed to load stylesheet for Sprite Editor Window Project Settings.");
                    else
                        scrollView.styleSheets.Add(styleSheet);

                    scrollView.style.flexGrow = 1;
                    root.Add(scrollView);
                    var container = new VisualElement();
                    container.AddToClassList("uitoolkit-settings-container");
                    container.style.marginRight = container.style.marginLeft;
                    scrollView.Add(container);
                    var header = new Label("Sprite Editor");
                    header.AddToClassList("uitoolkit-settings-header");
                    container.Add(header);
                    foreach (var setting in settingsList)
                    {
                        container.Add(setting.Item2);
                    }
                }
            };
            return provider;
        }
    }
}
