using Game;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Unity.Collections;
using Unity.Entities;

namespace BitulaMod {
    public partial class ModDebugSystem : GameSystemBase {
        private EntityQuery m_CitizenQuery;

        protected override void OnCreate() {
            base.OnCreate();

            m_CitizenQuery = GetEntityQuery(
                ComponentType.ReadOnly<Citizen>()
            );
        }

        protected override void OnUpdate() {


            SmallCityJobs customEventData =
                SmallCityJobs.Create(ref CheckedStateRef);


            NativeArray<Entity> citizens =
    m_CitizenQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < citizens.Length; i++) {
                Entity citizenEntity = citizens[i];

                if (!customEventData.IsFollowed(citizenEntity))
                    continue;

                if (!EntityManager.HasComponent<Worker>(citizenEntity)) {
                    customEventData.AddParameter(8);
                    customEventData.Send(
                        citizenEntity,
                        CustomEventType.DebugMessage);

                    continue;
                }

                Worker worker =
                    EntityManager.GetComponentData<Worker>(citizenEntity);

                Entity workplace = worker.m_Workplace;

                

                // Worker no longer has a workplace reference.
                if (workplace == Entity.Null) {
                    customEventData.AddParameter(1);
                    customEventData.AddParameter(0);
                    customEventData.Send(
                        citizenEntity,
                        CustomEventType.DebugMessage);

                    continue;
                }

                // Workplace/company entity itself no longer exists.
                if (!EntityManager.Exists(workplace)) {
                    customEventData.AddParameter(2);
                    customEventData.AddParameter(workplace.Index);
                    customEventData.Send(
                        citizenEntity,
                        CustomEventType.DebugMessage);

                    continue;
                }

                // Workplace/company entity has been marked Deleted.
                if (EntityManager.HasComponent<Deleted>(workplace)) {
                    customEventData.AddParameter(3);
                    customEventData.AddParameter(workplace.Index);
                    customEventData.Send(
                        citizenEntity,
                        CustomEventType.DebugMessage);

                    continue;
                }

                // Workplace/company no longer has a property.
                if (!EntityManager.HasComponent<PropertyRenter>(workplace)) {
                    customEventData.AddParameter(7);
                    customEventData.AddParameter(workplace.Index);
                    customEventData.Send(
                        citizenEntity,
                        CustomEventType.DebugMessage);

                    continue;
                }

                PropertyRenter propertyRenter =
                    EntityManager.GetComponentData<PropertyRenter>(workplace);

                Entity property = propertyRenter.m_Property;

                // Employer still exists, but no longer points to a property.
                if (property == Entity.Null) {
                    customEventData.AddParameter(4);
                    customEventData.AddParameter(workplace.Index);
                    customEventData.Send(
                        citizenEntity,
                        CustomEventType.DebugMessage);
                }
                // Property/building entity no longer exists.
                else if (!EntityManager.Exists(property)) {
                    customEventData.AddParameter(5);
                    customEventData.AddParameter(property.Index);
                    customEventData.Send(
                        citizenEntity,
                        CustomEventType.DebugMessage);
                }
                // Property/building exists but has been marked Deleted.
                else if (EntityManager.HasComponent<Deleted>(property)) {
                    customEventData.AddParameter(6);
                    customEventData.AddParameter(property.Index);
                    customEventData.Send(
                        citizenEntity,
                        CustomEventType.DebugMessage);
                }
            }

            citizens.Dispose();
        }
    }
}