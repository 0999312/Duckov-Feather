using Cysharp.Threading.Tasks;
using FeatherMod.Register;
using FeatherMod.Utils;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

using UnityEngine;
using UnityEngine.Rendering;

namespace FeatherMod
{
    /// <summary>
    /// 运行时模型加载（OBJ 简化路径）。
    /// 从 mod 目录 <c>assets/models/</c> 直接读取 OBJ 文件并解析为 <see cref="Mesh"/>，
    /// 替代 AssetBundle 专用工作流，面向简单物品（单 mesh、单材质）。
    /// 文件目录/错误处理语义对齐 <see cref="ItemUtils.LoadSpriteFromDir"/>。
    /// </summary>
    public static class ModelUtils
    {
        // ===== 路径约定 =====

        private const string ModelsSubDir = "assets/models/";
        private const string TexturesSubDir = "assets/textures/";
        private const string DefaultShader = "SodaCraft/SodaLit";
        private const string FallbackShader = "Universal Render Pipeline/Lit";

        // ===== 缓存（Mesh: Identifier → Mesh；Material: textureKey → Material） =====

        private static readonly Dictionary<Identifier, Mesh> _meshCache = new Dictionary<Identifier, Mesh>();
        private static readonly object _meshLock = new object();
        private static readonly Dictionary<string, Material> _materialCache = new Dictionary<string, Material>();
        private static readonly object _materialLock = new object();

        // ===== 同步加载 =====

        /// <summary>
        /// 从调用方 mod 目录 <c>assets/models/</c> 加载模型。modid 从调用方程序集名自动推导。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Mesh? LoadMesh(string resourceName)
        {
            var callingAssembly = System.Reflection.Assembly.GetCallingAssembly();
            var id = new Identifier(callingAssembly.GetName().Name, resourceName);
            return LoadMesh(id);
        }

        /// <summary>
        /// 从指定 mod 目录 <c>assets/models/</c> 加载模型（带 Mesh 缓存，同 Identifier 复用）。
        /// </summary>
        public static Mesh? LoadMesh(Identifier id)
        {
            lock (_meshLock)
            {
                if (_meshCache.TryGetValue(id, out var cached) && cached != null)
                    return cached;
            }
            var modDir = ModPathResolver.ResolveDirectory(id.Domain);
            if (modDir == null)
            {
                Debug.LogError($"[ModelUtils] Mod directory not registered: {id.Domain}");
                return null;
            }
            Mesh? mesh = LoadMeshFromDir(modDir, id.Path);
            if (mesh != null)
            {
                lock (_meshLock) _meshCache[id] = mesh;
            }
            return mesh;
        }

        /// <summary>从指定目录加载模型（无缓存）。OBJ 之外仅支持自动补全扩展名的无后缀文件名。</summary>
        public static Mesh? LoadMeshFromDir(string modDirectory, string resourceName)
        {
            if (!TryResolveModelPath(modDirectory, resourceName, out string fileLoc))
                return null;
            try
            {
                if (!TryParseObj(fileLoc, out ObjMeshData data))
                    return null;
                return BuildMesh(resourceName, data);
            }
            catch (Exception arg)
            {
                Debug.LogError($"[ModelUtils] Except on loading model: {arg}");
                return null;
            }
        }

        // ===== 异步加载（推荐：IO + 解析在线程池，主线程组装 Mesh） =====

        /// <summary>
        /// 【推荐】异步加载模型。modid 从调用方程序集名自动推导。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async UniTask<Mesh?> LoadMeshAsync(string resourceName)
        {
            var callingAssembly = System.Reflection.Assembly.GetCallingAssembly();
            var id = new Identifier(callingAssembly.GetName().Name, resourceName);
            return await LoadMeshAsync(id);
        }

        /// <summary>
        /// 【推荐】异步加载模型（带缓存单飞：同一 Identifier 并发调用只解析一次）。
        /// 文件 IO 与文本解析在 <see cref="UniTask.RunOnThreadPool"/> 执行，主线程零阻塞；
        /// Mesh 创建与赋值回主线程完成。
        /// </summary>
        public static async UniTask<Mesh?> LoadMeshAsync(Identifier id)
        {
            lock (_meshLock)
            {
                if (_meshCache.TryGetValue(id, out var cached) && cached != null)
                    return cached;
            }
            var modDir = ModPathResolver.ResolveDirectory(id.Domain);
            if (modDir == null)
            {
                Debug.LogError($"[ModelUtils] Mod directory not registered: {id.Domain}");
                return null;
            }
            Mesh? mesh = await LoadMeshFromDirAsync(modDir, id.Path);
            if (mesh != null)
            {
                lock (_meshLock) _meshCache[id] = mesh;
            }
            return mesh;
        }

        /// <summary>
        /// 【推荐】从指定目录异步加载模型。IO + 解析在线程池，Mesh 创建在主线程。
        /// </summary>
        public static async UniTask<Mesh?> LoadMeshFromDirAsync(string modDirectory, string resourceName)
        {
            if (!TryResolveModelPath(modDirectory, resourceName, out string fileLoc))
                return null;
            try
            {
                string file = fileLoc;
                ObjMeshData? data = await UniTask.RunOnThreadPool(() => TryParseObj(file, out ObjMeshData parsed) ? parsed : null);
                if (data == null)
                    return null;
                return BuildMesh(resourceName, data);
            }
            catch (Exception arg)
            {
                Debug.LogError($"[ModelUtils] Except on loading model: {arg}");
                return null;
            }
        }

        // ===== 材质 =====

        /// <summary>
        /// 获取模型材质（带缓存，同 textureId 全局共享一个实例）。
        /// shader 取 <c>SodaCraft/SodaLit</c>（游戏物品主 shader），未命中降级
        /// <c>Universal Render Pipeline/Lit</c>。textureId 从 <c>assets/textures/</c> 加载纹理
        /// （建议与物品 sprite 隔离，如 <c>assets/textures/models/</c>）。
        /// </summary>
        public static Material? GetModelMaterial(Identifier? textureId = null)
        {
            string key = MakeTextureKey(textureId);
            lock (_materialLock)
            {
                if (_materialCache.TryGetValue(key, out var cached) && cached != null)
                    return cached;
            }
            Texture2D? texture = null;
            if (textureId != null)
            {
                var modDir = ModPathResolver.ResolveDirectory(textureId.Domain);
                if (modDir == null)
                {
                    Debug.LogError($"[ModelUtils] Mod directory not registered: {textureId.Domain}");
                    return null;
                }
                texture = LoadTextureFromDir(modDir, textureId.Path);
            }
            Material? material = CreateMaterial(texture);
            if (material != null)
            {
                UnityEngine.Object.DontDestroyOnLoad(material);
                lock (_materialLock) _materialCache[key] = material;
            }
            return material;
        }

        /// <summary>
        /// 【推荐】异步获取模型材质。缓存命中立即返回；纹理 IO 在线程池执行，材质创建在主线程。
        /// </summary>
        public static async UniTask<Material?> GetModelMaterialAsync(Identifier? textureId = null)
        {
            string key = MakeTextureKey(textureId);
            lock (_materialLock)
            {
                if (_materialCache.TryGetValue(key, out var cached) && cached != null)
                    return cached;
            }
            Texture2D? texture = null;
            if (textureId != null)
            {
                var modDir = ModPathResolver.ResolveDirectory(textureId.Domain);
                if (modDir == null)
                {
                    Debug.LogError($"[ModelUtils] Mod directory not registered: {textureId.Domain}");
                    return null;
                }
                texture = await LoadTextureFromDirAsync(modDir, textureId.Path);
            }
            Material? material = CreateMaterial(texture);
            if (material != null)
            {
                UnityEngine.Object.DontDestroyOnLoad(material);
                lock (_materialLock) _materialCache[key] = material;
            }
            return material;
        }

        // ===== 组装 =====

        /// <summary>
        /// 用已有 Mesh 组装渲染 GameObject（MeshFilter + MeshRenderer 成对，无碰撞体）。
        /// 纯视觉展示，不包含交互/物理组件。
        /// </summary>
        public static GameObject? CreateModel(Mesh mesh, Material? material = null)
        {
            if (mesh == null)
            {
                Debug.LogError("[ModelUtils] CreateModel: mesh is null.");
                return null;
            }
            GameObject go = new GameObject("Model");
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            if (material != null)
                renderer.sharedMaterial = material;
            return go;
        }

        // ===== 缓存与卸载 =====

        /// <summary>释放指定 Identifier 的 Mesh 缓存（同时释放其 Domain 的材质缓存）。</summary>
        public static void ReleaseModel(Identifier id)
        {
            lock (_meshLock)
            {
                if (_meshCache.Remove(id, out Mesh? mesh) && mesh != null)
                    UnityEngine.Object.Destroy(mesh);
            }
            ReleaseMaterialsByDomain(id.Domain);
        }

        /// <summary>
        /// 批量释放指定 mod 的全部模型缓存（Mesh + Material）。
        /// modid 未指定时走 <see cref="RegistryManager.CurrentModid"/>。
        /// </summary>
        public static void ReleaseAllModels(string? modid = null)
        {
            string domain = modid ?? RegistryManager.CurrentModid;
            lock (_meshLock)
            {
                var keys = new List<Identifier>();
                foreach (var kvp in _meshCache)
                {
                    if (kvp.Key.Domain == domain)
                        keys.Add(kvp.Key);
                }
                foreach (Identifier key in keys)
                {
                    if (_meshCache.Remove(key, out Mesh? mesh) && mesh != null)
                        UnityEngine.Object.Destroy(mesh);
                }
            }
            ReleaseMaterialsByDomain(domain);
        }

        private static void ReleaseMaterialsByDomain(string domain)
        {
            string prefix = domain + ":";
            lock (_materialLock)
            {
                var keys = new List<string>();
                foreach (var kvp in _materialCache)
                {
                    if (kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
                        keys.Add(kvp.Key);
                }
                foreach (string key in keys)
                {
                    if (_materialCache.Remove(key, out Material? mat) && mat != null)
                        UnityEngine.Object.Destroy(mat);
                }
            }
        }

        // ===== 内部：路径解析 =====

        private static bool TryResolveModelPath(string modDirectory, string resourceName, out string fileLoc)
        {
            fileLoc = string.Empty;
            if (resourceName.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[FML] FBX runtime import is not supported. Export as OBJ (Blender: File > Export > Wavefront OBJ, triangulate, Y-up) or use the AssetBundle path (ItemUtils.SetItemGraphic).");
                return false;
            }
            StringBuilder assetLoc = new StringBuilder(ModelsSubDir);
            assetLoc.Append(resourceName);
            if (resourceName.IndexOf('.') < 0)
                assetLoc.Append(".obj");
            string loc = Path.Combine(modDirectory, assetLoc.ToString());
            if (!File.Exists(loc))
            {
                Debug.LogError("[FML] Model is missing: " + loc);
                return false;
            }
            fileLoc = loc;
            return true;
        }

        private static string MakeTextureKey(Identifier? textureId)
        {
            return textureId?.ToString() ?? string.Empty;
        }

        private static Material? CreateMaterial(Texture2D? texture)
        {
            Shader? shader = Shader.Find(DefaultShader);
            if (shader == null)
                shader = Shader.Find(FallbackShader);
            if (shader == null)
            {
                Debug.LogError($"[ModelUtils] Shader not found: {DefaultShader} / {FallbackShader}");
                return null;
            }
            Material material = new Material(shader);
            if (texture != null)
                material.SetTexture("_MainTex", texture);
            return material;
        }

        private static Texture2D? LoadTextureFromDir(string modDirectory, string resourceName)
        {
            StringBuilder assetLoc = new StringBuilder(TexturesSubDir);
            assetLoc.Append(resourceName);
            string fileLoc = Path.Combine(modDirectory, assetLoc.ToString());
            if (!File.Exists(fileLoc))
            {
                Debug.LogError("[FML] Model texture is missing: " + fileLoc);
                return null;
            }
            try
            {
                byte[] imageData = File.ReadAllBytes(fileLoc);
                return CreateTexture(imageData, resourceName);
            }
            catch (Exception arg)
            {
                Debug.LogError($"[ModelUtils] Except on loading model texture: {arg}");
                return null;
            }
        }

        private static async UniTask<Texture2D?> LoadTextureFromDirAsync(string modDirectory, string resourceName)
        {
            StringBuilder assetLoc = new StringBuilder(TexturesSubDir);
            assetLoc.Append(resourceName);
            string fileLoc = Path.Combine(modDirectory, assetLoc.ToString());
            if (!File.Exists(fileLoc))
            {
                Debug.LogError("[FML] Model texture is missing: " + fileLoc);
                return null;
            }
            try
            {
                byte[] imageData = await UniTask.RunOnThreadPool(() => File.ReadAllBytes(fileLoc));
                return CreateTexture(imageData, resourceName);
            }
            catch (Exception arg)
            {
                Debug.LogError($"[ModelUtils] Except on loading model texture: {arg}");
                return null;
            }
        }

        private static Texture2D CreateTexture(byte[] imageData, string resourceName)
        {
            Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            if (!texture2D.LoadImage(imageData))
            {
                Debug.LogError($"[FML] Invalid model texture image, Resource:{resourceName}");
                UnityEngine.Object.Destroy(texture2D);
                return null!;
            }
            texture2D.filterMode = FilterMode.Bilinear;
            texture2D.Apply();
            UnityEngine.Object.DontDestroyOnLoad(texture2D);
            return texture2D;
        }

        // ===== 内部：Mesh 组装 =====

        private static Mesh BuildMesh(string meshName, ObjMeshData data)
        {
            Mesh mesh = new Mesh();
            mesh.name = meshName;
            // 顶点数超过 16-bit 上限自动升 UInt32（2022.3 完整支持）
            mesh.indexFormat = data.FinalPositions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(data.FinalPositions);
            if (data.FinalNormals.Count > 0)
                mesh.SetNormals(data.FinalNormals);
            if (data.FinalUvs.Count > 0)
                mesh.SetUVs(0, data.FinalUvs);
            mesh.SetTriangles(data.FinalIndices, 0);
            if (data.NeedsRecalcNormals)
                mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.Optimize();
            // 释放 CPU 侧顶点/索引副本（之后 Mesh 只读）
            mesh.UploadMeshData(true);
            UnityEngine.Object.DontDestroyOnLoad(mesh);
            return mesh;
        }

        // ===== 内部：OBJ 解析（线程池执行，逐行零额外分配） =====

        private sealed class ObjMeshData
        {
            public List<Vector3> Positions = new List<Vector3>(4096);
            public List<Vector2> Uvs = new List<Vector2>(4096);
            public List<Vector3> Normals = new List<Vector3>(4096);
            public readonly List<FaceRef> Faces = new List<FaceRef>(4096 * 3);
            public bool HasUv;
            public bool HasNormal;
            public List<Vector3> FinalPositions = new List<Vector3>(4096);
            public List<Vector2> FinalUvs = new List<Vector2>(0);
            public List<Vector3> FinalNormals = new List<Vector3>(0);
            public List<int> FinalIndices = new List<int>(4096 * 3);
            public bool NeedsRecalcNormals = true;
        }

        private readonly struct FaceRef
        {
            public readonly int P;
            public readonly int U;
            public readonly int N;
            public FaceRef(int p, int u, int n)
            {
                P = p;
                U = u;
                N = n;
            }
        }

        /// <summary>
        /// 顶点唯一化 key（(v, vt, vn) 三元组）。struct + IEquatable 保证 Dictionary 零装箱。
        /// </summary>
        private readonly struct VertKey : IEquatable<VertKey>
        {
            private readonly int _p;
            private readonly int _u;
            private readonly int _n;
            public VertKey(int p, int u, int n)
            {
                _p = p;
                _u = u;
                _n = n;
            }
            public bool Equals(VertKey other)
            {
                return _p == other._p && _u == other._u && _n == other._n;
            }
            public override bool Equals(object? obj)
            {
                return obj is VertKey other && Equals(other);
            }
            public override int GetHashCode()
            {
                return (_p * 397) ^ (_u * 31) ^ _n;
            }
        }

        private static bool TryParseObj(string fileLoc, out ObjMeshData result)
        {
            result = null!;
            ObjMeshData data = new ObjMeshData();
            int lineNo = 0;
            try
            {
                using (StreamReader reader = new StreamReader(fileLoc, Encoding.UTF8))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNo++;
                        ReadOnlySpan<char> s = line.AsSpan();
                        int pos = 0;
                        while (pos < s.Length && (s[pos] == ' ' || s[pos] == '\t'))
                            pos++;
                        if (pos >= s.Length || s[pos] == '#')
                            continue;
                        int tokEnd = pos;
                        while (tokEnd < s.Length && s[tokEnd] != ' ' && s[tokEnd] != '\t')
                            tokEnd++;
                        char c = s[pos];
                        if (c == 'v' && tokEnd - pos == 1)
                        {
                            if (TryParseVec3(s, ref tokEnd, out Vector3 v))
                            {
                                // 右手 → Unity 左手：绕 X 轴 180°，保绕序
                                v.y = -v.y;
                                v.z = -v.z;
                                data.Positions.Add(v);
                            }
                        }
                        else if (c == 'v' && tokEnd - pos == 2 && s[pos + 1] == 't')
                        {
                            if (TryParseVec2(s, ref tokEnd, out Vector2 uv))
                            {
                                uv.y = 1f - uv.y;
                                data.Uvs.Add(uv);
                                data.HasUv = true;
                            }
                        }
                        else if (c == 'v' && tokEnd - pos == 2 && s[pos + 1] == 'n')
                        {
                            if (TryParseVec3(s, ref tokEnd, out Vector3 n))
                            {
                                n.y = -n.y;
                                n.z = -n.z;
                                data.Normals.Add(n);
                                data.HasNormal = true;
                            }
                        }
                        else if (c == 'f')
                        {
                            ParseFace(s, ref tokEnd, data.Positions.Count, data.Uvs.Count, data.Normals.Count, data.Faces);
                        }
                        // o / g / usemtl / mtllib / s / vp 等关键字首版忽略（单 submesh 合并）
                    }
                }
            }
            catch (Exception arg)
            {
                Debug.LogError($"[FML] Except on parsing OBJ ({fileLoc}): {arg}");
                result = null!;
                return false;
            }

            if (data.Positions.Count == 0 || data.Faces.Count == 0)
            {
                Debug.LogError($"[FML] Invalid OBJ (no positions/faces): {fileLoc} (line {lineNo})");
                result = null!;
                return false;
            }

            // 顶点唯一化（按 (v, vt, vn) 三元组展开，同位置不同 UV 的面渲染正确）
            Dictionary<VertKey, int> unique = new Dictionary<VertKey, int>(data.Positions.Count);
            data.FinalPositions = new List<Vector3>(data.Positions.Count);
            data.FinalUvs = data.HasUv ? new List<Vector2>(data.Positions.Count) : new List<Vector2>(0);
            data.FinalNormals = data.HasNormal ? new List<Vector3>(data.Positions.Count) : new List<Vector3>(0);
            data.FinalIndices = new List<int>(data.Faces.Count);
            foreach (FaceRef face in data.Faces)
            {
                if (!unique.TryGetValue(new VertKey(face.P, face.U, face.N), out int idx))
                {
                    idx = data.FinalPositions.Count;
                    unique.Add(new VertKey(face.P, face.U, face.N), idx);
                    data.FinalPositions.Add(data.Positions[face.P]);
                    if (data.HasUv)
                        data.FinalUvs.Add(face.U >= 0 ? data.Uvs[face.U] : Vector2.zero);
                    if (data.HasNormal)
                        data.FinalNormals.Add(face.N >= 0 ? data.Normals[face.N] : Vector3.zero);
                }
                data.FinalIndices.Add(idx);
            }
            data.NeedsRecalcNormals = !data.HasNormal;
            result = data;
            return true;
        }

        private static void ParseFace(ReadOnlySpan<char> s, ref int pos, int posCount, int uvCount, int nrmCount, List<FaceRef> faces)
        {
            if (!TryReadFaceVertex(s, ref pos, posCount, uvCount, nrmCount, out FaceRef first))
                return;
            if (!TryReadFaceVertex(s, ref pos, posCount, uvCount, nrmCount, out FaceRef prev))
                return;
            while (TryReadFaceVertex(s, ref pos, posCount, uvCount, nrmCount, out FaceRef cur))
            {
                // n 边形扇形三角化
                faces.Add(first);
                faces.Add(prev);
                faces.Add(cur);
                prev = cur;
            }
        }

        private static bool TryReadFaceVertex(ReadOnlySpan<char> s, ref int pos, int posCount, int uvCount, int nrmCount, out FaceRef r)
        {
            r = default;
            while (pos < s.Length && (s[pos] == ' ' || s[pos] == '\t'))
                pos++;
            if (pos >= s.Length)
                return false;
            int end = pos;
            while (end < s.Length && s[end] != ' ' && s[end] != '\t')
                end++;
            ReadOnlySpan<char> tok = s.Slice(pos, end - pos);
            pos = end;
            if (tok.IsEmpty)
                return false;

            // 按 '/' 拆分 v[/vt[/vn]]
            int s1 = tok.IndexOf('/');
            ReadOnlySpan<char> pTok;
            ReadOnlySpan<char> uTok = default;
            ReadOnlySpan<char> nTok = default;
            if (s1 < 0)
            {
                pTok = tok;
            }
            else
            {
                pTok = tok.Slice(0, s1);
                int s2 = tok.Slice(s1 + 1).IndexOf('/');
                if (s2 < 0)
                {
                    uTok = tok.Slice(s1 + 1);
                }
                else
                {
                    uTok = tok.Slice(s1 + 1, s2);
                    nTok = tok.Slice(s1 + 2 + s2);
                }
            }
            if (!TryParseIntRef(pTok, posCount, out int p))
                return false;
            int u = -1;
            int n = -1;
            if (!uTok.IsEmpty && !TryParseIntRef(uTok, uvCount, out u))
                return false;
            if (!nTok.IsEmpty && !TryParseIntRef(nTok, nrmCount, out n))
                return false;
            r = new FaceRef(p, u, n);
            return true;
        }

        /// <summary>OBJ 索引：1-based → 0-based；负值表示"当前列表倒数第 n 个"。</summary>
        private static bool TryParseIntRef(ReadOnlySpan<char> tok, int count, out int value)
        {
            value = 0;
            if (tok.IsEmpty)
                return false;
            if (!int.TryParse(tok, NumberStyles.Integer, CultureInfo.InvariantCulture, out int raw))
                return false;
            if (raw == 0)
                return false;
            value = raw > 0 ? raw - 1 : count + raw;
            return value >= 0 && value < count;
        }

        private static bool TryParseVec3(ReadOnlySpan<char> s, ref int pos, out Vector3 v)
        {
            v = default;
            if (!TryReadFloat(s, ref pos, out float x))
                return false;
            if (!TryReadFloat(s, ref pos, out float y))
                return false;
            if (!TryReadFloat(s, ref pos, out float z))
                return false;
            v = new Vector3(x, y, z);
            return true;
        }

        private static bool TryParseVec2(ReadOnlySpan<char> s, ref int pos, out Vector2 v)
        {
            v = default;
            if (!TryReadFloat(s, ref pos, out float x))
                return false;
            if (!TryReadFloat(s, ref pos, out float y))
                return false;
            v = new Vector2(x, y);
            return true;
        }

        private static bool TryReadFloat(ReadOnlySpan<char> s, ref int pos, out float value)
        {
            while (pos < s.Length && (s[pos] == ' ' || s[pos] == '\t'))
                pos++;
            int end = pos;
            while (end < s.Length && s[end] != ' ' && s[end] != '\t')
                end++;
            if (end == pos)
            {
                value = 0f;
                return false;
            }
            if (!float.TryParse(s.Slice(pos, end - pos), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return false;
            pos = end;
            return true;
        }
    }
}
