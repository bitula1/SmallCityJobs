using Game;
using Game.Citizens;
using Game.Common;
using Game.Pathfind;
using Game.Objects;
using Game.Simulation;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Game.UI;
using System;
using Game.Prefabs;
using Game.SceneFlow;
using System.Reflection;

namespace BitulaMod {
    public partial class CitizenTravelDebugSystem : GameSystemBase {
        private EntityQuery m_FollowedCitizens;
        private SimulationSystem m_SimulationSystem;
        private NameSystem m_NameSystem;
        private static readonly FieldInfo m_NameArgsField = typeof(NameSystem.Name).GetField("m_NameArgs", BindingFlags.NonPublic | BindingFlags.Instance);

        // Log approximately every 1.5 simulation hours.
        public override int GetUpdateInterval(SystemUpdatePhase phase) {
            return 1024;
        }

        protected override void OnCreate() {
            base.OnCreate();

            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();

            m_FollowedCitizens = GetEntityQuery(
                ComponentType.ReadOnly<Citizen>(),
                ComponentType.ReadOnly<Followed>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>()
            );
            m_NameSystem = World.GetOrCreateSystemManaged<NameSystem>();
            RequireForUpdate(m_FollowedCitizens);
        }

        protected override void OnUpdate() {
            using (NativeArray<Entity> citizens =
                m_FollowedCitizens.ToEntityArray(Allocator.Temp)) {

                foreach (Entity citizen in citizens) {
                    string name = GetFullName(citizen);

                    Mod.log.Info($"Citizen={citizen} Name='{name}'");

                    if (!string.Equals(name, "Booker Raleigh", StringComparison.OrdinalIgnoreCase))
                        continue;

                    //DebugCitizenTravel(citizen);
                    //DebugCitizenMeeting(citizen);
                }
            }
        }

        private void DebugCitizenMeeting(Entity citizen) {
            if (!EntityManager.HasComponent<AttendingMeeting>(citizen)) {
                Mod.log.Info("AttendingMeeting=Missing");
                return;
            }

            AttendingMeeting attending = EntityManager.GetComponentData<AttendingMeeting>(citizen);
            Entity meeting = attending.m_Meeting;

            Mod.log.Info($"Meeting={meeting}");

            if (meeting == Entity.Null || !EntityManager.Exists(meeting)) {
                Mod.log.Warn($"Meeting entity invalid: {meeting}");
                return;
            }

            if (!EntityManager.HasComponent<CoordinatedMeeting>(meeting)) {
                Mod.log.Info("CoordinatedMeeting=Missing");
                return;
            }

            CoordinatedMeeting coordinated = EntityManager.GetComponentData<CoordinatedMeeting>(meeting);

            Mod.log.Info($"Meeting Status={coordinated.m_Status}");
            Mod.log.Info($"Meeting Phase={coordinated.m_Phase}");
            Mod.log.Info($"Meeting Target={coordinated.m_Target}");

            if (EntityManager.HasBuffer<CoordinatedMeetingAttendee>(meeting)) {
                DynamicBuffer<CoordinatedMeetingAttendee> attendees =
                    EntityManager.GetBuffer<CoordinatedMeetingAttendee>(meeting);

                Mod.log.Info($"Meeting Attendees={attendees.Length}");

                for (int i = 0; i < attendees.Length; i++) {
                    Entity attendee = attendees[i].m_Attendee;

                    if (!EntityManager.Exists(attendee)) {
                        Mod.log.Warn($"Attendee[{i}]={attendee} Entity missing");
                        continue;
                    }

                    string name = GetFullName(attendee);

                    Entity location = Entity.Null;

                    if (EntityManager.HasComponent<CurrentBuilding>(attendee)) {
                        location = EntityManager.GetComponentData<CurrentBuilding>(attendee).m_CurrentBuilding;
                    }

                    bool atTarget = location == coordinated.m_Target;
                    bool arrived = EntityManager.HasComponent<Arrived>(attendee);
                    bool hasMeeting = EntityManager.HasComponent<AttendingMeeting>(attendee);

                    string purpose = EntityManager.HasComponent<TravelPurpose>(attendee)
                        ? EntityManager.GetComponentData<TravelPurpose>(attendee).m_Purpose.ToString()
                        : "Missing";

                    Mod.log.Info($"Attendee[{i}]={attendee} Name='{name}' Location={location} AtTarget={atTarget} Arrived={arrived} AttendingMeeting={hasMeeting} Purpose={purpose}");

                    if (EntityManager.HasBuffer<TripNeeded>(attendee)) {
                        DynamicBuffer<TripNeeded> trips = EntityManager.GetBuffer<TripNeeded>(attendee);

                        Mod.log.Info($"Attendee[{i}] TripCount={trips.Length}");

                        for (int j = 0; j < trips.Length; j++) {
                            Mod.log.Info($"Attendee[{i}] Trip[{j}] Purpose={trips[j].m_Purpose} Target={trips[j].m_TargetAgent}");
                        }
                    }
                }                
            }

            if (EntityManager.HasComponent<PrefabRef>(meeting)) {
                Entity prefab = EntityManager.GetComponentData<PrefabRef>(meeting).m_Prefab;

                if (EntityManager.HasBuffer<HaveCoordinatedMeetingData>(prefab)) {
                    DynamicBuffer<HaveCoordinatedMeetingData> phases =
                        EntityManager.GetBuffer<HaveCoordinatedMeetingData>(prefab);

                    int phase = (int)coordinated.m_Phase;

                    if (phase >= 0 && phase < phases.Length) {
                        Mod.log.Info($"Meeting Purpose={phases[phase].m_TravelPurpose.m_Purpose}");
                    }
                }
            }
            /*if (GetFullName(citizen) == "Booker Raleigh" &&
    coordinated.m_Status == MeetingStatus.Traveling) {

                coordinated.m_Status = MeetingStatus.Done;
                EntityManager.SetComponentData(meeting, coordinated);

                Mod.log.Info($"TEST: Marked Booker's meeting {meeting} as Done");
            }*/
        }

        private string GetFullName(Entity citizen) {
            if (m_NameSystem.TryGetCustomName(citizen, out string customName))
                return customName;

            NameSystem.Name name = m_NameSystem.GetName(citizen);

            string[] args = (string[])m_NameArgsField.GetValue(name);

            if (args == null || args.Length < 4)
                return m_NameSystem.GetRenderedLabelName(citizen);

            string firstName = GetLocalizedName(args[1]);
            string lastName = GetLocalizedName(args[3]);

            return $"{firstName} {lastName}".Trim();
        }

        private string GetLocalizedName(string id) {
            if (string.IsNullOrEmpty(id))
                return "";

            if (GameManager.instance.localizationManager.activeDictionary.TryGetValue(id, out string value))
                return value;

            return id;
        }

        private void DebugEntityComponents(Entity entity, string label) {
            if (!EntityManager.Exists(entity)) {
                Mod.log.Info($"{label} Entity does not exist: {entity}");
                return;
            }

            Mod.log.Info($"=== {label} Components: {entity} ===");

            using (NativeArray<ComponentType> types =
                EntityManager.GetComponentTypes(entity, Allocator.Temp)) {

                foreach (ComponentType type in types) {
                    System.Type managedType = TypeManager.GetType(type.TypeIndex);

                    string typeName = managedType?.FullName ?? "Unknown";

                    Mod.log.Info($"{label} Component={typeName}");
                }
            }
        }

        private void DebugCitizenTravel(Entity citizen) {
            if (!EntityManager.Exists(citizen))
                return;

            uint frame = m_SimulationSystem.frameIndex;

            Mod.log.Info($"=== CitizenTravelDebug citizen={citizen} frame={frame} day={frame / 262144u} ===");

            // Current building
            if (EntityManager.HasComponent<CurrentBuilding>(citizen)) {
                var building = EntityManager.GetComponentData<CurrentBuilding>(citizen);

                Mod.log.Info($"CurrentBuilding={building.m_CurrentBuilding}");
            } else {
                Mod.log.Info("CurrentBuilding=Missing");
            }

            // Travel purpose
            if (EntityManager.HasComponent<TravelPurpose>(citizen)) {
                var purpose = EntityManager.GetComponentData<TravelPurpose>(citizen);

                Mod.log.Info($"TravelPurpose={purpose.m_Purpose}");
            } else {
                Mod.log.Info("TravelPurpose=Missing");
            }

            // Pending trips
            if (EntityManager.HasBuffer<TripNeeded>(citizen)) {
                var trips = EntityManager.GetBuffer<TripNeeded>(citizen);

                Mod.log.Info($"TripNeeded count={trips.Length}");

                for (int i = 0; i < trips.Length; i++) {
                    var trip = trips[i];

                    Mod.log.Info(
                        $"TripNeeded[{i}] Purpose={trip.m_Purpose} Target={trip.m_TargetAgent}"
                    );
                }
            } else {
                Mod.log.Info("TripNeeded=Missing");
            }

            // Citizen's own movement and pathfinding components
            DebugMovementEntity(citizen, "Citizen");

            // Current transport / moving creature
            if (EntityManager.HasComponent<CurrentTransport>(citizen)) {
                var transport = EntityManager.GetComponentData<CurrentTransport>(citizen);

                Entity movingEntity = transport.m_CurrentTransport;

                Mod.log.Info($"CurrentTransport={movingEntity}");

                if (movingEntity != Entity.Null && EntityManager.Exists(movingEntity)) {
                    DebugMovementEntity(movingEntity, "Transport");
                } else {
                    Mod.log.Info("Transport entity unavailable");
                }
            } else {
                Mod.log.Info("CurrentTransport=Missing");
            }
            DebugEntityComponents(citizen, "Citizen");
            if (EntityManager.HasComponent<CurrentBuilding>(citizen)) {
                Entity building = EntityManager
                    .GetComponentData<CurrentBuilding>(citizen)
                    .m_CurrentBuilding;

                DebugEntityComponents(building, "CurrentBuilding");
            }

        }

        private void DebugMovementEntity(Entity entity, string label) {
            // Position
            if (EntityManager.HasComponent<Transform>(entity)) {
                var transform = EntityManager.GetComponentData<Transform>(entity);

                Mod.log.Info(
                    $"{label} PositionX={transform.m_Position.x} " +
                    $"PositionY={transform.m_Position.y} " +
                    $"PositionZ={transform.m_Position.z}"
                );
            } else {
                Mod.log.Info($"{label} Transform=Missing");
            }

            // Destination
            if (EntityManager.HasComponent<Target>(entity)) {
                var target = EntityManager.GetComponentData<Target>(entity);

                Mod.log.Info($"{label} Target={target.m_Target}");
            } else {
                Mod.log.Info($"{label} Target=Missing");
            }

            // Pathfinding state
            if (EntityManager.HasComponent<PathOwner>(entity)) {
                var path = EntityManager.GetComponentData<PathOwner>(entity);

                Mod.log.Info($"{label} PathState={path.m_State}");
            } else {
                Mod.log.Info($"{label} PathOwner=Missing");
            }

            // Path elements
            if (EntityManager.HasBuffer<PathElement>(entity)) {
                var elements = EntityManager.GetBuffer<PathElement>(entity);

                Mod.log.Info($"{label} PathElementCount={elements.Length}");
            } else {
                Mod.log.Info($"{label} PathElement=Missing");
            }
        }
    }
}