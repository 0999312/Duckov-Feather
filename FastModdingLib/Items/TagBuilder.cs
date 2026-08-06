using Duckov.Utilities;

using UnityEngine;

namespace FeatherMod.Items;

public class TagBuilder
{
    private bool show;
    private bool showDesc;
    private Color color;
    private int priority;
    private string name;

    public TagBuilder()
    {
        show = true;
        showDesc = true;
        color = Color.black;
        priority = 0;
    }

    public TagBuilder Show(bool show)
    {
        this.show = show;
        return this;
    }

    public TagBuilder ShowDescription(bool showDesc)
    {
        this.showDesc = showDesc;
        return this;
    }

    public TagBuilder Colour(Color color)
    {
        this.color = color;
        return this;
    }

    public TagBuilder Priority(int priority)
    {
        this.priority = priority;
        return this;
    }

    internal TagBuilder Name(string name)
    {
        this.name = name;
        return this;
    }

    internal Tag Instantiate()
    {
        Tag r = ScriptableObject.CreateInstance<Tag>();
        r.show = show;
        r.showDescription = showDesc;
        r.color = color;
        r.priority = priority;
        r.name = name;

        return r;
    }
}
