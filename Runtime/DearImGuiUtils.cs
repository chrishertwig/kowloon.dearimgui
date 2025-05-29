using System;
using System.Collections.Generic;
using ImGuiNET;
using NumericsConverter;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Kowloon.DearImGui
{
    public static class DearImGuiUtils
    {
        public delegate bool TryGetTexture(IntPtr texturePtr, out Texture texture);

        public static Texture2D CreateAtlasTexture()
        {
            ImGuiIOPtr io = ImGui.GetIO();
            ImFontAtlasPtr atlasPtr = io.Fonts;

            unsafe
            {
                atlasPtr.GetTexDataAsRGBA32(out byte* rawImGuiData, out int width, out int height, out int bytesPerPixel);

                Texture2D texture = new(width, height, TextureFormat.RGBA32, false, false)
                {
                    name = "DearImGui.AtlasTexture",
                    filterMode = FilterMode.Point
                };

                NativeArray<byte> sourceData = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(rawImGuiData, width * height * bytesPerPixel, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref sourceData, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
                NativeArray<byte> destinationData = texture.GetRawTextureData<byte>();
                int stride = width * bytesPerPixel;
                for (int y = 0; y < height; y++)
                {
                    NativeArray<byte>.Copy(sourceData, y * stride, destinationData, (height - y - 1) * stride, stride);
                }

                texture.Apply();
                texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
                return texture;
            }
        }

        public static IntPtr GetNativeTexturePtr(Texture texture)
        {
            IntPtr id = texture.GetNativeTexturePtr(); // todo potentially remove and add to readme of package?
            return id;
        }

        public static Mesh CreateMesh()
        {
            Mesh mesh = new()
            {
                name = "DearImGui.Mesh",
                indexFormat = IndexFormat.UInt16
            };
            mesh.MarkDynamic();
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }

        public static Material CreateMaterial(Shader shader)
        {
            Material material = new(shader)
            {
                name = "DearImGui.Material"
            };
            material.hideFlags = HideFlags.DontSave;
            return material;
        }
        
        // This function is adapted from UImGui - https://github.com/psydack/uimgui
        // Original code licensed under the MIT License.
        // imgui/Source/Renderer/RendererMesh.cs
        public static void UpdateMesh(Mesh mesh)
        {
            ImDrawDataPtr drawData = ImGui.GetDrawData();

            int subMeshCount = 0;
            for (int i = 0, cmdListsCountCached = drawData.CmdListsCount; i < cmdListsCountCached; i++)
            {
                subMeshCount += drawData.CmdLists[i].CmdBuffer.Size;
            }

            mesh.Clear(true);
            mesh.subMeshCount = subMeshCount;

            // Position, UV, Color
            VertexAttributeDescriptor[] vertexAttributes =
            {
                new(VertexAttribute.Position, VertexAttributeFormat.Float32, 2),
                new(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
                new(VertexAttribute.TexCoord1, VertexAttributeFormat.UInt32, 1)
            };

            mesh.SetVertexBufferParams(drawData.TotalVtxCount, vertexAttributes);
            mesh.SetIndexBufferParams(drawData.TotalIdxCount, IndexFormat.UInt16);

            const MeshUpdateFlags flags = MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds |
                                          MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontValidateIndices;

            int vertexBufferOffset = 0;
            int indexBufferOffset = 0;
            List<SubMeshDescriptor> subMeshDescriptors = new();
            for (int drawListIndex = 0, cmdListsCountCached = drawData.CmdListsCount; drawListIndex < cmdListsCountCached; drawListIndex++)
            {
                ImDrawListPtr drawList = drawData.CmdLists[drawListIndex];

                unsafe
                {
                    NativeArray<ImDrawVert> vertexData = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<ImDrawVert>((void*)drawList.VtxBuffer.Data, drawList.VtxBuffer.Size, Allocator.None);
                    NativeArray<ushort> indexData = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<ushort>((void*)drawList.IdxBuffer.Data, drawList.IdxBuffer.Size, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref vertexData, AtomicSafetyHandle.GetTempMemoryHandle());
                    NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref indexData, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
                    mesh.SetVertexBufferData(vertexData, 0, vertexBufferOffset, vertexData.Length, 0, flags);
                    mesh.SetIndexBufferData(indexData, 0, indexBufferOffset, indexData.Length, flags);

                    for (int commandIndex = 0, cmdBufferSizeCached = drawList.CmdBuffer.Size; commandIndex < cmdBufferSizeCached; commandIndex++)
                    {
                        ImDrawCmdPtr command = drawList.CmdBuffer[commandIndex];
                        SubMeshDescriptor subMeshDescriptor = new()
                        {
                            topology = MeshTopology.Triangles,
                            indexStart = indexBufferOffset + (int)command.IdxOffset,
                            indexCount = (int)command.ElemCount,
                            baseVertex = vertexBufferOffset + (int)command.VtxOffset
                        };
                        subMeshDescriptors.Add(subMeshDescriptor);
                    }

                    vertexBufferOffset += vertexData.Length;
                    indexBufferOffset += indexData.Length;
                }
            }

            mesh.SetSubMeshes(subMeshDescriptors, flags);
            mesh.UploadMeshData(false);
        }

        private static bool IsOutsideView(Vector4 drawRect, Vector2 viewSize)
        {
            return drawRect.x >= viewSize.x || drawRect.y >= viewSize.y || drawRect.z < 0f || drawRect.w < 0f;
        }

        public static void GenerateDrawData(ref DearImGuiRenderPass.ImGuiDraw[] renderPassDrawData, TryGetTexture tryGetTexture)
        {
            ImDrawDataPtr drawData = ImGui.GetDrawData();
            unsafe
            {
                if (drawData.NativePtr == null) return;
            }

            Vector2 frameBufferSize = drawData.DisplaySize.ToUnity(); // todo we might have to change this

            Vector4 clipOffset = new(drawData.DisplayPos.X, drawData.DisplayPos.Y, drawData.DisplayPos.X, drawData.DisplayPos.Y);
            Vector4 clipScale = new(drawData.FramebufferScale.X, drawData.FramebufferScale.Y, drawData.FramebufferScale.X, drawData.FramebufferScale.Y);

            int drawCount = 0;
            for (int i = 0; i < drawData.CmdListsCount; i++)
            {
                drawCount += drawData.CmdLists[i].CmdBuffer.Size;
            }

            renderPassDrawData = new DearImGuiRenderPass.ImGuiDraw[drawCount];

            int subMeshIndex = 0;
            for (int commandListIndex = 0, cmdListsCountCached = drawData.CmdListsCount; commandListIndex < cmdListsCountCached; commandListIndex++)
            {
                ImDrawListPtr drawList = drawData.CmdLists[commandListIndex];
                for (int commandIndex = 0, cmdBufferSizeCached = drawList.CmdBuffer.Size; commandIndex < cmdBufferSizeCached; commandIndex++, subMeshIndex++)
                {
                    ImDrawCmdPtr drawCommand = drawList.CmdBuffer[commandIndex];

                    // Project scissor rectangle into framebuffer space and skip if fully outside.
                    Vector4 clipSize = drawCommand.ClipRect.ToUnity() - clipOffset;
                    Vector4 clip = Vector4.Scale(clipSize, clipScale);

                    if (IsOutsideView(clip, frameBufferSize)) continue;

                    tryGetTexture(drawCommand.TextureId, out Texture texture);
                    renderPassDrawData[subMeshIndex] = new DearImGuiRenderPass.ImGuiDraw
                    {
                        ClipRect = new Rect(clip.x, frameBufferSize.y - clip.w, clip.z - clip.x, clip.w - clip.y),
                        Texture = texture,
                        SubMeshIndex = subMeshIndex
                    };
                }
            }
        }

        public static bool TryMapKey(Key key, out ImGuiKey result)
        {
            result = key switch
            {
                >= Key.F1 and <= Key.F12 => KeyToImGuiKeyShortcut(key, Key.F1, ImGuiKey.F1),
                >= Key.Numpad0 and <= Key.Numpad9 => KeyToImGuiKeyShortcut(key, Key.Numpad0, ImGuiKey.Keypad0),
                >= Key.A and <= Key.Z => KeyToImGuiKeyShortcut(key, Key.A, ImGuiKey.A),
                >= Key.Digit1 and <= Key.Digit0 => KeyToImGuiKeyShortcut(key, Key.Digit0, ImGuiKey._0),
                Key.LeftShift or Key.RightShift => ImGuiKey.ModShift,
                Key.LeftCtrl or Key.RightCtrl => ImGuiKey.ModCtrl,
                Key.LeftAlt or Key.RightAlt => ImGuiKey.ModAlt,
                Key.ContextMenu => ImGuiKey.Menu,
                Key.UpArrow => ImGuiKey.UpArrow,
                Key.DownArrow => ImGuiKey.DownArrow,
                Key.LeftArrow => ImGuiKey.LeftArrow,
                Key.RightArrow => ImGuiKey.RightArrow,
                Key.Enter => ImGuiKey.Enter,
                Key.Escape => ImGuiKey.Escape,
                Key.Space => ImGuiKey.Space,
                Key.Tab => ImGuiKey.Tab,
                Key.Backspace => ImGuiKey.Backspace,
                Key.Insert => ImGuiKey.Insert,
                Key.Delete => ImGuiKey.Delete,
                Key.PageUp => ImGuiKey.PageUp,
                Key.PageDown => ImGuiKey.PageDown,
                Key.Home => ImGuiKey.Home,
                Key.End => ImGuiKey.End,
                Key.CapsLock => ImGuiKey.CapsLock,
                Key.ScrollLock => ImGuiKey.ScrollLock,
                Key.PrintScreen => ImGuiKey.PrintScreen,
                Key.Pause => ImGuiKey.Pause,
                Key.NumLock => ImGuiKey.NumLock,
                Key.NumpadDivide => ImGuiKey.KeypadDivide,
                Key.NumpadMultiply => ImGuiKey.KeypadMultiply,
                Key.NumpadMinus => ImGuiKey.KeypadSubtract,
                Key.NumpadPlus => ImGuiKey.KeypadAdd,
                Key.NumpadPeriod => ImGuiKey.KeypadDecimal,
                Key.NumpadEnter => ImGuiKey.KeypadEnter,
                Key.Backquote => ImGuiKey.GraveAccent,
                Key.Minus => ImGuiKey.Minus,
                Key.Equals => ImGuiKey.Equal,
                Key.LeftBracket => ImGuiKey.LeftBracket,
                Key.RightBracket => ImGuiKey.RightBracket,
                Key.Semicolon => ImGuiKey.Semicolon,
                Key.Quote => ImGuiKey.Apostrophe,
                Key.Comma => ImGuiKey.Comma,
                Key.Period => ImGuiKey.Period,
                Key.Slash => ImGuiKey.Slash,
                Key.Backslash => ImGuiKey.Backslash,
                _ => ImGuiKey.None
            };

            return result != ImGuiKey.None;

            ImGuiKey KeyToImGuiKeyShortcut(Key keyToConvert, Key startKey1, ImGuiKey startKey2)
            {
                int changeFromStart1 = (int)keyToConvert - (int)startKey1;
                return startKey2 + changeFromStart1;
            }
        }
    }
}