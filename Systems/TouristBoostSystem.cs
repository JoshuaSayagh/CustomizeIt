using Colossal.Logging;
using Game;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CustomizeIt.Systems
{
    public partial class TouristBoostSystem : GameSystemBase
    {
        private static readonly ILog log = Mod.log;

        private CitySystem m_CitySystem;
        private CityStatisticsSystem m_CityStatisticsSystem;
        private SimulationSystem m_SimulationSystem;
        private EndFrameBarrier m_EndFrameBarrier;
        private TouristSpawnSystem m_VanillaSpawnSystem;
        private int m_LastDelta;

        private EntityQuery m_HouseholdPrefabQuery;
        private EntityQuery m_OutsideConnectionQuery;
        private EntityQuery m_TouristHouseholdQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_CitySystem = World.GetOrCreateSystemManaged<CitySystem>();
            m_CityStatisticsSystem = World.GetOrCreateSystemManaged<CityStatisticsSystem>();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            m_VanillaSpawnSystem = World.GetExistingSystemManaged<TouristSpawnSystem>();

            // Same queries the game's TouristSpawnSystem uses
            m_HouseholdPrefabQuery = GetEntityQuery(
                ComponentType.ReadOnly<ArchetypeData>(),
                ComponentType.ReadOnly<HouseholdData>());

            m_OutsideConnectionQuery = GetEntityQuery(
                ComponentType.ReadOnly<Game.Objects.OutsideConnection>(),
                ComponentType.Exclude<Game.Objects.ElectricityOutsideConnection>(),
                ComponentType.Exclude<Game.Objects.WaterPipeOutsideConnection>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Deleted>());

            m_TouristHouseholdQuery = GetEntityQuery(
                ComponentType.ReadOnly<TouristHousehold>(),
                ComponentType.ReadOnly<Household>(),
                ComponentType.Exclude<MovingAway>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            RequireForUpdate(m_HouseholdPrefabQuery);
            RequireForUpdate(m_OutsideConnectionQuery);

            log.Info("TouristBoostSystem created.");
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            return 64;
        }

        protected override void OnUpdate()
        {
            Setting setting = Mod.Setting;
            if (setting == null)
                return;

            int target = setting.TargetTouristCount;

            if (m_VanillaSpawnSystem != null)
                m_VanillaSpawnSystem.Enabled = target <= 0;

            if (target <= 0)
            {
                m_LastDelta = 0;
                return;
            }

            int statCurrent = m_CityStatisticsSystem.GetStatisticValue(
                Game.City.StatisticType.TouristCount);

            int predictedCurrent = statCurrent + m_LastDelta;
            int diff = target - predictedCurrent;
            int absDiff = math.abs(diff);

            int deadband = math.max(50, target / 50);

            int thisDelta = 0;

            if (diff > deadband)
            {
                int batch;
                if (absDiff > 2000)      batch = 90;
                else if (absDiff > 1000)   batch = 40;
                else if (absDiff > 200)    batch = 10;
                else                      batch = 3;

                int count = math.min(diff, batch);
                SpawnTourists(count);
                thisDelta = count;
            }
            else if (diff < -deadband)
            {
                int batch;
                if (absDiff > 1000)       batch = 50;
                else if (absDiff > 200)   batch = 20;
                else                      batch = 5;

                int count = math.min(absDiff, batch);
                DespawnTourists(count);
                thisDelta = -count;
            }

            m_LastDelta = thisDelta;
        }

        private void SpawnTourists(int count)
        {
            using var prefabEntities = m_HouseholdPrefabQuery.ToEntityArray(Allocator.Temp);
            using var archetypes = m_HouseholdPrefabQuery.ToComponentDataArray<ArchetypeData>(Allocator.Temp);
            using var outsideConnections = m_OutsideConnectionQuery.ToEntityArray(Allocator.Temp);

            if (prefabEntities.Length == 0 || outsideConnections.Length == 0)
                return;

            var commandBuffer = m_EndFrameBarrier.CreateCommandBuffer();
            var random = new Random((uint)(m_SimulationSystem.frameIndex + 1));

            for (int i = 0; i < count; i++)
            {
                int prefabIndex = random.NextInt(prefabEntities.Length);
                Entity prefab = prefabEntities[prefabIndex];
                EntityArchetype archetype = archetypes[prefabIndex].m_Archetype;

                Entity household = commandBuffer.CreateEntity(archetype);
                commandBuffer.SetComponent(household, new PrefabRef { m_Prefab = prefab });
                commandBuffer.SetComponent(household, new Household { m_Flags = HouseholdFlags.Tourist });
                commandBuffer.AddComponent(household, new TouristHousehold
                {
                    m_Hotel = Entity.Null,
                    m_LeavingTime = 0u
                });

                Entity oc = outsideConnections[random.NextInt(outsideConnections.Length)];
                commandBuffer.AddComponent(household, new CurrentBuilding
                {
                    m_CurrentBuilding = oc
                });
            }

            m_EndFrameBarrier.AddJobHandleForProducer(Dependency);
        }

        private void DespawnTourists(int count)
        {
            using var tourists = m_TouristHouseholdQuery.ToEntityArray(Allocator.Temp);

            if (tourists.Length == 0)
                return;

            var commandBuffer = m_EndFrameBarrier.CreateCommandBuffer();

            int toRemove = math.min(count, tourists.Length);
            for (int i = 0; i < toRemove; i++)
            {
                commandBuffer.AddComponent(tourists[tourists.Length - 1 - i], new MovingAway
                {
                    m_Reason = MoveAwayReason.TouristNoTarget
                });
            }

            m_EndFrameBarrier.AddJobHandleForProducer(Dependency);
        }

        protected override void OnDestroy()
        {
            if (m_VanillaSpawnSystem != null)
                m_VanillaSpawnSystem.Enabled = true;
            base.OnDestroy();
        }
    }
}
