using System;
using Vintagestory.API.Common;

namespace PlayerCorpse
{

    public static class ApiExtensions
    {
        public static T LoadOrCreateConfig<T>(this ICoreAPI api, string filename) where T : new()
        {
            // Try load config for this world
            try
            {
                T loadedWorldConfig = api.LoadModConfig<T>(api.World.SavegameIdentifier + "/" + filename);
                if (loadedWorldConfig != null)
                {
                    api.StoreModConfig<T>(loadedWorldConfig, api.World.SavegameIdentifier + "/" + filename);
                    return loadedWorldConfig;
                }
            }
            catch (Exception e)
            {
                api.World.Logger.Error(e.Message);
            }

            // Else try load root config (and save as world config)
            try
            {
                T loadedRootConfig = api.LoadModConfig<T>(filename);
                if (loadedRootConfig != null)
                {
                    api.StoreModConfig<T>(loadedRootConfig, filename);
                    api.StoreModConfig<T>(loadedRootConfig, api.World.SavegameIdentifier + "/" + filename);
                    return loadedRootConfig;
                }
            }
            catch (Exception e)
            {
                api.World.Logger.Error(e.Message);
            }

            // Else create defaul config (and save as root and world config)
            var newConfig = new T();
            api.StoreModConfig<T>(newConfig, filename);
            api.StoreModConfig<T>(newConfig, api.World.SavegameIdentifier + "/" + filename);
            return newConfig;
        }
    }
}