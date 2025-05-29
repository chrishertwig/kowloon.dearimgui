using Unity.Entities;
using Unity.Mathematics;

namespace Kowloon.DearImGui
{
    public struct DearImGuiInput : IComponentData
    {
        public float2 MousePos;
        public float2 MouseWheel;
        public bool LeftMouseWasPressedThisFrame;
        public bool LeftMouseWasReleasedThisFrame;
        public bool RightMouseWasPressedThisFrame;
        public bool RightMouseWasReleasedThisFrame;
        public bool MiddleMouseWasPressedThisFrame;
        public bool MiddleMouseWasReleasedThisFrame;
    }
}