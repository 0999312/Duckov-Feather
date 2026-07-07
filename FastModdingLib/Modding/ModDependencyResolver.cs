using Duckov.Modding;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FeatherMod.Modding
{
    /// <summary>
    /// Mod 依赖排序器。先按声明式 priority 排序，再通过拓扑排序解析 dependencies 约束，
    /// 确保依赖 mod 始终排在依赖者之前。含循环依赖检测。
    /// </summary>
    public static class ModDependencyResolver
    {
        /// <summary>
        /// 对 <paramref name="modInfos"/> 原地排序。
        /// 排序键：声明式 priority（默认 int.MaxValue）→ 拓扑序（依赖者排在被依赖者之后）。
        /// 用于 Rescan（初始化阶段），确保声明式优先级 + 依赖关系全部生效。
        /// </summary>
        public static void Sort(List<ModInfo> modInfos)
        {
            if (modInfos == null || modInfos.Count <= 1) return;

            // 1. 构建 name → index 映射
            var nameToIndex = new Dictionary<string, int>();
            for (int i = 0; i < modInfos.Count; i++)
            {
                nameToIndex[modInfos[i].name] = i;
            }

            // 2. 按声明式 priority 预排序（稳定排序：同 priority 保持 Rescan 原始顺序；
            //    LINQ OrderBy 保证稳定，List.Sort 不保证）
            var prioritySorted = modInfos
                .OrderBy(m => ModMetaCache.Get(m.name).Priority)
                .ToList();
            modInfos.Clear();
            modInfos.AddRange(prioritySorted);

            // 3. 拓扑排序：确保依赖关系成立
            var graph = BuildGraph(modInfos, nameToIndex);
            var cycle = DetectCycle(graph, modInfos);
            if (cycle != null)
            {
                Debug.LogWarning($"[FML ModDependencyResolver] Circular dependency detected: {string.Join(" → ", cycle)}. Resolving by priority order.");
                return; // 有循环依赖时保持按 priority 的排序结果
            }

            var sorted = TopologicalSort(graph, modInfos);
            modInfos.Clear();
            modInfos.AddRange(sorted);

            LogSortResult(modInfos);
        }

        /// <summary>
        /// 对 <paramref name="modInfos"/> 按依赖关系原地排序，但<span class="keyword">保持玩家手动顺序</span>。
        /// 仅通过拓扑排序确保硬性依赖（dependencies / loadAfter / loadBefore），
        /// <span class="keyword">不</span>按 fml.json 声明的 priority 重排。
        /// 用于 Reorder（玩家手动拖拽排序后）修正依赖关系。
        /// 未声明依赖关系的 mod 顺序完全不受影响。
        /// </summary>
        public static void SortByDependencyOnly(List<ModInfo> modInfos)
        {
            if (modInfos == null || modInfos.Count <= 1) return;

            // 1. 构建索引（保持玩家当前顺序）
            var nameToIndex = new Dictionary<string, int>();
            for (int i = 0; i < modInfos.Count; i++)
            {
                nameToIndex[modInfos[i].name] = i;
            }

            // 2. 构建依赖图，检测循环
            var graph = BuildGraph(modInfos, nameToIndex);
            var cycle = DetectCycle(graph, modInfos);
            if (cycle != null)
            {
                Debug.LogWarning($"[FML ModDependencyResolver] Circular dependency detected during reorder: {string.Join(" → ", cycle)}. Keeping player order.");
                return;
            }

            // 3. 拓扑排序，保持玩家顺序
            var sorted = TopologicalSortPreserveOrder(graph, modInfos);
            modInfos.Clear();
            modInfos.AddRange(sorted);
        }

        private static HashSet<int>[] BuildGraph(List<ModInfo> modInfos, Dictionary<string, int> nameToIndex)
        {
            int n = modInfos.Count;
            var graph = new HashSet<int>[n];
            for (int i = 0; i < n; i++) graph[i] = new HashSet<int>();

            // 重新构建索引（排序后）
            nameToIndex.Clear();
            var workshopIdToIndex = new Dictionary<string, int>();
            for (int i = 0; i < n; i++)
            {
                nameToIndex[modInfos[i].name] = i;
                // publishedFileId 非零时建立 workshopId → index 映射（字符串键）
                if (modInfos[i].publishedFileId != 0)
                    workshopIdToIndex[modInfos[i].publishedFileId.ToString()] = i;
            }

            // 边：dep → dependent（dep 必须在 dependent 之前）
            // hard dependencies + soft loadAfter（仅当目标存在时生效）
            for (int i = 0; i < n; i++)
            {
                var meta = ModMetaCache.Get(modInfos[i].name);

                AddHardEdges(meta.Dependencies);
                AddSoftEdges(meta.LoadAfter);
                AddSoftEdgesReversed(meta.LoadBefore);

                void AddHardEdges(ModDependency[]? deps)
                {
                    if (deps == null) return;
                    foreach (var dep in deps)
                    {
                        if (dep.IsEmpty) continue;
                        int? targetIdx = ResolveDepIndex(dep);
                        if (targetIdx.HasValue && targetIdx.Value != i)
                        {
                            graph[targetIdx.Value].Add(i);
                        }
                        else
                        {
                            Debug.LogWarning($"[FML ModDependencyResolver] Hard dependency '{dep}' of '{modInfos[i].name}' not found in loaded mods.");
                        }
                    }
                }

                void AddSoftEdges(string[]? targets)
                {
                    if (targets == null) return;
                    foreach (var targetName in targets)
                    {
                        if (string.IsNullOrEmpty(targetName)) continue;
                        if (nameToIndex.TryGetValue(targetName, out int targetIdx) && targetIdx != i)
                        {
                            graph[targetIdx].Add(i);
                        }
                        // soft dep 缺失时静默跳过
                    }
                }

                void AddSoftEdgesReversed(string[]? targets)
                {
                    if (targets == null) return;
                    foreach (var targetName in targets)
                    {
                        if (string.IsNullOrEmpty(targetName)) continue;
                        if (nameToIndex.TryGetValue(targetName, out int targetIdx) && targetIdx != i)
                        {
                            graph[i].Add(targetIdx);
                        }
                    }
                }

                int? ResolveDepIndex(ModDependency dep)
                {
                    // Name 优先
                    if (!string.IsNullOrEmpty(dep.Name) && nameToIndex.TryGetValue(dep.Name, out var i1))
                        return i1;
                    // WorkshopId 兜底
                    if (!string.IsNullOrEmpty(dep.WorkshopId) && workshopIdToIndex.TryGetValue(dep.WorkshopId, out var i2))
                        return i2;
                    return null;
                }
            }
            return graph;
        }

        private static List<string>? DetectCycle(HashSet<int>[] graph, List<ModInfo> modInfos)
        {
            int n = graph.Length;
            var state = new int[n]; // 0=unvisited, 1=visiting, 2=visited
            var dfsPath = new List<int>();

            bool Dfs(int u)
            {
                if (state[u] == 1)
                {
                    // 找到环：从 dfsPath 中提取 u → ... → u
                    int cycleStart = dfsPath.IndexOf(u);
                    var cycle = new List<string>();
                    for (int i = cycleStart; i < dfsPath.Count; i++)
                    {
                        cycle.Add(modInfos[dfsPath[i]].name);
                    }
                    cycle.Add(modInfos[u].name); // 闭合
                    return true; // 通过外部 closure 返回 cycle
                }
                if (state[u] == 2) return false;
                state[u] = 1;
                dfsPath.Add(u);
                foreach (int v in graph[u])
                {
                    if (Dfs(v))
                    {
                        // 向上传播 cycle 信息
                        return true;
                    }
                }
                dfsPath.RemoveAt(dfsPath.Count - 1);
                state[u] = 2;
                return false;
            }

            // 闭包捕获 cycle 列表
            List<string>? detectedCycle = null;
            bool DfsWrapper(int u)
            {
                if (state[u] == 1)
                {
                    int cycleStart = dfsPath.IndexOf(u);
                    detectedCycle = new List<string>();
                    for (int i = cycleStart; i < dfsPath.Count; i++)
                    {
                        detectedCycle.Add(modInfos[dfsPath[i]].name);
                    }
                    detectedCycle.Add(modInfos[u].name);
                    return true;
                }
                if (state[u] == 2) return false;
                state[u] = 1;
                dfsPath.Add(u);
                foreach (int v in graph[u])
                {
                    if (DfsWrapper(v)) return true;
                }
                dfsPath.RemoveAt(dfsPath.Count - 1);
                state[u] = 2;
                return false;
            }

            for (int i = 0; i < n; i++)
            {
                if (state[i] == 0 && DfsWrapper(i))
                    return detectedCycle;
            }
            return null;
        }

        private static List<ModInfo> TopologicalSort(HashSet<int>[] graph, List<ModInfo> modInfos)
        {
            int n = modInfos.Count;
            var indegree = new int[n];
            for (int u = 0; u < n; u++)
                foreach (int v in graph[u])
                    indegree[v]++;

            var queue = new Queue<int>();
            for (int i = 0; i < n; i++)
                if (indegree[i] == 0)
                    queue.Enqueue(i);

            var result = new List<ModInfo>(n);
            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                result.Add(modInfos[u]);
                foreach (int v in graph[u])
                {
                    indegree[v]--;
                    if (indegree[v] == 0)
                        queue.Enqueue(v);
                }
            }

            // 如果拓扑排序结果数量不对，把剩余的按原顺序追加（有环的情况已在 DetectCycle 处理）
            if (result.Count < n)
            {
                var included = new HashSet<int>(n);
                for (int i = 0; i < result.Count; i++) included.Add(i);
                for (int i = 0; i < n; i++)
                    if (!included.Contains(i))
                        result.Add(modInfos[i]);
            }

            return result;
        }

        /// <summary>
        /// 拓扑排序，尽量保持 modInfos 的原有顺序（玩家手动排序）。
        /// 当多个节点入度均为 0 时，优先选择玩家排在前面（索引小）的节点。
        /// 对于没有依赖关系的 mod，结果顺序与输入完全一致。
        /// </summary>
        private static List<ModInfo> TopologicalSortPreserveOrder(HashSet<int>[] graph, List<ModInfo> modInfos)
        {
            int n = modInfos.Count;
            var indegree = new int[n];
            for (int u = 0; u < n; u++)
                foreach (int v in graph[u])
                    indegree[v]++;

            // 维护入度为 0 的节点，按玩家索引（小索引 = 排在前面）排序
            var ready = new List<int>();
            for (int i = 0; i < n; i++)
                if (indegree[i] == 0)
                    ready.Add(i);
            ready.Sort();

            var result = new List<ModInfo>(n);
            var includedIndices = new HashSet<int>();
            while (ready.Count > 0)
            {
                int u = ready[0];
                ready.RemoveAt(0);
                result.Add(modInfos[u]);
                includedIndices.Add(u);
                foreach (int v in graph[u])
                {
                    indegree[v]--;
                    if (indegree[v] == 0)
                    {
                        int insertPos = ready.BinarySearch(v);
                        if (insertPos < 0) insertPos = ~insertPos;
                        ready.Insert(insertPos, v);
                    }
                }
            }

            // 有环时把未包含的节点按原顺序追加
            if (result.Count < n)
            {
                for (int i = 0; i < n; i++)
                    if (!includedIndices.Contains(i))
                        result.Add(modInfos[i]);
            }

            return result;
        }

        private static void LogSortResult(List<ModInfo> modInfos)
        {
            var sb = new StringBuilder("[FML] Mod load order:");
            for (int i = 0; i < modInfos.Count; i++)
            {
                var meta = ModMetaCache.Get(modInfos[i].name);
                sb.Append($"\n  {i + 1}. {modInfos[i].name}");
                if (meta.Loaded)
                {
                    sb.Append($" (priority={meta.Priority}");
                    if (meta.Dependencies.Length > 0)
                        sb.Append($", deps=[{string.Join(",", meta.Dependencies)}]");
                    sb.Append(")");
                }
            }
            Debug.Log(sb.ToString());
        }
    }
}
