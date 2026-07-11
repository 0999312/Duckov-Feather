using FeatherMod.Utils;

using Saves;

namespace FeatherMod.Saves;

public class SaveUtils
{
    public static T? Load<T>(Identifier identifierKey)
    {
        return
            SavesSystem.KeyExisits(ModBehaviour.FrameworkName, identifierKey.ToString()) ?
                SavesSystem.Load<T>(ModBehaviour.FrameworkName, identifierKey.ToString()) :
                default;
    }

    public static void Save<T>(Identifier identifierKey, T? value)
    {
        if (value == null)
        {
            if (SavesSystem.KeyExisits(ModBehaviour.FrameworkName, identifierKey.ToString()))
            {
                ES3.DeleteKey(ModBehaviour.FrameworkName + identifierKey, SavesSystem.CurrentFilePath);
            }
            return;
        }

        SavesSystem.Save(ModBehaviour.FrameworkName, identifierKey.ToString(), value);
    }
}
