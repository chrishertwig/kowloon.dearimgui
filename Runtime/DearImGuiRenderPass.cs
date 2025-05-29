using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Kowloon.DearImGui
{
    public class DearImGuiRenderPass : ScriptableRenderPass
    {
        private static readonly int TextureNameID = Shader.PropertyToID("_Texture");
        private readonly MaterialPropertyBlock _MaterialPropertyBlock = new();
        public ImGuiDraw[] DrawData;
        public Material Material;
        public Mesh Mesh;

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameContext)
        {
            UniversalResourceData frameData = frameContext.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameContext.Get<UniversalCameraData>();

            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("DearImGui", out PassData passData);
            passData.Mesh = Mesh;
            passData.Material = Material;
            passData.MaterialProperties = _MaterialPropertyBlock;
            passData.PixelRect = cameraData.camera.pixelRect;
            passData.DrawData = DrawData;
            builder.SetRenderAttachment(frameData.activeColorTexture, 0);
            builder.SetRenderFunc((PassData data, RasterGraphContext renderGraphContext) => ExecutePass(data, renderGraphContext));
        }

        private static void ExecutePass(PassData data, RasterGraphContext rasterGraphContext)
        {
            RasterCommandBuffer cmd = rasterGraphContext.cmd;
            cmd.SetViewport(data.PixelRect);
            cmd.SetViewProjectionMatrices(
                Matrix4x4.Translate(new Vector3(0.5f / data.PixelRect.width, 0.5f / data.PixelRect.height, 0f)), // Small adjustment to improve text.
                Matrix4x4.Ortho(0f, data.PixelRect.width, data.PixelRect.height, 0f, 0f, 1f));

            for (int i = 0; i < data.DrawData.Length; i++)
            {
                if (data.DrawData[i].Texture)
                {
                    data.MaterialProperties.SetTexture(TextureNameID, data.DrawData[i].Texture);
                }
                cmd.EnableScissorRect(data.DrawData[i].ClipRect);
                cmd.DrawMesh(data.Mesh, Matrix4x4.identity, data.Material, data.DrawData[i].SubMeshIndex, -1, data.MaterialProperties);
            }

            cmd.DisableScissorRect();
        }

        private class PassData
        {
            public ImGuiDraw[] DrawData;
            public Material Material;
            public MaterialPropertyBlock MaterialProperties;
            public Mesh Mesh;
            public Rect PixelRect;
        }

        public struct ImGuiDraw
        {
            public Rect ClipRect;
            public Texture Texture;
            public int SubMeshIndex;
        }
    }
}