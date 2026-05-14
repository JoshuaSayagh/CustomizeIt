using Colossal.Logging;
using System.Collections.Generic;
using Game;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Events;
using Game.Net;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;

namespace CustomizeIt.Systems
{
    // Runs before vanilla TouristFindTargetSystem. Vanilla pathfinds from the
    // tourist's OC with Pedestrian-only boundary methods, which works for Road
    // (the OC has car sub-lanes plus vanilla's spawned personal car) but dies
    // with TouristNoTarget on Train/Ship/Air. For Train and Ship we submit our
    // own pathfind with mode-matched boundary methods. For Air the pathfind
    // always returns null in practice — the only pedestrian anchor on an Air
    // OC entity is a sky-floating ConnectionLane that the seeker can step onto
    // but can't expand from — so we skip it and assign a hotel or attraction
    // directly at submission time.
    [UpdateBefore(typeof(TouristFindTargetSystem))]
    public partial class TouristTransitArrivalSystem : GameSystemBase
    {
        private static readonly ILog log = Mod.log;

        private EntityQuery m_SeekerQuery;
        private EntityQuery m_ResultQuery;
        private EntityQuery m_HotelQuery;
        private EntityQuery m_AttractionBuildingQuery;
        private PathfindSetupSystem m_PathfindSetupSystem;
        private AddMeetingSystem m_AddMeetingSystem;
        private readonly Dictionary<Entity, OutsideConnectionTransferType> m_PendingPaths = new Dictionary<Entity, OutsideConnectionTransferType>();
        private readonly List<Entity> m_PendingCleanup = new List<Entity>();
        private uint m_RngState = 0x9E3779B9u;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PathfindSetupSystem = World.GetOrCreateSystemManaged<PathfindSetupSystem>();
            m_AddMeetingSystem = World.GetOrCreateSystemManaged<AddMeetingSystem>();

            m_SeekerQuery = GetEntityQuery(
                ComponentType.ReadOnly<TouristHousehold>(),
                ComponentType.ReadOnly<LodgingSeeker>(),
                ComponentType.Exclude<MovingAway>(),
                ComponentType.Exclude<Target>(),
                ComponentType.Exclude<PathInformation>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            m_ResultQuery = GetEntityQuery(
                ComponentType.ReadOnly<TouristHousehold>(),
                ComponentType.ReadOnly<LodgingSeeker>(),
                ComponentType.ReadOnly<PathInformation>(),
                ComponentType.Exclude<MovingAway>(),
                ComponentType.Exclude<Target>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            m_HotelQuery = GetEntityQuery(
                ComponentType.ReadWrite<LodgingProvider>(),
                ComponentType.ReadWrite<Renter>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            // AttractionData is on the prefab, so we query all buildings and filter at pick time.
            m_AttractionBuildingQuery = GetEntityQuery(
                ComponentType.ReadOnly<Game.Buildings.Building>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            log.Info("TouristTransitArrivalSystem created.");
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase) => 16;

        protected override void OnUpdate()
        {
            using var households = m_SeekerQuery.ToEntityArray(Allocator.Temp);
            var citizenBufLookup = GetBufferLookup<HouseholdCitizen>(true);
            var currentBuildingLookup = GetComponentLookup<CurrentBuilding>(true);
            var prefabRefLookup = GetComponentLookup<PrefabRef>(true);
            var ocDataLookup = GetComponentLookup<OutsideConnectionData>(true);
            var ownedVehicleLookup = GetBufferLookup<OwnedVehicle>(true);
            var pathInfoLookup = GetComponentLookup<PathInformation>(true);
            var touristLookup = GetComponentLookup<TouristHousehold>(false);
            var lodgingProviderLookup = GetComponentLookup<LodgingProvider>(false);
            var renterLookup = GetBufferLookup<Renter>(false);

            CleanupPendingPathTags(ref pathInfoLookup, ref touristLookup);
            ProcessCompletedPathResults(ref pathInfoLookup);

            if (households.Length == 0)
                return;

            var queue = m_PathfindSetupSystem.GetQueue(this, 64);
            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            int submittedTrain = 0, submittedShip = 0;
            int airHotel = 0, airAttraction = 0, airNoTarget = 0;
            int skippedRoad = 0, skippedNoOrigin = 0;

            NativeList<Entity> attractions = default;
            NativeQueue<AddMeetingSystem.AddMeeting> meetingQueue = default;
            bool meetingsReady = false;

            for (int i = 0; i < households.Length; i++)
            {
                Entity household = households[i];

                if (!citizenBufLookup.HasBuffer(household)) { skippedNoOrigin++; continue; }
                var citizens = citizenBufLookup[household];
                Entity originEntity = Entity.Null;
                for (int j = 0; j < citizens.Length; j++)
                {
                    Entity citizen = citizens[j].m_Citizen;
                    if (currentBuildingLookup.HasComponent(citizen))
                        originEntity = currentBuildingLookup[citizen].m_CurrentBuilding;
                }
                if (originEntity == Entity.Null) { skippedNoOrigin++; continue; }

                if (!prefabRefLookup.HasComponent(originEntity)) { skippedRoad++; continue; }
                Entity prefab = prefabRefLookup[originEntity].m_Prefab;
                if (!ocDataLookup.HasComponent(prefab)) { skippedRoad++; continue; }

                var ocType = ocDataLookup[prefab].m_Type;
                if ((ocType & OutsideConnectionTransferType.Road) != 0) { skippedRoad++; continue; }

                bool isTrain = (ocType & OutsideConnectionTransferType.Train) != 0;
                bool isAir = (ocType & OutsideConnectionTransferType.Air) != 0;
                bool isShip = (ocType & OutsideConnectionTransferType.Ship) != 0;
                if (!isTrain && !isAir && !isShip) { skippedRoad++; continue; }

                if (isAir)
                {
                    if (!meetingsReady)
                    {
                        attractions = CollectAttractions(ref prefabRefLookup);
                        meetingQueue = m_AddMeetingSystem.GetMeetingQueue(out var meetingDeps);
                        meetingDeps.Complete();
                        meetingsReady = true;
                    }

                    bool preferHotel = (NextRoll() & 1u) == 0u;
                    bool hotelOk = false, attractionOk = false;
                    if (preferHotel)
                    {
                        hotelOk = TryAssignHotel(household, ref touristLookup, ref lodgingProviderLookup, ref renterLookup, ecb);
                        if (!hotelOk)
                            attractionOk = TryAssignAttraction(household, attractions, meetingQueue, ecb);
                    }
                    else
                    {
                        attractionOk = TryAssignAttraction(household, attractions, meetingQueue, ecb);
                        if (!attractionOk)
                            hotelOk = TryAssignHotel(household, ref touristLookup, ref lodgingProviderLookup, ref renterLookup, ecb);
                    }

                    if (hotelOk) airHotel++;
                    else if (attractionOk) airAttraction++;
                    else airNoTarget++;
                    continue;
                }

                var parameters = new PathfindParameters
                {
                    m_MaxSpeed = 277.77777f,
                    m_WalkSpeed = 1.6666667f,
                    m_Weights = new PathfindWeights(0.1f, 0.1f, 0.1f, 0.2f),
                    m_Methods = PathMethod.Pedestrian | PathMethod.PublicTransportDay
                              | PathMethod.PublicTransportNight | PathMethod.Taxi
                              | PathMethod.Track | PathMethod.Road | PathMethod.MediumRoad
                              | PathMethod.Offroad,
                    m_TaxiIgnoredRules = VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults(),
                    m_PathfindFlags = PathfindFlags.IgnoreFlow | PathfindFlags.Simplified | PathfindFlags.IgnorePath
                };

                var origin = new SetupQueueTarget
                {
                    m_Type = SetupTargetType.CurrentLocation,
                    m_Entity = originEntity
                };

                if (isTrain)
                {
                    origin.m_Methods = PathMethod.Pedestrian | PathMethod.Track;
                    origin.m_TrackTypes = TrackTypes.Train;
                }
                else
                {
                    // Ship SpawnLocations use Road/Cargo/Offroad ConnectionType with
                    // RoadTypes.Watercraft. Covering all three lets the seeker bridge in.
                    origin.m_Methods = PathMethod.Pedestrian | PathMethod.Road
                                     | PathMethod.MediumRoad | PathMethod.Offroad;
                    origin.m_RoadTypes = RoadTypes.Watercraft;
                }

                var destination = new SetupQueueTarget
                {
                    m_Type = SetupTargetType.TouristFindTarget,
                    m_Methods = PathMethod.Pedestrian,
                    m_Entity = household
                };
                PathUtils.UpdateOwnedVehicleMethods(household, ref ownedVehicleLookup, ref parameters, ref origin, ref destination);

                m_PendingPaths[household] = ocType;
                queue.Enqueue(new SetupQueueItem(household, parameters, origin, destination));
                ecb.AddComponent(household, new PathInformation { m_State = PathFlags.Pending });
                if (isTrain) submittedTrain++;
                else submittedShip++;
            }

            ecb.Playback(EntityManager);
            m_PathfindSetupSystem.AddQueueWriter(Dependency);
            if (attractions.IsCreated) attractions.Dispose();

            int totalSubmitted = submittedTrain + submittedShip;
            int totalAir = airHotel + airAttraction + airNoTarget;
            if (totalSubmitted > 0 || totalAir > 0 || skippedRoad > 0)
            {
                log.Info(
                    $"[CT-TTA] eligible={households.Length} " +
                    $"submitted[Train={submittedTrain} Ship={submittedShip}] " +
                    $"airAssigned[hotel={airHotel} attraction={airAttraction} none={airNoTarget}] " +
                    $"skipped[Road={skippedRoad} noOrigin={skippedNoOrigin}]");
            }
        }

        private void ProcessCompletedPathResults(ref ComponentLookup<PathInformation> pathInfoLookup)
        {
            using var results = m_ResultQuery.ToEntityArray(Allocator.Temp);
            if (results.Length == 0)
                return;

            int trainOk = 0, trainFail = 0;
            int shipOk = 0, shipFail = 0;

            for (int i = 0; i < results.Length; i++)
            {
                Entity household = results[i];
                if (!m_PendingPaths.TryGetValue(household, out var type))
                    continue;

                PathInformation path = pathInfoLookup[household];
                if ((path.m_State & PathFlags.Pending) != 0)
                    continue;

                bool ok = path.m_Destination != Entity.Null;
                if ((type & OutsideConnectionTransferType.Train) != 0)
                    { if (ok) trainOk++; else trainFail++; }
                else if ((type & OutsideConnectionTransferType.Ship) != 0)
                    { if (ok) shipOk++; else shipFail++; }

                m_PendingPaths.Remove(household);
            }

            int total = trainOk + trainFail + shipOk + shipFail;
            if (total > 0)
            {
                log.Info(
                    $"[CT-TTA] results total={total} " +
                    $"ok[Train={trainOk} Ship={shipOk}] " +
                    $"fail[Train={trainFail} Ship={shipFail}] " +
                    $"pendingTracked={m_PendingPaths.Count}");
            }
        }

        private NativeList<Entity> CollectAttractions(ref ComponentLookup<PrefabRef> prefabRefLookup)
        {
            var attractionDataLookup = GetComponentLookup<AttractionData>(true);
            using var buildings = m_AttractionBuildingQuery.ToEntityArray(Allocator.Temp);
            var list = new NativeList<Entity>(64, Allocator.Temp);
            for (int i = 0; i < buildings.Length; i++)
            {
                Entity building = buildings[i];
                if (!prefabRefLookup.HasComponent(building))
                    continue;
                Entity prefab = prefabRefLookup[building].m_Prefab;
                if (attractionDataLookup.HasComponent(prefab))
                    list.Add(building);
            }
            return list;
        }

        private uint NextRoll()
        {
            uint x = m_RngState;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            m_RngState = x;
            return x;
        }

        private bool TryAssignHotel(
            Entity household,
            ref ComponentLookup<TouristHousehold> touristLookup,
            ref ComponentLookup<LodgingProvider> lodgingProviderLookup,
            ref BufferLookup<Renter> renterLookup,
            EntityCommandBuffer ecb)
        {
            if (!touristLookup.HasComponent(household))
                return false;

            using var hotels = m_HotelQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < hotels.Length; i++)
            {
                Entity hotel = hotels[i];
                if (!lodgingProviderLookup.HasComponent(hotel) || !renterLookup.HasBuffer(hotel))
                    continue;

                LodgingProvider provider = lodgingProviderLookup[hotel];
                if (provider.m_FreeRooms <= 0)
                    continue;

                provider.m_FreeRooms--;
                lodgingProviderLookup[hotel] = provider;
                renterLookup[hotel].Add(new Renter { m_Renter = household });

                TouristHousehold tourist = touristLookup[household];
                tourist.m_Hotel = hotel;
                touristLookup[household] = tourist;

                ecb.RemoveComponent<LodgingSeeker>(household);
                ecb.AddComponent(household, new Target(hotel));
                return true;
            }
            return false;
        }

        private bool TryAssignAttraction(
            Entity household,
            NativeList<Entity> attractions,
            NativeQueue<AddMeetingSystem.AddMeeting> meetingQueue,
            EntityCommandBuffer ecb)
        {
            if (attractions.Length == 0)
                return false;

            Entity attraction = attractions[(int)(NextRoll() % (uint)attractions.Length)];

            meetingQueue.Enqueue(new AddMeetingSystem.AddMeeting
            {
                m_Household = household,
                m_Type = LeisureType.Attractions
            });

            ecb.RemoveComponent<LodgingSeeker>(household);
            ecb.AddComponent(household, new Target(attraction));
            return true;
        }

        private void CleanupPendingPathTags(
            ref ComponentLookup<PathInformation> pathInfoLookup,
            ref ComponentLookup<TouristHousehold> touristLookup)
        {
            if (m_PendingPaths.Count == 0)
                return;

            m_PendingCleanup.Clear();
            foreach (var pair in m_PendingPaths)
            {
                Entity household = pair.Key;
                if (!touristLookup.HasComponent(household) || !pathInfoLookup.HasComponent(household))
                    m_PendingCleanup.Add(household);
            }

            if (m_PendingCleanup.Count == 0)
                return;

            for (int i = 0; i < m_PendingCleanup.Count; i++)
                m_PendingPaths.Remove(m_PendingCleanup[i]);

            log.Info($"[CT-TTA] pending cleanup removed={m_PendingCleanup.Count} remaining={m_PendingPaths.Count}");
        }
    }
}
