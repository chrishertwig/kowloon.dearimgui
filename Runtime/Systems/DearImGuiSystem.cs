using System;
using ImGuiNET;
using NumericsConverter;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kowloon.DearImGui
{
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    public partial class DearImGuiSystem : SystemBase
    {
        private DearImGuiRenderPass _DearImGuiRenderPass;
        private NativeHashMap<IntPtr, UnityObjectRef<Texture>> _Textures;

        protected override void OnCreate()
        {
            Enabled = false;
        }

        protected override void OnDestroy() { }

        protected override void OnStartRunning()
        {
            _Textures = new NativeHashMap<IntPtr, UnityObjectRef<Texture>>(1, Allocator.Persistent);

            ImGui.CreateContext();
            ImGuiIOPtr io = ImGui.GetIO();
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard | ImGuiConfigFlags.DockingEnable;

            io.Fonts.Flags |= ImFontAtlasFlags.NoBakedLines;
            io.Fonts.AddFontDefault();
            io.Fonts.Build();

            io.DisplayFramebufferScale = Vector2.one.ToSystem();

            Mesh mesh = DearImGuiUtils.CreateMesh();
            Shader shader = Shader.Find("Unlit/DearImGuiURP_hlsl");
            Material material = DearImGuiUtils.CreateMaterial(shader);
            Texture2D atlasTexture = DearImGuiUtils.CreateAtlasTexture();

            _DearImGuiRenderPass = new DearImGuiRenderPass
            {
                renderPassEvent = RenderPassEvent.AfterRendering + 20,
                Mesh = mesh,
                Material = material,
                DrawData = new DearImGuiRenderPass.ImGuiDraw[] { }
            };

            EntityManager.CreateSingleton(new DearImGuiReferences
            {
                Mesh = mesh,
                AtlasTexture = atlasTexture,
                Material = material
            });

            EntityManager.CreateSingleton<DearImGuiInput>();
            EntityManager.CreateSingletonBuffer<DearImGuiInputCharacter>();
            EntityManager.CreateSingletonBuffer<DearImGuiKeyEvent>();
            EntityManager.CreateSingleton(new DearImGuiFrameStarted { Value = false });

            RenderPipelineManager.beginCameraRendering += EnqueueRenderPass;
#if UNITY_EDITOR
            SceneView.duringSceneGui += OnSceneGui;
#endif
        }

        protected override void OnStopRunning()
        {
            RenderPipelineManager.beginCameraRendering -= EnqueueRenderPass;
#if UNITY_EDITOR
            SceneView.duringSceneGui -= OnSceneGui;
#endif

            if (SystemAPI.TryGetSingletonEntity<DearImGuiFrameStarted>(out Entity frameStartedEntity)) EntityManager.DestroyEntity(frameStartedEntity);
            if (SystemAPI.TryGetSingletonEntity<DearImGuiReferences>(out Entity referencesEntity)) EntityManager.DestroyEntity(referencesEntity);
            if (SystemAPI.TryGetSingletonEntity<DearImGuiInput>(out Entity inputEntity)) EntityManager.DestroyEntity(inputEntity);
            if (SystemAPI.TryGetSingletonEntity<DearImGuiInputCharacter>(out Entity inputCharacterEntity)) EntityManager.DestroyEntity(inputCharacterEntity);
            if (SystemAPI.TryGetSingletonEntity<DearImGuiKeyEvent>(out Entity keyEventEntity)) EntityManager.DestroyEntity(keyEventEntity);

            _Textures.Dispose();
        }

        protected override void OnUpdate()
        {
            // There seems to be edge cases where DearImGuiFrameStarted does not exist even though it gets created in OnStartRunning.
            // Seems to mostly happen when creating player builds.
            if (SystemAPI.TryGetSingleton(out DearImGuiFrameStarted frameStarted))
            {
                ImGuiIOPtr io = ImGui.GetIO();
                DearImGuiReferences references = SystemAPI.GetSingleton<DearImGuiReferences>();
                IntPtr atlasID = RegisterTexture(references.AtlasTexture);

                io.DeltaTime = SystemAPI.Time.DeltaTime;
#if UNITY_EDITOR
                if (EditorApplication.isPlaying || !SceneView.lastActiveSceneView)
#endif
                {
                    io.DisplaySize = new Vector2(Screen.width, Screen.height).ToSystem();
                }
#if UNITY_EDITOR
                else
                {
                    io.DisplaySize = SceneView.lastActiveSceneView.rootVisualElement.layout.size.ToSystem();
                }
#endif
                ImGui.NewFrame();
                io.Fonts.SetTexID(atlasID);

                frameStarted.Value = true;
                SystemAPI.SetSingleton(frameStarted);
            }
        }

        private void EnqueueRenderPass(ScriptableRenderContext context, Camera camera)
        {
#if UNITY_EDITOR
            if ((EditorApplication.isPlaying && camera.cameraType == CameraType.Game) ||
                (!EditorApplication.isPlaying && camera.cameraType == CameraType.SceneView))
#else
            if (camera.cameraType == CameraType.Game)
#endif
            {
                camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(_DearImGuiRenderPass);
            }
        }

        private static void ForcePlayerLoopUpdate()
        {
#if false // Disabled for now. Seems to slow down the editor.
            // Force viewport re-draw. For some reason we are otherwise locked to 30fps.
            SceneView.RepaintAll();
#endif

#if UNITY_EDITOR
            // Make sure we always run system updates even when we are interacting with the editor.
            EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        public void GenerateDrawData()
        {
            DearImGuiUtils.GenerateDrawData(ref _DearImGuiRenderPass.DrawData, TryGetTexture);
        }

        public IntPtr RegisterTexture(Texture texture)
        {
            IntPtr id = texture.GetNativeTexturePtr();
            _Textures[id] = texture;
            return id;
        }

        private bool TryGetTexture(IntPtr id, out Texture texture)
        {
            if (_Textures.TryGetValue(id, out UnityObjectRef<Texture> unityObjectRef))
            {
                texture = unityObjectRef.Value;
                return true;
            }

            texture = null;
            return false;
        }

#if UNITY_EDITOR
        private void OnSceneGui(SceneView sceneView)
        {
            Event currentEvent = Event.current;
            if (Enabled && currentEvent.type != EventType.Repaint)
            {
                ForcePlayerLoopUpdate();
            }

            ReadInput(currentEvent);
        }

        /// <summary>
        ///     We are reading input continuously every frame. During OnSceneGui we are only using the events when necessary to
        ///     stop them from propagating. Only exception is mouse position which we are setting here to get correct offset.
        /// </summary>
        private void ReadInput(Event evt)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            switch (evt.type)
            {
            case EventType.MouseMove or EventType.MouseDrag:
                if (SystemAPI.TryGetSingleton(out DearImGuiInput dearImGuiInput))
                {
                    dearImGuiInput.MousePos = evt.mousePosition;
                    SystemAPI.SetSingleton(dearImGuiInput);
                }

                if (io.WantCaptureMouse) evt.Use();
                break;

            case EventType.MouseDown:
                if (io.WantCaptureMouse) evt.Use();
                break;

            case EventType.MouseUp:
                if (io.WantCaptureMouse) evt.Use();
                break;

            case EventType.ScrollWheel:
                if (io.WantCaptureMouse) evt.Use();
                break;

            case EventType.KeyDown or EventType.KeyUp:
                if (io.WantCaptureKeyboard) evt.Use();
                break;
            }
        }
#endif
    }
}