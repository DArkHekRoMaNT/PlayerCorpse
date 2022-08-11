using System;
using System.IO;
using Vintagestory.API.Common;

namespace PlayerCorpse
{
    public static class ApiExtensions
    {
        public static string GetWorldId(this ICoreAPI api)
        {
            if (api != null && api.World != null)
            {
                return api.World.SavegameIdentifier;
            }
            else
            {
                return null;
            }
        }

        public static TConfig LoadOrCreateConfig<TConfig>(this ICoreAPI api, string file, TConfig defaultConfig = null) where TConfig : class, new()
        {
            TConfig config = null;

            try
            {
                config = api.LoadModConfig<TConfig>(file);
            }
            catch (Exception e)
            {
                string format = "Failed loading config file ({0}), error {1}. Will initialize default config";
                Core.ModLogger.Error(string.Format(format), file, e);
            }

            if (config == null)
            {
                Core.ModLogger.Notification("Will initialize default config");
                config = defaultConfig ?? new TConfig();
            }

            api.StoreModConfig(config, file);
            return config;
        }

        public static TData LoadDataFile<TData>(this ICoreAPI api, string file) where TData : class, new()
        {
            try
            {
                if (File.Exists(file))
                {
                    var content = File.ReadAllText(file);
                    return JsonUtil.FromString<TData>(content);
                }
            }
            catch (Exception e)
            {
                string format = "Failed loading data file ({0}), error {1}. Will initialize new data file";
                Core.ModLogger.Error(string.Format(format, file, e));
            }

            return new TData();
        }

        public static TData LoadOrCreateDataFile<TData>(this ICoreAPI api, string file) where TData : class, new()
        {
            var data = api.LoadDataFile<TData>(file);
            if (data == null)
            {
                Core.ModLogger.Notification("Will initialize new data file");
                data = new TData();
                SaveDataFile(api, file, data);
            }
            return data;
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
                string format = "Failed saving file ({0}), error {1}";
                Core.ModLogger.Error(string.Format(format, file, e));
            }
        }
    }
}