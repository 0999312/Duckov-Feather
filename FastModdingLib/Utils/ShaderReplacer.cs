using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FeatherMod.Utils
{
    /// <summary>
    /// 运行时 Shader 自动替换工具。
    ///
    /// 提供两个层级的替换：
    /// 1. ApplyToBundle(AssetBundle) — 替换 Bundle 中所有 Material 资产的 Shader（主要路径）。
    /// 2. ApplyTo(GameObject) — 遍历 GameObject 子级的所有 Renderer 替换（兜底）。
    ///
    /// 自动跳过已使用 Unity URP 内置 Shader 的 Material（如 "Universal Render Pipeline/Lit"）。
    /// </summary>
    public static class ShaderReplacer
    {
        private static readonly Dictionary<string, string> ShaderMap = new Dictionary<string, string>()
        {
            { "SodaCraft/SodaLit",       "SodaCraft/SodaLit" },
            { "SodaCraft/SodaCharacter", "SodaCraft/SodaCharacter" },
        };

        // Shader.Find 缓存
        private static readonly Dictionary<string, Shader> ShaderCache = new Dictionary<string, Shader>();

        // Unity URP 内置 Shader 前缀 — 直接跳过，不替换
        private const string URP_PREFIX = "Universal Render Pipeline/";

        /// <summary>注册自定义 Shader 映射。</summary>
        public static void RegisterMapping(string sourceShaderName, string targetShaderName)
        {
            ShaderMap[sourceShaderName] = targetShaderName;
            ShaderCache.Remove(targetShaderName);
        }

        /// <summary>检查 shader.name 是否在映射表中。</summary>
        public static bool IsKnownShader(string shaderName)
        {
            return ShaderMap.ContainsKey(shaderName);
        }

        // ═══════════════════════════════════════════════════════
        //  层级 1：AssetBundle 级别
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 对 AssetBundle 中所有 Material 资产执行 Shader 替换。
        /// LoadAllAssets&lt;Material&gt; 直接获取 Material，修改后所有
        /// 后续 LoadAsset 引用的 Renderer 自动获得正确 Shader。
        /// 自动跳过 Unity URP 内置 Shader。
        /// </summary>
        public static int ApplyToBundle(AssetBundle bundle)
        {
            if (bundle == null) return 0;

            var materials = bundle.LoadAllAssets<Material>();
            if (materials == null || materials.Length == 0) return 0;

            int replaced = 0;
            int skipped = 0;

            foreach (var mat in materials)
            {
                if (mat == null) continue;

                string sourceName = mat.shader.name;

                // 跳过 Unity URP 内置 Shader
                if (IsUrpBuiltin(sourceName))
                {
                    skipped++;
                    continue;
                }

                if (TryReplace(mat, sourceName))
                    replaced++;
            }

            if (replaced > 0 || skipped > 0)
            {
                Debug.Log($"[ShaderReplacer] Bundle '{bundle.name}': replaced {replaced}, skipped {skipped} URP built-in.");
            }

            return replaced;
        }

        // ═══════════════════════════════════════════════════════
        //  层级 2：GameObject 级别（兜底）
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 对 GameObject 及其子物体执行自动 Shader 替换。
        /// 遍历所有 Renderer.sharedMaterial。
        /// </summary>
        public static int ApplyTo(GameObject root)
        {
            if (root == null) return 0;

            var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers.Length == 0) return 0;

            int replaced = 0;
            int skipped = 0;
            var unmatched = new StringBuilder();

            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var mat = materials[i];
                    if (mat == null) continue;

                    string sourceName = mat.shader.name;
                    if (string.IsNullOrEmpty(sourceName)) continue;

                    if (IsUrpBuiltin(sourceName))
                    {
                        skipped++;
                        continue;
                    }

                    if (TryReplace(mat, sourceName))
                        replaced++;
                    else if (unmatched.Length < 500)
                        unmatched.AppendLine($"  [{renderer.name}] #{i}: '{sourceName}'");
                }
            }

            if (replaced > 0 || skipped > 0)
                Debug.Log($"[ShaderReplacer] '{root.name}': replaced {replaced}, skipped {skipped} URP built-in.");

            if (unmatched.Length > 0)
                Debug.Log($"[ShaderReplacer] '{root.name}': unmatched shader names:\n{unmatched}");

            return replaced;
        }

        /// <summary>
        /// 对 GameObject 强制使用指定 Shader（跳过映射表和 URP 检查）。
        /// </summary>
        public static int ApplyTo(GameObject root, string targetShaderName)
        {
            if (root == null || string.IsNullOrEmpty(targetShaderName)) return 0;

            var targetShader = FindShader(targetShaderName);
            if (targetShader == null) return 0;

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
                Debug.Log($"[ShaderReplacer] '{root.name}': force-replaced {replaced} → '{targetShaderName}'.");

            return replaced;
        }

        // ═══════════════════════════════════════════════════════
        //  内部
        // ═══════════════════════════════════════════════════════

        private static bool TryReplace(Material mat, string sourceName)
        {
            if (!ShaderMap.TryGetValue(sourceName, out string targetName)) return false;

            var targetShader = FindShader(targetName);
            if (targetShader == null) return false;

            mat.shader = targetShader;
            mat.shaderKeywords = null;
            return true;
        }

        private static Shader FindShader(string name)
        {
            if (!ShaderCache.TryGetValue(name, out var shader))
            {
                shader = Shader.Find(name);
                if (shader != null)
                    ShaderCache[name] = shader;
                else
                    Debug.LogWarning($"[ShaderReplacer] Shader.Find(\"{name}\") → null. Game not fully loaded?");
            }
            return shader;
        }

        private static bool IsUrpBuiltin(string shaderName)
        {
            return shaderName.StartsWith(URP_PREFIX);
        }
    }
}
