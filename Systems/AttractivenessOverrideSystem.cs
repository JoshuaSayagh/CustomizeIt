using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Prefabs;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace CustomizeIt.Systems
{
    /// <summary>
    /// Manages attractiveness overrides at the prefab layer by modifying
    /// AttractionData directly on prefab entities. Persists overrides via
    /// the game's ModSetting framework.
    /// </summary>
    public partial class AttractivenessOverrideSystem : GameSystemBase
    {
        private static readonly ILog log = Mod.log;
        private PrefabSystem m_PrefabSystem;

        private readonly Dictionary<Entity, int> m_Overrides = new Dictionary<Entity, int>();

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            Enabled = false;
            log.Info("AttractivenessOverrideSystem created.");
        }

        protected override void OnUpdate()
        {
        }

        /// <summary>
        /// Re-applies saved overrides after a game loads.
        /// </summary>
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);

            if (mode != GameMode.Game)
                return;

            LoadAndApplyOverrides();
        }

        /// <summary>
        /// Applies an attractiveness override to the given prefab entity.
        /// </summary>
        public void SetOverride(Entity prefabEntity, int attractiveness)
        {
            if (!EntityManager.HasComponent<AttractionData>(prefabEntity))
                return;

            EntityManager.SetComponentData(prefabEntity, new AttractionData
            {
                m_Attractiveness = attractiveness
            });

            if (!EntityManager.HasComponent<Updated>(prefabEntity))
                EntityManager.AddComponent<Updated>(prefabEntity);

            m_Overrides[prefabEntity] = attractiveness;
            SaveOverrides();
            log.Info($"Override set on prefab {prefabEntity.Index}: attractiveness = {attractiveness}");
        }

        /// <summary>
        /// Removes the override and restores the vanilla value from PrefabBase.
        /// </summary>
        public bool RemoveOverride(Entity prefabEntity)
        {
            if (!m_Overrides.ContainsKey(prefabEntity))
                return false;

            if (m_PrefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase)
                && prefabBase.TryGet<Attraction>(out var attraction))
            {
                EntityManager.SetComponentData(prefabEntity, new AttractionData
                {
                    m_Attractiveness = attraction.m_Attractiveness
                });

                if (!EntityManager.HasComponent<Updated>(prefabEntity))
                    EntityManager.AddComponent<Updated>(prefabEntity);
            }

            m_Overrides.Remove(prefabEntity);
            SaveOverrides();
            log.Info($"Override removed from prefab {prefabEntity.Index}, restored to vanilla.");
            return true;
        }

        /// <summary>
        /// Returns whether the given prefab has an active override.
        /// </summary>
        public bool TryGetOverride(Entity prefabEntity, out int attractiveness)
        {
            return m_Overrides.TryGetValue(prefabEntity, out attractiveness);
        }

        /// <summary>
        /// Returns the vanilla attractiveness from the PrefabBase authoring layer,
        /// falling back to AttractionData if the authoring component is unavailable.
        /// </summary>
        public int GetBaseAttractiveness(Entity prefabEntity)
        {
            if (m_PrefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefabBase)
                && prefabBase.TryGet<Attraction>(out var attraction))
            {
                return attraction.m_Attractiveness;
            }

            if (EntityManager.HasComponent<AttractionData>(prefabEntity))
            {
                return EntityManager.GetComponentData<AttractionData>(prefabEntity).m_Attractiveness;
            }

            return 0;
        }

        /// <summary>
        /// Persists current overrides to the mod's settings file via AssetDatabase.
        /// </summary>
        private void SaveOverrides()
        {
            Setting setting = Mod.Setting;
            if (setting == null)
                return;

            var names = new List<string>();
            var values = new List<int>();

            foreach (var kvp in m_Overrides)
            {
                if (m_PrefabSystem.TryGetPrefab(kvp.Key, out PrefabBase prefab))
                {
                    names.Add(prefab.name);
                    values.Add(kvp.Value);
                }
            }

            setting.OverridePrefabNames = names.ToArray();
            setting.OverrideValues = values.ToArray();
            setting.ApplyAndSave();
            log.Info($"Saved {names.Count} attractiveness override(s).");
        }

        /// <summary>
        /// Reads overrides from the mod's settings and applies them to prefab entities.
        /// </summary>
        private void LoadAndApplyOverrides()
        {
            Setting setting = Mod.Setting;
            if (setting == null)
                return;

            string[] names = setting.OverridePrefabNames;
            int[] values = setting.OverrideValues;

            if (names == null || values == null || names.Length == 0 || names.Length != values.Length)
                return;

            var savedOverrides = new Dictionary<string, int>();
            for (int i = 0; i < names.Length; i++)
                savedOverrides[names[i]] = values[i];

            EntityQuery prefabQuery = GetEntityQuery(ComponentType.ReadOnly<AttractionData>());
            using NativeArray<Entity> entities = prefabQuery.ToEntityArray(Allocator.Temp);

            int applied = 0;
            for (int i = 0; i < entities.Length; i++)
            {
                if (m_PrefabSystem.TryGetPrefab(entities[i], out PrefabBase prefab)
                    && savedOverrides.TryGetValue(prefab.name, out int attractiveness))
                {
                    EntityManager.SetComponentData(entities[i], new AttractionData
                    {
                        m_Attractiveness = attractiveness
                    });
                    m_Overrides[entities[i]] = attractiveness;
                    applied++;
                }
            }

            log.Info($"Loaded and applied {applied} attractiveness override(s) from settings.");
        }
    }
}
