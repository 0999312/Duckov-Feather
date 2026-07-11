using System;
using System.Reflection;

using ES3Internal;

using ES3Types;

using UnityEngine;

namespace FeatherMod.Saves;

public static class ES3Validator
{
    public static bool CanBeSerializedByES3<T>()
    {
        return CanBeSerializedByES3(typeof(T));
    }

    public static bool CanBeSerializedByES3(Type type)
    {
        if (type == null) return false;

        try
        {
            ES3Type es3Type = ES3TypeMgr.GetOrCreateES3Type(type);

            if (es3Type == null) return false;
            if (es3Type.isUnsupported) return false;
            if (es3Type is ES3ObjectType)
            {
                if (typeof(MonoBehaviour).IsAssignableFrom(type) ||
                    typeof(ScriptableObject).IsAssignableFrom(type))
                {
                    Debug.Log("Serialization of MonoBehaviour and ScriptableObject is not supported.");
                    return false;
                }

                ConstructorInfo? ctor = type.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null
                );

                if (ctor == null)
                {
                    bool hasValidES3Ctor = false;
                    var ctors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var c in ctors)
                    {
                        if (c.GetCustomAttribute<ES3Serializable>() != null)
                        {
                            hasValidES3Ctor = true;
                            break;
                        }
                    }
                    if (!hasValidES3Ctor) return false;
                }
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return false;
        }
    }
}
