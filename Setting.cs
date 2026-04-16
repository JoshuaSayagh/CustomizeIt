using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using System.Collections.Generic;

namespace CustomizeIt
{
    [FileLocation(nameof(CustomizeIt))]
    public class Setting : ModSetting
    {
        public Setting(IMod mod) : base(mod)
        {
        }

        /// <summary>
        /// Persisted prefab names for attractiveness overrides (parallel to OverrideValues).
        /// </summary>
        public string[] OverridePrefabNames { get; set; } = new string[0];

        /// <summary>
        /// Persisted attractiveness values (parallel to OverridePrefabNames).
        /// </summary>
        public int[] OverrideValues { get; set; } = new int[0];

        public override void SetDefaults()
        {
            OverridePrefabNames = new string[0];
            OverrideValues = new int[0];
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
                { m_Setting.GetSettingsLocaleID(), "Customize It" },
            };
        }

        public void Unload()
        {
        }
    }
}
