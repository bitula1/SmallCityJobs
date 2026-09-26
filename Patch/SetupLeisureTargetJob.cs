using Game.Agents;
using Game.Buildings;
using Game.Companies;
using Game.Economy;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst.Intrinsics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace BitulaMod {
    [BurstCompile]
    public struct SetupLeisureTargetJob : IJobChunk {

        // Token: 0x060078E6 RID: 30950 RVA: 0x004494E4 File Offset: 0x004476E4
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
            NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
            NativeArray<ServiceAvailable> nativeArray2 = chunk.GetNativeArray<ServiceAvailable>(ref this.m_ServiceAvailableType);
            NativeArray<PrefabRef> nativeArray3 = chunk.GetNativeArray<PrefabRef>(ref this.m_PrefabType);
            for (int i = 0; i < this.m_SetupData.Length; i++) {
                Entity entity;
                PathfindTargetSeeker<PathfindSetupBuffer> pathfindTargetSeeker;
                this.m_SetupData.GetItem(i, out entity, out pathfindTargetSeeker);
                LeisureType value = (LeisureType)pathfindTargetSeeker.m_SetupQueueTarget.m_Value;
                float value2 = pathfindTargetSeeker.m_SetupQueueTarget.m_Value2;
                for (int j = 0; j < nativeArray.Length; j++) {
                    Entity entity2 = nativeArray[j];
                    if (m_CompanyDatas.HasComponent(entity2) &&
                        !m_PropertyRenters.HasComponent(entity2)) {
                        continue;
                    }
                    if (!this.m_BuildingDatas.HasComponent(entity2) || !BuildingUtils.CheckOption(this.m_BuildingDatas[entity2], BuildingOption.Inactive)) {
                        Entity prefab = nativeArray3[j].m_Prefab;
                        if (this.m_LeisureProviderDatas.HasComponent(prefab)) {
                            LeisureProviderData leisureProviderData = this.m_LeisureProviderDatas[prefab];
                            float num = 0f;
                            if (value == leisureProviderData.m_LeisureType) {                              
                                m_SmallCityJobs.SetDesiredProviderFound(entity);
                                if ((value == LeisureType.Commercial || value == LeisureType.Meals) && nativeArray2.Length > 0 && this.m_ServiceDatas.HasComponent(prefab)) {
                                    int num2 = nativeArray2[j].m_ServiceAvailable;
                                    if ((float)num2 < value2) {
                                        goto IL_01B4;
                                    }
                                    if (this.m_IndustrialProcessDatas.HasComponent(prefab)) {
                                        IndustrialProcessData industrialProcessData = this.m_IndustrialProcessDatas[prefab];
                                        if (industrialProcessData.m_Output.m_Resource != Resource.NoResource) {
                                            num2 = math.min(num2, EconomyUtils.GetResources(industrialProcessData.m_Output.m_Resource, this.m_Resources[entity2]));
                                            num = 1000f * (1f - math.saturate(1f * (float)num2 / (float)this.m_ServiceDatas[prefab].m_MaxService) * 2f);
                                        }
                                    }
                                }
                                m_SmallCityJobs.SetAvailableProviderFound(entity);
                                pathfindTargetSeeker.FindTargets(entity2, num);
                            }
                        }
                    }
                IL_01B4:;
                }
            }
        }
        // Token: 0x0400C246 RID: 49734
        [ReadOnly]
        public EntityTypeHandle m_EntityType;

        // Token: 0x0400C247 RID: 49735
        [ReadOnly]
        public ComponentTypeHandle<ServiceAvailable> m_ServiceAvailableType;

        // Token: 0x0400C248 RID: 49736
        [ReadOnly]
        public ComponentTypeHandle<PrefabRef> m_PrefabType;

        // Token: 0x0400C249 RID: 49737
        [ReadOnly]
        public ComponentLookup<IndustrialProcessData> m_IndustrialProcessDatas;

        // Token: 0x0400C24A RID: 49738
        [ReadOnly]
        public ComponentLookup<LeisureProviderData> m_LeisureProviderDatas;

        // Token: 0x0400C24B RID: 49739
        [ReadOnly]
        public ComponentLookup<ResourceData> m_ResourceDatas;

        // Token: 0x0400C24C RID: 49740
        [ReadOnly]
        public ComponentLookup<ServiceCompanyData> m_ServiceDatas;

        // Token: 0x0400C24D RID: 49741
        [ReadOnly]
        public ComponentLookup<Building> m_BuildingDatas;

        // Token: 0x0400C24E RID: 49742
        [ReadOnly]
        public BufferLookup<Game.Economy.Resources> m_Resources;

        // Token: 0x0400C24F RID: 49743
        [ReadOnly]
        public ResourcePrefabs m_ResourcePrefabs;

        // Token: 0x0400C250 RID: 49744
        public PathfindSetupSystem.SetupData m_SetupData;

        // Token: 0x0400C251 RID: 49745
        public int m_LeisureSystemUpdateInterval;

        public SmallCityJobs m_SmallCityJobs;
        [ReadOnly]
        public ComponentLookup<PropertyRenter> m_PropertyRenters;

        [ReadOnly]
        public ComponentLookup<CompanyData> m_CompanyDatas;
    }
}
