using System.Collections.Generic;
using UnityEngine;

namespace FastModdingLib.Utils
{
    /// <summary>
    /// 运行时 Shader 自动替换工具。
    /// Modder 在 Unity 编辑器中用官方 .shader 源文件设置模型材质，
    /// 但 AssetBundle 中的 Shader 引用与游戏运行时通过 GUID 绑定，
    /// 二者为不同资产实例——Bundle 加载后材质 Shader 会变粉色。
    /// 本工具在模型加载后自动将材质 Shader 替换为游戏已编译的对应 Shader。
    /// </summary>
    /// <remarks>
    /// 材质属性（_MainTex、_Tint、_Metallic 等）按属性名自动保留，无需手动迁移。
    /// 替换后自动清空 shaderKeywords，避免源 Shader 的关键字在目标 Shader 上无效。
    /// </remarks>
    public static class ShaderReplacer
    {
        /// <summary>
        /// Shader 名称映射表：源 shader.name → 目标 Shader.Find 的 key。
        /// 自动替换时，遍历 Renderer 的材质，若 shader.name 命中此表则执行替换。
        /// </summary>
        private static readonly Dictionary<string, string> ShaderMap = new Dictionary<string, string>()
        {
            { "SodaCraft/SodaLit",       "SodaCraft/SodaLit" },
            { "SodaCraft/SodaCharacter", "SodaCraft/SodaCharacter" },
        };

        /// <summary>
        /// 注册自定义 Shader 映射。用于未来新增 Shader 类型。
        /// </summary>
        /// <param name="sourceShaderName">源 Shader 名称（shader.name，编辑器中的值）</param>
        /// <param name="targetShaderName">目标 Shader 名称（Shader.Find 的 key）</param>
        public static void RegisterMapping(string sourceShaderName, string targetShaderName)
        {
            ShaderMap[sourceShaderName] = targetShaderName;
        }

        /// <summary>
        /// 检查指定的 shader.name 是否在映射表中。
        /// </summary>
        public static bool IsKnownShader(string shaderName)
        {
            return ShaderMap.ContainsKey(shaderName);
        }

        /// <summary>
        /// 对 GameObject 及其所有子物体（含 inactive）执行自动 Shader 替换。
        /// 遍历所有 Renderer 的 sharedMaterial，若 shader.name 命中映射表则替换。
        /// </summary>
        /// <param name="root">模型根 GameObject</param>
        /// <returns>替换成功的 Material 数量</returns>
        public static int ApplyTo(GameObject root)
        {
            if (root == null) return 0;

            int replaced = 0;
            var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);

            foreach (var renderer in renderers)
            {
                replaced += ReplaceMaterials(renderer.sharedMaterials, ShaderMap);
            }

            if (replaced > 0)
            {
                Debug.Log($"[ShaderReplacer] Replaced {replaced} material(s) on '{root.name}'.");
            }

            return replaced;
        }

        /// <summary>
        /// 对 GameObject 及其所有子物体强制使用指定 Shader（跳过映射表）。
        /// 用于 Modder 需要手动指定 Shader 的边缘情况。
        /// </summary>
        /// <param name="root">模型根 GameObject</param>
        /// <param name="targetShaderName">目标 Shader 名称（Shader.Find 的 key）</param>
        /// <returns>替换成功的 Material 数量</returns>
        public static int ApplyTo(GameObject root, string targetShaderName)
        {
            if (root == null || string.IsNullOrEmpty(targetShaderName)) return 0;

            var targetShader = Shader.Find(targetShaderName);
            if (targetShader == null)
            {
                Debug.LogWarning($"[ShaderReplacer] Target shader '{targetShaderName}' not found.");
                return 0;
            }

            int replaced = 0;
            var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);

            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var mat = materials[i];
                    if (mat == null) continue;

                    mat.shader = targetShader;
                    mat.shaderKeywords = null;
                    replaced++;
                }
            }

            if (replaced > 0)
            {
                Debug.Log($"[ShaderReplacer] Force-replaced {replaced} material(s) on '{root.name}' with '{targetShaderName}'.");
            }

            return replaced;
        }

        /// <summary>
        /// 按映射表替换一组 Material 的 Shader。
        /// </summary>
        private static int ReplaceMaterials(Material[] materials, Dictionary<string, string> map)
        {
            int count = 0;

            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                if (mat == null) continue;

                string sourceName = mat.shader.name;
                if (string.IsNullOrEmpty(sourceName)) continue;

                if (!map.TryGetValue(sourceName, out string targetName)) continue;

                var targetShader = Shader.Find(targetName);
                if (targetShader == null)
                {
                    Debug.LogWarning($"[ShaderReplacer] Shader '{targetName}' not found. Is the game fully loaded?");
                    continue;
                }

                mat.shader = targetShader;
                mat.shaderKeywords = null;
                count++;
            }

            return count;
        }
    }
}
