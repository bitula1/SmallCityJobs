using Game.Buildings;
using Game.Citizens;
using Game.Companies;
using Game.Economy;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Vehicles;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Game.Common;
using Unity.Mathematics;
using BitulaMod.SmallCity;
using Unity.Burst;
using UnityEngine.Rendering;

namespace BitulaMod.Patch {
    [BurstCompile]
    public struct SetupResourceSellerJob : IJobChunk {
        // Token: 0x06007A5B RID: 31323 RVA: 0x0045F564 File Offset: 0x0045D764
        public SmallCity<SCShopping> m_SmallCityShopping;
        public EntityCommandBuffer.ParallelWriter m_CommandBuffer;
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
            m_SmallCityShopping.SetCommandBuffer(m_CommandBuffer, unfilteredChunkIndex);
            NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
            BufferAccessor<StorageTransferRequest> bufferAccessor = chunk.GetBufferAccessor<StorageTransferRequest>(ref this.m_StorageTransferRequestType);
            BufferAccessor<InstalledUpgrade> bufferAccessor2 = chunk.GetBufferAccessor<InstalledUpgrade>(ref this.m_InstalledUpgradeType);
            BufferAccessor<TripNeeded> bufferAccessor3 = chunk.GetBufferAccessor<TripNeeded>(ref this.m_TripNeededType);
            BufferAccessor<OwnedVehicle> bufferAccessor4 = chunk.GetBufferAccessor<OwnedVehicle>(ref this.m_OwnedVehicles);
            Unity.Mathematics.Random random = this.m_RandomSeed.GetRandom(unfilteredChunkIndex);
            for (int i = 0; i < this.m_SetupData.Length; i++) {
                Entity entity;
                PathfindTargetSeeker<PathfindSetupBuffer> pathfindTargetSeeker;
                this.m_SetupData.GetItem(i, out entity, out pathfindTargetSeeker);
                Resource resource = pathfindTargetSeeker.m_SetupQueueTarget.m_Resource;
                int value = pathfindTargetSeeker.m_SetupQueueTarget.m_Value;
                if ((pathfindTargetSeeker.m_SetupQueueTarget.m_Flags & SetupTargetFlags.RequireTransport) == SetupTargetFlags.None || bufferAccessor4.Length != 0) {
                    for (int j = 0; j < nativeArray.Length; j++) {
                        Entity entity2 = nativeArray[j];
                        Entity prefab = this.m_Prefabs[entity2].m_Prefab;
                        int num = ((bufferAccessor.Length > 0) ? bufferAccessor[j].Length : 0);
                        if (!entity2.Equals(entity)) {
                            bool flag = this.m_OutsideConnections.HasComponent(entity2);
                            bool flag2 = this.m_CargoTransportStations.HasComponent(entity2);
                            bool flag3 = this.m_StorageCompanyDatas.HasComponent(prefab) && !flag2 && !flag;
                            bool flag4 = this.m_ServiceAvailables.HasComponent(entity2);
                            bool flag5 = this.m_IndustrialProcessDatas.HasComponent(prefab) && !flag4 && !flag3;
                            bool flag6 = EconomyUtils.IsOfficeResource(resource);
                            bool flag7 = EconomyUtils.GetWeight(resource, this.m_ResourcePrefabs, ref this.m_ResourceDatas) == 0f;
                            bool flag8 = (pathfindTargetSeeker.m_SetupQueueTarget.m_Flags & SetupTargetFlags.BuildingUpkeep) > SetupTargetFlags.None;
                            if ((!this.m_Buildings.HasComponent(entity2) || !BuildingUtils.CheckOption(this.m_Buildings[entity2], BuildingOption.Inactive)) && ((!flag4 && !flag5) || (this.m_PropertyRenters.HasComponent(entity2) && !(this.m_PropertyRenters[entity2].m_Property == Entity.Null)))) {
                                bool flag9 = false;
                                if (flag6 && flag5 && (this.m_IndustrialProcessDatas[prefab].m_Output.m_Resource & resource) != Resource.NoResource) {
                                    flag9 = true;
                                } else if ((pathfindTargetSeeker.m_SetupQueueTarget.m_Flags & SetupTargetFlags.Commercial) > SetupTargetFlags.None && flag4 && (this.m_IndustrialProcessDatas[prefab].m_Output.m_Resource & resource) != Resource.NoResource) {
                                    flag9 = true;
                                } else if ((pathfindTargetSeeker.m_SetupQueueTarget.m_Flags & SetupTargetFlags.Industrial) > SetupTargetFlags.None && flag5 && (this.m_IndustrialProcessDatas[prefab].m_Output.m_Resource & resource) != Resource.NoResource) {
                                    flag9 = true;
                                } else if ((pathfindTargetSeeker.m_SetupQueueTarget.m_Flags & SetupTargetFlags.Import) != SetupTargetFlags.None && (flag || flag2 || flag3)) {
                                    StorageCompanyData storageCompanyData = this.m_StorageCompanyDatas[prefab];
                                    if (bufferAccessor2.Length != 0) {
                                        UpgradeUtils.CombineStats<StorageCompanyData>(ref storageCompanyData, bufferAccessor2[j], ref pathfindTargetSeeker.m_PrefabRef, ref this.m_StorageCompanyDatas);
                                    }
                                    if ((storageCompanyData.m_StoredResources & resource) != Resource.NoResource || (flag && flag7)) {
                                        flag9 = true;
                                    }
                                }
                                if (flag9 && (flag || !flag8 || bufferAccessor3.Length <= 0 || bufferAccessor3[j].Length <= 0)) {
                                    m_SmallCityShopping.Get().SetShopFound(entity, true);
                                    int num2;
                                    if (flag && flag7) {
                                        num2 = value;
                                    } else {
                                        int allBuyingResourcesTrucks = VehicleUtils.GetAllBuyingResourcesTrucks(entity2, resource, ref this.m_DeliveryTrucks, ref this.m_GuestVehicleBufs, ref this.m_LayoutElementBufs);
                                        num2 = EconomyUtils.GetResources(resource, this.m_Resources[entity2]) - allBuyingResourcesTrucks;
                                        if (num2 <= 0 || (!flag && num2 < value / 2)) {
                                            goto IL_05BF;
                                        }
                                    }
                                    m_SmallCityShopping.Get().SetShopResourceFound(entity, true);
                                    float num3 = 0f;
                                    if (this.m_ServiceAvailables.HasComponent(entity2)) {
                                        ServiceAvailable serviceAvailable = this.m_ServiceAvailables[entity2];
                                        int num4 = math.max(1, this.m_ServiceCompanies[prefab].m_MaxService);
                                        float num5 = (float)serviceAvailable.m_ServiceAvailable / (float)num4;
                                        float servicePriceMultiplier = EconomyUtils.GetServicePriceMultiplier((float)serviceAvailable.m_ServiceAvailable, num4);
                                        float marketPrice = EconomyUtils.GetMarketPrice(resource, this.m_ResourcePrefabs, ref this.m_ResourceDatas);
                                        float num6 = 0f;
                                        if (this.m_TradeCosts.HasBuffer(entity2)) {
                                            num6 = EconomyUtils.GetTradeCost(resource, this.m_TradeCosts[entity2]).m_BuyCost;
                                        }
                                        float num7 = marketPrice * 0.5f;
                                        float num8 = (marketPrice * (servicePriceMultiplier - 1f) + num6 - num7) * (float)value;
                                        float num9 = 100f * math.saturate(1f - 2f * num5);
                                        num3 = (num8 + num9) * pathfindTargetSeeker.m_PathfindParameters.m_Weights.money;
                                    } else {
                                        float num10 = math.min(1f, (float)num2 * 1f / (float)value);
                                        num3 += 100f * (1f - num10);
                                        if (flag2) {
                                            if ((pathfindTargetSeeker.m_SetupQueueTarget.m_Flags & SetupTargetFlags.RequireTransport) != SetupTargetFlags.None) {
                                                if (!this.m_TransportCompanyDatas.HasComponent(prefab)) {
                                                    goto IL_05BF;
                                                }
                                                TransportCompanyData transportCompanyData = this.m_TransportCompanyDatas[prefab];
                                                if (bufferAccessor4[j].Length >= transportCompanyData.m_MaxTransports) {
                                                    goto IL_05BF;
                                                }
                                            }
                                            if (bufferAccessor3.Length > 0 && bufferAccessor3[j].Length >= ResourcePathfindSetup.kCargoStationMaxTripNeededQueue) {
                                                goto IL_05BF;
                                            }
                                            num3 += ResourcePathfindSetup.kCargoStationAmountBasedPenalty * (float)value;
                                            num3 += ResourcePathfindSetup.kCargoStationPerRequestPenalty * (float)num;
                                        }
                                        if (flag) {
                                            num3 += ResourcePathfindSetup.kOutsideConnectionAmountBasedPenalty * (float)value;
                                            if (flag8) {
                                                num3 += (float)random.NextInt(300);
                                            }
                                        }
                                        if (this.m_TradeCosts.HasBuffer(entity2)) {
                                            DynamicBuffer<TradeCost> dynamicBuffer = this.m_TradeCosts[entity2];
                                            TradeCost tradeCost = EconomyUtils.GetTradeCost(resource, dynamicBuffer);
                                            num3 += tradeCost.m_BuyCost * (float)value * 0.01f;
                                        }
                                    }
                                    pathfindTargetSeeker.FindTargets(entity2, num3);
                                }
                            }
                        }
                    IL_05BF:;
                    }
                }
            }
        }

        // Token: 0x06007A5C RID: 31324 RVA: 0x0045FB5C File Offset: 0x0045DD5C
        void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
            this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }

        // Token: 0x0400C777 RID: 51063
        [ReadOnly]
        public EntityTypeHandle m_EntityType;

        // Token: 0x0400C778 RID: 51064
        [ReadOnly]
        public BufferTypeHandle<OwnedVehicle> m_OwnedVehicles;

        // Token: 0x0400C779 RID: 51065
        [ReadOnly]
        public ComponentLookup<IndustrialProcessData> m_IndustrialProcessDatas;

        // Token: 0x0400C77A RID: 51066
        [ReadOnly]
        public ComponentLookup<ServiceAvailable> m_ServiceAvailables;

        // Token: 0x0400C77B RID: 51067
        [ReadOnly]
        public ComponentLookup<ServiceCompanyData> m_ServiceCompanies;

        // Token: 0x0400C77C RID: 51068
        [ReadOnly]
        public ComponentLookup<ResourceData> m_ResourceDatas;

        // Token: 0x0400C77D RID: 51069
        [ReadOnly]
        public ResourcePrefabs m_ResourcePrefabs;

        // Token: 0x0400C77E RID: 51070
        [ReadOnly]
        public ComponentLookup<StorageCompanyData> m_StorageCompanyDatas;

        // Token: 0x0400C77F RID: 51071
        [ReadOnly]
        public ComponentLookup<PropertyRenter> m_PropertyRenters;

        // Token: 0x0400C780 RID: 51072
        [ReadOnly]
        public BufferLookup<Game.Economy.Resources> m_Resources;

        // Token: 0x0400C781 RID: 51073
        [ReadOnly]
        public BufferLookup<TradeCost> m_TradeCosts;

        // Token: 0x0400C782 RID: 51074
        [ReadOnly]
        public ComponentLookup<Game.Objects.OutsideConnection> m_OutsideConnections;

        // Token: 0x0400C783 RID: 51075
        [ReadOnly]
        public ComponentLookup<Game.Buildings.CargoTransportStation> m_CargoTransportStations;

        // Token: 0x0400C784 RID: 51076
        [ReadOnly]
        public BufferTypeHandle<StorageTransferRequest> m_StorageTransferRequestType;

        // Token: 0x0400C785 RID: 51077
        [ReadOnly]
        public ComponentLookup<TransportCompanyData> m_TransportCompanyDatas;

        // Token: 0x0400C786 RID: 51078
        [ReadOnly]
        public BufferTypeHandle<TripNeeded> m_TripNeededType;

        // Token: 0x0400C787 RID: 51079
        [ReadOnly]
        public ComponentLookup<PrefabRef> m_Prefabs;

        // Token: 0x0400C788 RID: 51080
        [ReadOnly]
        public ComponentLookup<Building> m_Buildings;

        // Token: 0x0400C789 RID: 51081
        [ReadOnly]
        public ComponentLookup<Game.Vehicles.DeliveryTruck> m_DeliveryTrucks;

        // Token: 0x0400C78A RID: 51082
        [ReadOnly]
        public BufferLookup<GuestVehicle> m_GuestVehicleBufs;

        // Token: 0x0400C78B RID: 51083
        [ReadOnly]
        public BufferLookup<LayoutElement> m_LayoutElementBufs;

        // Token: 0x0400C78C RID: 51084
        [ReadOnly]
        public BufferTypeHandle<InstalledUpgrade> m_InstalledUpgradeType;

        // Token: 0x0400C78D RID: 51085
        [ReadOnly]
        public RandomSeed m_RandomSeed;

        // Token: 0x0400C78E RID: 51086
        public PathfindSetupSystem.SetupData m_SetupData;
    }
}
