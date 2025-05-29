using Unity.Entities;
using UnityEngine;

namespace Kowloon.DearImGui
{
    public struct DearImGuiReferences : IComponentData
    {
        public UnityObjectRef<Mesh> Mesh;
        public UnityObjectRef<Texture2D> AtlasTexture;
        public UnityObjectRef<Material> Material;
    }
}