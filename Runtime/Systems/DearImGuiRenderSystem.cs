using ImGuiNET;
using Unity.Entities;
using UnityEngine;

namespace Kowloon.DearImGui
{
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    public partial struct DearImGuiRenderSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DearImGuiInput>();
            state.RequireForUpdate<DearImGuiReferences>();
            state.RequireForUpdate<DearImGuiFrameStarted>();
        }

        public void OnUpdate(ref SystemState state)
        {
            DearImGuiFrameStarted frameStarted = SystemAPI.GetSingleton<DearImGuiFrameStarted>();
            if (!frameStarted.Value) return;

            DearImGuiReferences dearImGuiReferences = SystemAPI.GetSingleton<DearImGuiReferences>();
            ImGui.Render();
            Mesh mesh = dearImGuiReferences.Mesh;
            DearImGuiUtils.UpdateMesh(mesh);
            HandleInput();

            DearImGuiSystem imGuiSystem = state.World.GetOrCreateSystemManaged<DearImGuiSystem>();
            imGuiSystem.GenerateDrawData();

            Entity frameStartedEntity = SystemAPI.GetSingletonEntity<DearImGuiFrameStarted>();
            state.EntityManager.SetComponentData(frameStartedEntity, new DearImGuiFrameStarted { Value = false });
        }

        private void HandleInput()
        {
            ImGuiIOPtr io = ImGui.GetIO();
            DearImGuiInput input = SystemAPI.GetSingleton<DearImGuiInput>();
            io.AddMousePosEvent(input.MousePos.x, input.MousePos.y);
            io.AddMouseWheelEvent(input.MouseWheel.x, input.MouseWheel.y);
            if (input.LeftMouseWasPressedThisFrame) io.AddMouseButtonEvent(0, true);
            if (input.LeftMouseWasReleasedThisFrame) io.AddMouseButtonEvent(0, false);
            if (input.RightMouseWasPressedThisFrame) io.AddMouseButtonEvent(1, true);
            if (input.RightMouseWasReleasedThisFrame) io.AddMouseButtonEvent(1, false);
            if (input.MiddleMouseWasPressedThisFrame) io.AddMouseButtonEvent(2, true);
            if (input.MiddleMouseWasReleasedThisFrame) io.AddMouseButtonEvent(2, false);

            DynamicBuffer<DearImGuiInputCharacter> characterBuffer = SystemAPI.GetSingletonBuffer<DearImGuiInputCharacter>();
            foreach (DearImGuiInputCharacter character in characterBuffer)
            {
                io.AddInputCharacter(character.Value);
            }

            DynamicBuffer<DearImGuiKeyEvent> keyEventBuffer = SystemAPI.GetSingletonBuffer<DearImGuiKeyEvent>();
            foreach (DearImGuiKeyEvent keyEvent in keyEventBuffer)
            {
                io.AddKeyEvent(keyEvent.Key, keyEvent.Type == DearImGuiKeyEventType.Down);
            }

            characterBuffer.Clear();
            keyEventBuffer.Clear();
        }
    }
}