using System.Collections.Generic;

using FeatherMod.Utils;

namespace FeatherMod.Items;

public class TagsDesc
{
    public List<Identifier> requiredTags = new();
    public List<Identifier> excludeTags = new();
}
