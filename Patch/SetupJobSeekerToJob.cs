using Game.Buildings;
using Game.City;
using Game.Companies;
using Game.Pathfind;
using Game.Simulation;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace BitulaMod {
    [BurstCompile]
    public struct SetupJobSeekerToJob : IJobChunk {
        [ReadOnly]
        public EntityTypeHandle m_EntityType;

        [ReadOnly]
        public ComponentTypeHandle<FreeWorkplaces> m_FreeWorkplaceType;

        [ReadOnly]
        public ComponentTypeHandle<WorkProvider> m_WorkProviderType;

        [ReadOnly]
        public ComponentTypeHandle<CityServiceUpkeep> m_CityServiceType;

        [ReadOnly]
        public ComponentLookup<Game.Objects.OutsideConnection> m_OutsideConnections;

        public PathfindSetupSystem.SetupData m_SetupData;

        public void Execute(
            in ArchetypeChunk chunk,
            int unfilteredChunkIndex,
            bool useEnabledMask,
            in v128 chunkEnabledMask) {

            NativeArray<Entity> nativeArray = chunk.GetNativeArray(m_EntityType);
            NativeArray<FreeWorkplaces> nativeArray2 = chunk.GetNativeArray(ref m_FreeWorkplaceType);
            NativeArray<WorkProvider> nativeArray3 = chunk.GetNativeArray(ref m_WorkProviderType);

            float num = chunk.Has(ref m_CityServiceType) ? -4000f : 0f;

            for (int i = 0; i < m_SetupData.Length; i++) {
                Entity entity;
                PathfindTargetSeeker<PathfindSetupBuffer> pathfindTargetSeeker;

                m_SetupData.GetItem(i, out entity, out pathfindTargetSeeker);

                Unity.Mathematics.Random random =
                    pathfindTargetSeeker.m_RandomSeed.GetRandom(unfilteredChunkIndex);

                int num2 = pathfindTargetSeeker.m_SetupQueueTarget.m_Value % 5;
                int num3 = pathfindTargetSeeker.m_SetupQueueTarget.m_Value / 5 - 1;
                float value = pathfindTargetSeeker.m_SetupQueueTarget.m_Value2;
                SetupTargetFlags flags = pathfindTargetSeeker.m_SetupQueueTarget.m_Flags;
                Entity requestedWorkplace = pathfindTargetSeeker.m_SetupQueueTarget.m_Entity;

                for (int j = 0; j < nativeArray.Length; j++) {
                    Entity entity2 = nativeArray[j];
                    if (requestedWorkplace != Entity.Null && entity2 != requestedWorkplace)
                        continue;
                    FreeWorkplaces freeWorkplaces = nativeArray2[j];


                    if ((flags & SetupTargetFlags.Export) != SetupTargetFlags.None) {
                        if (freeWorkplaces.GetFree(num2) > 0 &&
                            !m_OutsideConnections.HasComponent(entity2)) {

                            pathfindTargetSeeker.FindTargets(entity2, 2000f);
                        }
                    } else if ((flags & SetupTargetFlags.Import) == SetupTargetFlags.None ||
                               !m_OutsideConnections.HasComponent(entity2)) {

                        int lowestFree = (int)freeWorkplaces.GetLowestFree();

                        if (num2 >= lowestFree && num2 >= num3) {
                            int bestFor = freeWorkplaces.GetBestFor(num2);
                            int num4 = nativeArray3.Length > 0
                                ? nativeArray3[j].m_MaxWorkers
                                : 0;

                            if (freeWorkplaces.Count > 0 && num4 > 0) {
                                float num5 = (float)freeWorkplaces.Count / num4;
                                int num6 = random.NextInt(4000);
                                int num7 = m_OutsideConnections.HasComponent(entity2)
                                    ? 8000
                                    : -4000;

                                pathfindTargetSeeker.FindTargets(
                                    entity2,
                                    6000f * (1f - num5)
                                    + math.max(0f, 2f - value) * 4000f * (num2 - bestFor)
                                    + num
                                    + num6
                                    + num7);
                            }
                        }
                    }
                }
            }
        }
    }
}