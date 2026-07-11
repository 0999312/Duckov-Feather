using FeatherMod.Utils;

using Saves;

namespace FeatherMod.Saves;

public class SaveUtils
{
    public static T? Load<T>(Identifier identifierKey)
    {
        return
            SavesSystem.KeyExisits(ModBehaviour.FrameworkName + identifierKey) ?
                SavesSystem.Load<T>(ModBehaviour.FrameworkName + identifierKey) :
                default;
    }

    public static void Save<T>(Identifier identifierKey, T? value)
    {
        if (value == null)
        {
            if (SavesSystem.KeyExisits(ModBehaviour.FrameworkName + identifierKey))
            {
                ES3.DeleteKey(ModBehaviour.FrameworkName + identifierKey, SavesSystem.CurrentFilePath);
            }
            return;
        }

        SavesSystem.Save<T>(ModBehaviour.FrameworkName + identifierKey, (T) value);
    }
}
