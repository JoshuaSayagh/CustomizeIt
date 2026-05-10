using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using System.Collections.Generic;

namespace CustomizeIt
{
    [FileLocation("ModsSettings/CustomTourism/CustomTourism")]
    [SettingsUIGroupOrder(TourismGroup, ResetGroup)]
    [SettingsUIShowGroupName(TourismGroup, ResetGroup)]
    public class Setting : ModSetting
    {
        private const int MinTarget = 0;
        private const int MaxTarget = 60000;
        private const int DefaultTarget = 0;

        // Tabs
        public const string TourismTab = "Tourism";

        // Groups
        public const string TourismGroup = "Tourism Settings";
        public const string ResetGroup = "Reset";

        public Setting(IMod mod) : base(mod)
        {
        }

        // ---- Existing: per-building attractiveness overrides ----

        public string[] OverridePrefabNames { get; set; } = new string[0];
        public int[] OverrideValues { get; set; } = new int[0];

        // ---- Tourism: target tourist count ----

        [SettingsUISlider(min = 0, max = 60000, step = 500, scalarMultiplier = 1, unit = Unit.kInteger)]
        [SettingsUISection(TourismTab, TourismGroup)]
        public int TargetTouristCount { get; set; }

        [SettingsUIButton]
        [SettingsUISection(TourismTab, ResetGroup)]
        public bool ResetTourism
        {
            set
            {
                if (!value) return;
                TargetTouristCount = DefaultTarget;
                ApplyAndSave();
            }
        }

        public override void SetDefaults()
        {
            OverridePrefabNames = new string[0];
            OverrideValues = new int[0];
            TargetTouristCount = DefaultTarget;
        }

        public override void Apply()
        {
            if (TargetTouristCount < MinTarget || TargetTouristCount > MaxTarget)
                TargetTouristCount = DefaultTarget;
            base.Apply();
        }
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;

        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Custom Tourism" },

                // Tabs
                { m_Setting.GetOptionTabLocaleID(Setting.TourismTab), "Tourism" },

                // Groups
                { m_Setting.GetOptionGroupLocaleID(Setting.TourismGroup), "Tourism Settings" },
                { m_Setting.GetOptionGroupLocaleID(Setting.ResetGroup), "Reset" },

                // Target tourist count slider
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.TargetTouristCount)), "Target Tourist Count" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.TargetTouristCount)),
                    "Set the target number of tourists for your city.\n" +
                    "**0 = disabled** (uses the game's vanilla formula).\n" +
                    "Higher values allow more tourists to spawn. The vanilla cap is around 2100. Range: 0-60,000.\n\n" +
                    "**Note:** Targets above 20,000 may impact game performance depending on your computer.\n" +
                    "Tourists also need good access to your city - make sure you have road, train, air, or ship outside connections, otherwise tourists won't be able to reach your city."
                },

                // Reset button
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ResetTourism)), "Reset to Default" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.ResetTourism)),
                    "Set the target tourist count back to 0 (disabled). Re-enables the game's vanilla tourist spawning."
                },
            };
        }

        public void Unload()
        {
        }
    }

    public class LocaleFR : IDictionarySource
    {
        private readonly Setting m_Setting;

        public LocaleFR(Setting setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Custom Tourism" },

                // Tabs
                { m_Setting.GetOptionTabLocaleID(Setting.TourismTab), "Tourisme" },

                // Groups
                { m_Setting.GetOptionGroupLocaleID(Setting.TourismGroup), "Parametres de tourisme" },
                { m_Setting.GetOptionGroupLocaleID(Setting.ResetGroup), "Reinitialiser" },

                // Target tourist count slider
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.TargetTouristCount)), "Nombre cible de touristes" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.TargetTouristCount)),
                    "Definit le nombre cible de touristes pour votre ville.\n" +
                    "**0 = desactive** (utilise la formule vanilla du jeu).\n" +
                    "Des valeurs plus elevees permettent l'arrivee de plus de touristes. Le plafond vanilla est d'environ 2100. Plage : 0-60000.\n\n" +
                    "**Note :** Au-dela de 20000, les performances peuvent etre affectees selon votre ordinateur.\n" +
                    "Les touristes ont aussi besoin d'un bon acces a votre ville - assurez-vous d'avoir des connexions exterieures par route, train, air ou bateau, sinon les touristes ne pourront pas atteindre votre ville."
                },

                // Reset button
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ResetTourism)), "Reinitialiser" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.ResetTourism)),
                    "Remet le nombre cible de touristes a 0 (desactive). Reactive le systeme de tourisme vanilla du jeu."
                },
            };
        }

        public void Unload()
        {
        }
    }
}
