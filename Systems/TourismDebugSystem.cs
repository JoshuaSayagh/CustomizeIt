using Colossal.Logging;
using Game;
using Game.City;
using Game.Simulation;
using Unity.Entities;

namespace CustomizeIt.Systems
{
    /// <summary>
    /// Periodically logs tourism-related values for debugging.
    /// </summary>
    public partial class TourismDebugSystem : GameSystemBase
    {
        private static readonly ILog log = Mod.log;

        private CitySystem m_CitySystem;
        private int m_TickCount;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_CitySystem = World.GetOrCreateSystemManaged<CitySystem>();
            log.Info("TourismDebugSystem created.");
        }

        protected override void OnUpdate()
        {
            m_TickCount++;

            Entity city = m_CitySystem.City;
            if (city == Entity.Null || !EntityManager.HasComponent<Tourism>(city))
                return;

            if (m_TickCount % 200 != 0)
                return;

            Tourism tourism = EntityManager.GetComponentData<Tourism>(city);

            // GetTargetTourists is now Harmony-patched — reflects override if active
            int effectiveTarget = TourismSystem.GetTargetTourists(tourism.m_Attractiveness);
            float spawnProb = TourismSystem.GetSpawnProbability(tourism.m_Attractiveness, tourism.m_CurrentTourists);
            int settingTarget = Mod.Setting?.TargetTouristCount ?? 0;

            long patchCalls = Patches.GetTargetTouristsPatch.CallCount;

            log.Info(
                $"[Tourism Debug] " +
                $"Attractiveness={tourism.m_Attractiveness}, " +
                $"SettingTarget={settingTarget}, " +
                $"EffectiveTarget={effectiveTarget}, " +
                $"CurrentTourists={tourism.m_CurrentTourists}, " +
                $"SpawnProb={spawnProb:F3}, " +
                $"Lodging=({tourism.m_Lodging.x}/{tourism.m_Lodging.y}), " +
                $"PatchCalls={patchCalls}"
            );
        }
    }
}
