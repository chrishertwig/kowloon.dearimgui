using System.Collections.Generic;
using System.Text.RegularExpressions;
using ImGuiNET;
using Unity.Entities;
using UnityEngine;

namespace Kowloon.DearImGui
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    public partial class DebugMenuSystem : SystemBase
    {
        public delegate void CmdDelegate();

        private readonly SubMenuItem _RootItem = new()
        {
            Name = "Root",
            Children = new List<IMenuItem>()
        };

        private readonly Dictionary<int, WindowItem> _WindowLookup = new();
        private int _WindowIndexCounter;

        private int _DearImGuiDemoWindowIndex;

        protected override void OnCreate()
        {
            RequireForUpdate<DearImGuiFrameStarted>();
            RegisterCommand("Game[-1000]>Exit[1000]", Application.Quit);
            _DearImGuiDemoWindowIndex = RegisterWindow("Game[-1000]>Dear Imgui Demo");
        }

        protected override void OnUpdate()
        {
            DearImGuiFrameStarted frameStarted = SystemAPI.GetSingleton<DearImGuiFrameStarted>();
            if (!frameStarted.Value) return;

            ImGui.BeginMainMenuBar();
            foreach (IMenuItem item in _RootItem.Children)
            {
                DrawMenuItem(item);
            }

            ImGui.EndMainMenuBar();

            WindowItem demoWindow = GetWindow(_DearImGuiDemoWindowIndex);
            if (demoWindow.IsVisible)
            {
                ImGui.ShowDemoWindow(ref demoWindow.IsVisible);
            }
        }

        private void DrawMenuItem(IMenuItem item)
        {
            switch (item)
            {
            case WindowItem windowItem:
            {
                if (ImGui.MenuItem(item.Name))
                {
                    windowItem.IsVisible = !windowItem.IsVisible;
                }

                break;
            }
            case CmdItem cmdItem:
            {
                if (ImGui.MenuItem(item.Name))
                {
                    cmdItem.Cmd.Invoke();
                }

                break;
            }
            case SubMenuItem menuItem:
            {
                if (ImGui.BeginMenu(item.Name))
                {
                    foreach (IMenuItem child in menuItem.Children)
                    {
                        DrawMenuItem(child);
                    }

                    ImGui.EndMenu();
                }

                break;
            }
            }
        }

        public void RegisterCommand(string menuPath, CmdDelegate cmd)
        {
            CreateMenuEntry(menuPath, cmd, out _);
        }

        public int RegisterWindow(string menuPath)
        {
            if (CreateMenuEntry(menuPath, null, out int windowIndex))
            {
                return windowIndex;
            }

            return -1;
        }

        public WindowItem GetWindow(int index)
        {
            // todo check for global debug menu visibility
            return _WindowLookup.GetValueOrDefault(index);
        }

        private bool CreateMenuEntry(string menuPath, CmdDelegate cmd, out int windowIndex)
        {
            windowIndex = -1;
            if (!menuPath.Contains(">"))
            {
                Debug.LogWarning("Tried to register debug menu entry with invalid menu hierarchy.");
                return false;
            }

            string[] menuPathParts = menuPath.Split(">");

            SubMenuItem currentItem = _RootItem;
            for (int index = 0; index < menuPathParts.Length; index++)
            {
                string entryString = menuPathParts[index];
                if (!ExtractNameAndPriority(entryString, out string name, out int priority, out bool explicitPriority))
                {
                    Debug.LogWarning($"Failed to extract name and sort priority from menu path. (Path: {menuPath})");
                    return false;
                }

                IMenuItem foundItem = currentItem.Children.Find(entry => entry.Name == name);

                if (index == menuPathParts.Length - 1)
                {
                    // Last entry needs to be command or window. Check if anything already exists.
                    if (foundItem != default)
                    {
                        Debug.LogWarning($"Tried to register a debug command or window but it already exists. (Path: {menuPath})");
                        return false;
                    }

                    if (cmd != null)
                    {
                        CmdItem newCmdItem = new()
                        {
                            Name = name,
                            SortPriority = priority,
                            Cmd = cmd,
                            ExplicitSortPriority = explicitPriority
                        };
                        currentItem.Children.Add(newCmdItem);
                    }
                    else
                    {
                        WindowItem newWindowItem = new()
                        {
                            Name = name,
                            SortPriority = priority,
                            IsVisible = false
                        };
                        currentItem.Children.Add(newWindowItem);
                        windowIndex = _WindowIndexCounter++;
                        _WindowLookup[windowIndex] = newWindowItem;
                    }
                }
                else
                {
                    if (foundItem == default)
                    {
                        // Could not find an entry, create one.
                        SubMenuItem newItem = new()
                        {
                            Name = name,
                            SortPriority = priority,
                            Children = new List<IMenuItem>()
                        };
                        currentItem.Children.Add(newItem);
                        currentItem = newItem;
                    }
                    else
                    {
                        // We have an existing entry. Check if it is not a command or window and continue.
                        if (foundItem is not SubMenuItem item)
                        {
                            Debug.LogWarning($"Tried to register a menu item ({name}) which is already a command or window. (Path: {menuPath})");
                            return false;
                        }

                        currentItem = item;

                        if (!currentItem.ExplicitSortPriority)
                        {
                            currentItem.SortPriority = priority;
                            currentItem.ExplicitSortPriority = explicitPriority;
                        }
                        else if (currentItem.SortPriority != priority)
                        {
                            Debug.LogWarning($"Menu '{name}' priority conflict: {priority} vs {currentItem.SortPriority} (Path: {menuPath})");
                        }
                    }
                }
            }

            SortMenuItems(_RootItem);
            return true;
        }

        private void SortMenuItems(SubMenuItem item)
        {
            item.Children.Sort((a, b) => a.SortPriority.CompareTo(b.SortPriority));
            foreach (IMenuItem child in item.Children)
            {
                if (child is SubMenuItem subMenuItem)
                {
                    SortMenuItems(subMenuItem);
                }
            }
        }

        private static bool ExtractNameAndPriority(string input, out string name, out int priority, out bool explicitPriority)
        {
            Match match = Regex.Match(input, @"^(.*?)(\[-?\d+\])?$");
            if (match.Success)
            {
                name = match.Groups[1].Value;
                priority = match.Groups[2].Success ? int.Parse(match.Groups[2].Value.Trim('[', ']')) : 0;
                explicitPriority = true;
                return true;
            }

            name = string.Empty;
            priority = 0;
            explicitPriority = false;
            return false;
        }

        public interface IMenuItem
        {
            string Name { get; }
            int SortPriority { get; }
            bool ExplicitSortPriority { get; }
        }

        public class CmdItem : IMenuItem
        {
            public string Name { get; set; }
            public int SortPriority { get; set; }
            public bool ExplicitSortPriority { get; set; }
            public CmdDelegate Cmd;
        }

        public class WindowItem : IMenuItem
        {
            public string Name { get; set; }
            public int SortPriority { get; set; }
            public bool ExplicitSortPriority { get; set; }
            public bool IsVisible;
        }

        public class SubMenuItem : IMenuItem
        {
            public string Name { get; set; }
            public int SortPriority { get; set; }
            public bool ExplicitSortPriority { get; set; }
            public List<IMenuItem> Children;
        }
    }
}