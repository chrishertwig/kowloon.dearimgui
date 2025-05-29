using ImGuiNET;
using Unity.Entities;

namespace Kowloon.DearImGui
{
    public enum DearImGuiKeyEventType
    {
        Down,
        Released
    }

    public struct DearImGuiKeyEvent : IBufferElementData
    {
        public ImGuiKey Key;
        public DearImGuiKeyEventType Type;
    }
}