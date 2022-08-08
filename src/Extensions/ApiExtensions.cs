using System;
using System.IO;
using Vintagestory.API.Common;

namespace PlayerCorpse
{
    public static class ApiExtensions
    {
        public static string GetWorldId(this ICoreAPI api)
        {
            if (api == null || api.World == null) return null;
            return api.World.SavegameIdentifier;
        }

        public static T LoadOrCreateConfig<T>(this ICoreAPI api, string file, T defaultConfig = default(T)) where T : new()
        {
            try
            {
                T loadedConfig = api.LoadModConfig<T>(file);
                if (loadedConfig != null)
                {
                    api.StoreModConfig<T>(loadedConfig, file);
                    return loadedConfig;
                }
            }
            catch (Exception e)
            {
                string format = "Failed loading file ({0}), error {1}. Will initialize new one";
                Core.ModLogger.Error(string.Format(format), file, e);
            }

            T newConfig;
            if (defaultConfig == null)
            {
                newConfig = defaultConfig;
            }
            else if (defaultConfig.Equals(default(T)))
            {
                newConfig = defaultConfig;
            }
            else
            {
                newConfig = new T();
            }

            api.StoreModConfig<T>(newConfig, file);
            return newConfig;
        }

        public static T LoadDataFile<T>(this ICoreAPI api, string file)
        {
            try
            {
                if (File.Exists(file))
                {
                    var content = File.ReadAllText(file);
                    return JsonUtil.FromString<T>(content);
                }
            }
            catch (Exception e)
            {
                string format = "Failed loading file ({0}), error {1}";
                Core.ModLogger.Error(string.Format(format, file, e));
            }

            return default(T);
        }

        public static T LoadOrCreateDataFile<T>(this ICoreAPI api, string file) where T : new()
        {
            var data = api.LoadDataFile<T>(file);
            if (data.Equals(default(T))) return data;

            Core.ModLogger.Notification("Will initialize new one");

            var newData = new T();
            SaveDataFile(api, file, newData);
            return newData;
        }

        public static void SaveDataFile<T>(this ICoreAPI api, string file, T data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                var content = JsonUtil.ToString(data);
                File.WriteAllText(file, content);
            }
            catch (Exception e)
            {
                string format = "Failed loading file ({0}), error {1}";
                Core.ModLogger.Error(string.Format(format, file, e));
            }
        }
    }
}