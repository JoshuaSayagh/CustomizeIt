using System.Collections.Generic;
using Colossal.Logging;
using Game;
using Game.Prefabs;
using Unity.Entities;

namespace CustomizeIt.Systems
{
    /// <summary>
    /// Manages attractiveness overrides at the prefab layer by modifying
    /// AttractionData directly on prefab entities.
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

            m_Overrides[prefabEntity] = attractiveness;
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
            }

            m_Overrides.Remove(prefabEntity);
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
    }
}
