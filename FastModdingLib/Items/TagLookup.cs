using System.Collections.Generic;
using System.Threading;

using Duckov.Utilities;

using FeatherMod.Register;
using FeatherMod.Utils;

namespace FeatherMod.Items;

public class TagLookup
{
    private static readonly Dictionary<Identifier, string> _idToTag = new();
    private static readonly Dictionary<string, Identifier> _tagToId = new();

    public static bool TryGetIdentifier(string tag, out Identifier id)
    {
        if (_tagToId.TryGetValue(tag, out id)) return true;
        if (RegistryManager.Instance.TagRegistry.TryGetIdentifier(tag, out id)) return true;
        if (GameplayDataSettings.Tags.allTags.Exists(t => t.name.Equals(tag)))
        {
            id = new Identifier(FMLConstants.DuckovDomain, SanitizePath(tag));
            _idToTag[id] = tag;
            _tagToId[tag] = id;
            return true;
        }

        return false;
    }

    public static string? GetNativeMayNotExist(Identifier tag)
    {
        if (tag.Domain.Equals(FMLConstants.DuckovDomain))
        {
            return tag.Path;
        }
        if (RegistryManager.Instance.TagRegistry.TryGet(tag, out var tagObj))
        {
            return RegistryManager.Instance.TagRegistry.GetNativeKey(tagObj);
        }

        return null;
    }

    private static string SanitizePath(string raw)
    {
        return raw
            .Replace("\\", "_")
            .Replace(":", "_")
            .Replace("..", "__")
            .Replace("/", "_");
    }
}
