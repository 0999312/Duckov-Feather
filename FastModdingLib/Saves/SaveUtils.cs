using FeatherMod.Utils;
using Saves;
using UnityEngine;

namespace FeatherMod.Saves;

/// <summary>
/// FML 统一存档 API。所有模块应通过本类读写存档键，使用 <see cref="Identifier"/>
/// 作为键——最终 ES3 键为 <c>"FeatherMod" + identifier.ToString()</c>，
/// 与游戏原生键（<c>QuestData</c>、<c>SaveTime</c>、<c>IsOldGame</c> 等）天然隔离，
/// 杜绝键碰撞覆盖原生数据的风险。
/// </summary>
/// <remarks>
/// <para><b>键命名约定</b>：Domain=modid（外部 mod）或 <c>"feather"</c>（FML 自身）；Path=描述性键名。</para>
/// <para><b>生命周期</b>：</para>
/// <list type="bullet">
/// <item><see cref="Save{T}"/> 在值为 <c>null</c> 时主动删除键</item>
/// <item><see cref="Delete{T}"/> 显式删除键（不论是否存在）</item>
/// <item>存档槽位删除时由 <c>SaveDeletedEvent</c> 通知各模块清理内存状态（见 <see cref="Events.GameEvents.SaveDeletedEvent"/>）</item>
/// </list>
/// <para><b>保留键警告</b>：若 <c>Path</c> 与游戏原生保留键同名（<c>QuestData</c>、<c>SaveTime</c>、
/// <c>IsOldGame</c>、<c>Created</c> 等），即使加了 <c>FeatherMod</c> 前缀，<see cref="Save{T}"/> 也会打印警告，
/// 提醒 modder 改名以避免与未来版本原生 ES3 直接读取路径冲突。</para>
/// </remarks>
public class SaveUtils
{
    /// <summary>
    /// 游戏原生保留键白名单。即使加上 <c>FeatherMod</c> 前缀仍可能与原生路径发生语义混淆，
    /// 触发写入警告。来源：反编译 <c>SavesSystem</c> 与各 ISaveDataProvider 实现。
    /// </summary>
    private static readonly System.Collections.Generic.HashSet<string> ReservedNativeKeys =
        new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal)
        {
            "QuestData", "SaveTime", "IsOldGame", "Created",
            "EconomyData", "GameClock", "ActiveModList",
            // 原版 Item/Inventory 命名空间
            "Item/", "Inventory/", "Count/",
        };

    /// <summary>从 <see cref="Identifier"/> 推导最终 ES3 键。</summary>
    public static string MakeKey(Identifier identifierKey)
        => ModBehaviour.FrameworkName + identifierKey;

    /// <summary>检查 ES3 文件中是否存在指定键。</summary>
    public static bool KeyExists(Identifier identifierKey)
        => SavesSystem.KeyExisits(MakeKey(identifierKey));

    /// <summary>
    /// 读取键值。键不存在时静默返回 <c>default(T)</c>（不触发 ES3 "Key not found" 警告）。
    /// </summary>
    public static T? Load<T>(Identifier identifierKey)
    {
        return
            SavesSystem.KeyExisits(MakeKey(identifierKey)) ?
                SavesSystem.Load<T>(MakeKey(identifierKey)) :
                default;
    }

    /// <summary>
    /// 读取键值；键不存在时返回 <paramref name="defaultValue"/>。
    /// </summary>
    public static T Load<T>(Identifier identifierKey, T defaultValue)
    {
        return SavesSystem.KeyExisits(MakeKey(identifierKey))
            ? SavesSystem.Load<T>(MakeKey(identifierKey))
            : defaultValue;
    }

    /// <summary>
    /// 写入键值。值为 <c>null</c> 时等价于 <see cref="Delete{T}"/>。
    /// 写入前会检查 <see cref="ReservedNativeKeys"/> 并打印警告。
    /// </summary>
    public static void Save<T>(Identifier identifierKey, T? value)
    {
        WarnIfReserved(identifierKey);
        if (value == null)
        {
            Delete<T>(identifierKey);
            return;
        }

        SavesSystem.Save<T>(MakeKey(identifierKey), (T)value);
    }

    /// <summary>
    /// 显式删除键。键不存在时静默跳过。
    /// </summary>
    public static void Delete<T>(Identifier identifierKey)
    {
        var key = MakeKey(identifierKey);
        if (SavesSystem.KeyExisits(key))
        {
            ES3.DeleteKey(key, SavesSystem.CurrentFilePath);
        }
    }

    private static void WarnIfReserved(Identifier identifierKey)
    {
        try
        {
            var path = identifierKey.Path;
            if (string.IsNullOrEmpty(path)) return;
            foreach (var reserved in ReservedNativeKeys)
            {
                if (path == reserved || (reserved.EndsWith("/") && path.StartsWith(reserved, System.StringComparison.Ordinal)))
                {
                    Debug.LogWarning(
                        $"[FML SaveUtils] Identifier path '{path}' matches reserved native key '{reserved}'. "
                        + "Even with FeatherMod prefix this may collide with vanilla ES3 access paths. "
                        + "Rename the Identifier path to a mod-specific value.");
                    return;
                }
            }
        }
        catch
        {
            // Best-effort warning; never break save flow.
        }
    }
}
