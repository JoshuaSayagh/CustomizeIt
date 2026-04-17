using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using CustomizeIt.Systems;
using Game;
using Game.Modding;
using Game.SceneFlow;
using HarmonyLib;

namespace CustomizeIt
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(CustomizeIt)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        public static Setting Setting { get; private set; }

        private Harmony _harmony;

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            _harmony = new Harmony("JoshuaSayagh.CustomizeIt");
            _harmony.PatchAll(typeof(Mod).Assembly);
            log.Info("Harmony patches applied.");

            Setting = new Setting(this);
            Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Setting));
            GameManager.instance.localizationManager.AddSource("fr-FR", new LocaleFR(Setting));

            AssetDatabase.global.LoadSettings(nameof(CustomizeIt), Setting, new Setting(this));

            updateSystem.UpdateAt<BuildingAttractivenessUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAfter<AttractivenessOverrideSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAt<TouristBoostSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<TourismDebugSystem>(SystemUpdatePhase.GameSimulation);
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));

            _harmony?.UnpatchAll("JoshuaSayagh.CustomizeIt");
            _harmony = null;

            if (Setting != null)
            {
                Setting.UnregisterInOptionsUI();
                Setting = null;
            }
        }
    }
}
