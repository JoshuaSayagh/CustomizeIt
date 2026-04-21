using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using CustomizeIt.Systems;
using Game;
using Game.Modding;
using Game.SceneFlow;

namespace CustomizeIt
{
    public class Mod : IMod
    {
        public const string ModId = nameof(CustomizeIt);

        public static ILog log = LogManager.GetLogger($"{nameof(CustomizeIt)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        public static Setting Setting { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            Setting = new Setting(this);
            Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Setting));
            GameManager.instance.localizationManager.AddSource("fr-FR", new LocaleFR(Setting));

            AssetDatabase.global.LoadSettings(ModId, Setting, new Setting(this));

            updateSystem.UpdateAt<BuildingAttractivenessUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAfter<AttractivenessOverrideSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAt<TouristBoostSystem>(SystemUpdatePhase.GameSimulation);
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));

            if (Setting != null)
            {
                Setting.UnregisterInOptionsUI();
                Setting = null;
            }
        }
    }
}
