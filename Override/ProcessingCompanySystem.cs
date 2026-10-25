using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Colossal.Collections;
using Colossal.Entities;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Prefabs;
using Game.Serialization;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Internal;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;
using Game;
using Game.Simulation;

namespace BitulaMod
{
	// Token: 0x0200158C RID: 5516
	public partial class ModProcessingCompanySystem : GameSystemBase, IDefaultSerializable, ISerializable, IPostDeserialize
	{
		// Token: 0x06006993 RID: 27027 RVA: 0x00397F46 File Offset: 0x00396146
		public override int GetUpdateInterval(SystemUpdatePhase phase)
		{
			return 262144 / (EconomyUtils.kCompanyUpdatesPerDay * 16);
		}

		// Token: 0x06006994 RID: 27028 RVA: 0x0039C5F8 File Offset: 0x0039A7F8
		[Preserve]
		protected override void OnCreate()
		{
			base.OnCreate();
			this.m_SimulationSystem = base.World.GetOrCreateSystemManaged<SimulationSystem>();
			this.m_EndFrameBarrier = base.World.GetOrCreateSystemManaged<EndFrameBarrier>();
			this.m_ResourceSystem = base.World.GetOrCreateSystemManaged<ResourceSystem>();
			this.m_TaxSystem = base.World.GetOrCreateSystemManaged<TaxSystem>();
			this.m_VehicleCapacitySystem = base.World.GetOrCreateSystemManaged<VehicleCapacitySystem>();
			this.m_ProductionSpecializationSystem = base.World.GetOrCreateSystemManaged<ProductionSpecializationSystem>();
			this.m_CitySystem = base.World.GetExistingSystemManaged<CitySystem>();
			this.m_CityProductionStatisticSystem = base.World.GetOrCreateSystemManaged<CityProductionStatisticSystem>();
			this.m_OfficeAISystem = base.World.GetOrCreateSystemManaged<OfficeAISystem>();
			this.m_CompanyGroup = base.GetEntityQuery(new ComponentType[]
			{
				ComponentType.ReadWrite<Game.Companies.ProcessingCompany>(),
				ComponentType.ReadOnly<PropertyRenter>(),
				ComponentType.ReadWrite<Game.Economy.Resources>(),
				ComponentType.ReadOnly<PrefabRef>(),
				ComponentType.ReadOnly<WorkProvider>(),
				ComponentType.ReadOnly<UpdateFrame>(),
				ComponentType.ReadWrite<Employee>(),
				ComponentType.Exclude<Deleted>(),
				ComponentType.Exclude<Game.Companies.ExtractorCompany>()
			});
			base.RequireForUpdate(this.m_CompanyGroup);
			base.RequireForUpdate<EconomyParameterData>();
			this.m_ProducedResources = new NativeArray<long>(EconomyUtils.ResourceCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
		}

		// Token: 0x06006995 RID: 27029 RVA: 0x0039C748 File Offset: 0x0039A948
		public void PostDeserialize(Context context)
		{
			if (context.version < Game.Version.officeFix)
			{
				ResourcePrefabs prefabs = this.m_ResourceSystem.GetPrefabs();
				ComponentLookup<ResourceData> componentLookup = InternalCompilerInterface.GetComponentLookup<ResourceData>(ref this.__TypeHandle.__Game_Prefabs_ResourceData_RO_ComponentLookup, ref base.CheckedStateRef);
				NativeArray<Entity> nativeArray = this.m_CompanyGroup.ToEntityArray(Allocator.Temp);
				for (int i = 0; i < nativeArray.Length; i++)
				{
					Entity prefab = base.EntityManager.GetComponentData<PrefabRef>(nativeArray[i]).m_Prefab;
					IndustrialProcessData componentData = base.EntityManager.GetComponentData<IndustrialProcessData>(prefab);
					if (!base.EntityManager.HasComponent<ServiceAvailable>(nativeArray[i]) && componentLookup[prefabs[componentData.m_Output.m_Resource]].m_Weight == 0f)
					{
						DynamicBuffer<Game.Economy.Resources> buffer = base.EntityManager.GetBuffer<Game.Economy.Resources>(nativeArray[i], false);
						if (EconomyUtils.GetResources(componentData.m_Output.m_Resource, buffer) >= 500)
						{
							EconomyUtils.AddResources(componentData.m_Output.m_Resource, -500, buffer);
						}
					}
				}
				nativeArray.Dispose();
			}
		}

		// Token: 0x06006996 RID: 27030 RVA: 0x0039C87C File Offset: 0x0039AA7C
		[Preserve]
		protected override void OnDestroy()
		{
			this.m_ProducedResources.Dispose();
			base.OnDestroy();
		}

		// Token: 0x06006997 RID: 27031 RVA: 0x0039C890 File Offset: 0x0039AA90
		[Preserve]
		protected override void OnUpdate()
		{
			Mod.log.Info("ModProcessingCompanySystem updating");
			uint updateFrame = SimulationUtils.GetUpdateFrame(this.m_SimulationSystem.frameIndex, EconomyUtils.kCompanyUpdatesPerDay, 16);
			ModProcessingCompanySystem.UpdateProcessingJob updateProcessingJob = default(ModProcessingCompanySystem.UpdateProcessingJob);
			updateProcessingJob.m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef);
			updateProcessingJob.m_UpdateFrameType = InternalCompilerInterface.GetSharedComponentTypeHandle<UpdateFrame>(ref this.__TypeHandle.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle, ref base.CheckedStateRef);
			updateProcessingJob.m_PrefabType = InternalCompilerInterface.GetComponentTypeHandle<PrefabRef>(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			updateProcessingJob.m_PropertyType = InternalCompilerInterface.GetComponentTypeHandle<PropertyRenter>(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			updateProcessingJob.m_EmployeeType = InternalCompilerInterface.GetBufferTypeHandle<Employee>(ref this.__TypeHandle.__Game_Companies_Employee_RO_BufferTypeHandle, ref base.CheckedStateRef);
			updateProcessingJob.m_ServiceAvailableType = InternalCompilerInterface.GetComponentTypeHandle<ServiceAvailable>(ref this.__TypeHandle.__Game_Companies_ServiceAvailable_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			updateProcessingJob.m_ResourceType = InternalCompilerInterface.GetBufferTypeHandle<Game.Economy.Resources>(ref this.__TypeHandle.__Game_Economy_Resources_RW_BufferTypeHandle, ref base.CheckedStateRef);
			updateProcessingJob.m_CompanyDataType = InternalCompilerInterface.GetComponentTypeHandle<CompanyData>(ref this.__TypeHandle.__Game_Companies_CompanyData_RW_ComponentTypeHandle, ref base.CheckedStateRef);
			updateProcessingJob.m_TaxPayerType = InternalCompilerInterface.GetComponentTypeHandle<TaxPayer>(ref this.__TypeHandle.__Game_Agents_TaxPayer_RW_ComponentTypeHandle, ref base.CheckedStateRef);
			updateProcessingJob.m_IndustrialProcessDatas = InternalCompilerInterface.GetComponentLookup<IndustrialProcessData>(ref this.__TypeHandle.__Game_Prefabs_IndustrialProcessData_RO_ComponentLookup, ref base.CheckedStateRef);
			updateProcessingJob.m_ResourceDatas = InternalCompilerInterface.GetComponentLookup<ResourceData>(ref this.__TypeHandle.__Game_Prefabs_ResourceData_RO_ComponentLookup, ref base.CheckedStateRef);
			updateProcessingJob.m_Limits = InternalCompilerInterface.GetComponentLookup<StorageLimitData>(ref this.__TypeHandle.__Game_Companies_StorageLimitData_RO_ComponentLookup, ref base.CheckedStateRef);
			updateProcessingJob.m_Buildings = InternalCompilerInterface.GetComponentLookup<Building>(ref this.__TypeHandle.__Game_Buildings_Building_RO_ComponentLookup, ref base.CheckedStateRef);
			updateProcessingJob.m_Specializations = InternalCompilerInterface.GetBufferLookup<SpecializationBonus>(ref this.__TypeHandle.__Game_City_SpecializationBonus_RO_BufferLookup, ref base.CheckedStateRef);
			updateProcessingJob.m_CityModifiers = InternalCompilerInterface.GetBufferLookup<CityModifier>(ref this.__TypeHandle.__Game_City_CityModifier_RO_BufferLookup, ref base.CheckedStateRef);
			updateProcessingJob.m_Citizens = InternalCompilerInterface.GetComponentLookup<Citizen>(ref this.__TypeHandle.__Game_Citizens_Citizen_RO_ComponentLookup, ref base.CheckedStateRef);
			updateProcessingJob.m_BuildingEfficiencies = InternalCompilerInterface.GetBufferLookup<Efficiency>(ref this.__TypeHandle.__Game_Buildings_Efficiency_RW_BufferLookup, ref base.CheckedStateRef);
			updateProcessingJob.m_OfficeProperties = InternalCompilerInterface.GetComponentLookup<OfficeProperty>(ref this.__TypeHandle.__Game_Buildings_OfficeProperty_RO_ComponentLookup, ref base.CheckedStateRef);
			updateProcessingJob.m_ServiceAvailables = InternalCompilerInterface.GetComponentLookup<ServiceAvailable>(ref this.__TypeHandle.__Game_Companies_ServiceAvailable_RO_ComponentLookup, ref base.CheckedStateRef);
			updateProcessingJob.m_ServiceCompanyDatas = InternalCompilerInterface.GetComponentLookup<ServiceCompanyData>(ref this.__TypeHandle.__Game_Companies_ServiceCompanyData_RO_ComponentLookup, ref base.CheckedStateRef);
			updateProcessingJob.m_TaxRates = this.m_TaxSystem.GetTaxRates();
			updateProcessingJob.m_ResourcePrefabs = this.m_ResourceSystem.GetPrefabs();
			updateProcessingJob.m_DeliveryTruckSelectData = this.m_VehicleCapacitySystem.GetDeliveryTruckSelectData();
			updateProcessingJob.m_ProducedResources = this.m_ProducedResources;
			JobHandle jobHandle;
			updateProcessingJob.m_ProductionQueue = this.m_ProductionSpecializationSystem.GetQueue(out jobHandle).AsParallelWriter();
			updateProcessingJob.m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter();
			JobHandle jobHandle2;
			updateProcessingJob.m_CountQueue = this.m_CityProductionStatisticSystem.GetConsumptionQueue(out jobHandle2).AsParallelWriter();
			JobHandle jobHandle3;
			updateProcessingJob.m_OfficeResourceConsumptionAmount = this.m_OfficeAISystem.GetIndustrialConsumptionAmount(out jobHandle3);
			updateProcessingJob.m_EconomyParameters = this.__query_1038562633_0.GetSingleton<EconomyParameterData>();
			updateProcessingJob.m_RandomSeed = RandomSeed.Next();
			updateProcessingJob.m_City = this.m_CitySystem.City;
			updateProcessingJob.m_UpdateFrameIndex = updateFrame;
			ModProcessingCompanySystem.UpdateProcessingJob updateProcessingJob2 = updateProcessingJob;
			base.Dependency = updateProcessingJob2.ScheduleParallel(this.m_CompanyGroup, JobUtils.CombineDependencies(this.m_ProducedResourcesDeps, jobHandle, jobHandle2, jobHandle3, base.Dependency));
			this.m_EndFrameBarrier.AddJobHandleForProducer(base.Dependency);
			this.m_ResourceSystem.AddPrefabsReader(base.Dependency);
			this.m_OfficeAISystem.AddWriteConsumptionDeps(base.Dependency);
			this.m_ProductionSpecializationSystem.AddQueueWriter(base.Dependency);
			this.m_CityProductionStatisticSystem.AddChainWriter(base.Dependency);
			this.m_TaxSystem.AddReader(base.Dependency);
			this.m_ProducedResourcesDeps = default(JobHandle);
		}

		// Token: 0x06006998 RID: 27032 RVA: 0x0039CC81 File Offset: 0x0039AE81
		public NativeArray<long> GetProducedResourcesArray(out JobHandle dependencies)
		{
			dependencies = base.Dependency;
			return this.m_ProducedResources;
		}

		// Token: 0x06006999 RID: 27033 RVA: 0x0039CC95 File Offset: 0x0039AE95
		public void AddProducedResourcesReader(JobHandle handle)
		{
			this.m_ProducedResourcesDeps = JobHandle.CombineDependencies(this.m_ProducedResourcesDeps, handle);
		}

		// Token: 0x0600699A RID: 27034 RVA: 0x0039CCAC File Offset: 0x0039AEAC
		public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
		{
			writer.Write((byte)this.m_ProducedResources.Length);
			for (int i = 0; i < this.m_ProducedResources.Length; i++)
			{
				writer.Write(this.m_ProducedResources[i]);
			}
		}

		// Token: 0x0600699B RID: 27035 RVA: 0x0039CD04 File Offset: 0x0039AF04
		public void Deserialize<TReader>(TReader reader) where TReader : IReader
		{
			byte b;
			reader.Read(out b);
			for (int i = 0; i < (int)b; i++)
			{
				long num;
				reader.Read(out num);
				if (i < this.m_ProducedResources.Length)
				{
					this.m_ProducedResources[i] = num;
				}
			}
			for (int j = (int)b; j < this.m_ProducedResources.Length; j++)
			{
				this.m_ProducedResources[j] = 0L;
			}
		}

		// Token: 0x0600699C RID: 27036 RVA: 0x0039CD7C File Offset: 0x0039AF7C
		public void SetDefaults(Context context)
		{
			for (int i = 0; i < this.m_ProducedResources.Length; i++)
			{
				this.m_ProducedResources[i] = 0L;
			}
		}

		// Token: 0x0600699D RID: 27037 RVA: 0x0039CDB0 File Offset: 0x0039AFB0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void __AssignQueries(ref SystemState state)
		{
			EntityQueryBuilder entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
			this.__query_1038562633_0 = entityQueryBuilder.WithAll<EconomyParameterData>().WithOptions(EntityQueryOptions.IncludeSystems).Build(ref state);
			entityQueryBuilder.Reset();
			entityQueryBuilder.Dispose();
		}

		// Token: 0x0600699E RID: 27038 RVA: 0x0039CDF9 File Offset: 0x0039AFF9
		protected override void OnCreateForCompiler()
		{
			base.OnCreateForCompiler();
			this.__AssignQueries(ref base.CheckedStateRef);
			this.__TypeHandle.__AssignHandles(ref base.CheckedStateRef);
		}

		// Token: 0x0600699F RID: 27039 RVA: 0x00006FBB File Offset: 0x000051BB
		[Preserve]
		public ModProcessingCompanySystem()
		{
		}

		// Token: 0x04009C1C RID: 39964
		public const int kMaxCommercialOutputResource = 5000;

		// Token: 0x04009C1D RID: 39965
		public const float kMaximumTransportUnitCost = 0.03f;

		// Token: 0x04009C1E RID: 39966
		private SimulationSystem m_SimulationSystem;

		// Token: 0x04009C1F RID: 39967
		private EndFrameBarrier m_EndFrameBarrier;

		// Token: 0x04009C20 RID: 39968
		private ResourceSystem m_ResourceSystem;

		// Token: 0x04009C21 RID: 39969
		private TaxSystem m_TaxSystem;

		// Token: 0x04009C22 RID: 39970
		private VehicleCapacitySystem m_VehicleCapacitySystem;

		// Token: 0x04009C23 RID: 39971
		private ProductionSpecializationSystem m_ProductionSpecializationSystem;

		// Token: 0x04009C24 RID: 39972
		private CitySystem m_CitySystem;

		// Token: 0x04009C25 RID: 39973
		private CityProductionStatisticSystem m_CityProductionStatisticSystem;

		// Token: 0x04009C26 RID: 39974
		private OfficeAISystem m_OfficeAISystem;

		// Token: 0x04009C27 RID: 39975
		private EntityQuery m_CompanyGroup;

		// Token: 0x04009C28 RID: 39976
		private NativeArray<long> m_ProducedResources;

		// Token: 0x04009C29 RID: 39977
		private JobHandle m_ProducedResourcesDeps;

		// Token: 0x04009C2A RID: 39978
		private ModProcessingCompanySystem.TypeHandle __TypeHandle;

		// Token: 0x04009C2B RID: 39979
		private EntityQuery __query_1038562633_0;

		// Token: 0x0200158D RID: 5517
		[BurstCompile]
		private struct UpdateProcessingJob : IJobChunk
		{
			// Token: 0x060069A0 RID: 27040 RVA: 0x0039CE20 File Offset: 0x0039B020
			public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				if (chunk.GetSharedComponent<UpdateFrame>(this.m_UpdateFrameType).m_Index != this.m_UpdateFrameIndex)
				{
					return;
				}
				Unity.Mathematics.Random random = this.m_RandomSeed.GetRandom(unfilteredChunkIndex);
				DynamicBuffer<CityModifier> dynamicBuffer = this.m_CityModifiers[this.m_City];
				DynamicBuffer<SpecializationBonus> dynamicBuffer2 = this.m_Specializations[this.m_City];
				NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
				NativeArray<PrefabRef> nativeArray2 = chunk.GetNativeArray<PrefabRef>(ref this.m_PrefabType);
				NativeArray<PropertyRenter> nativeArray3 = chunk.GetNativeArray<PropertyRenter>(ref this.m_PropertyType);
				BufferAccessor<Game.Economy.Resources> bufferAccessor = chunk.GetBufferAccessor<Game.Economy.Resources>(ref this.m_ResourceType);
				BufferAccessor<Employee> bufferAccessor2 = chunk.GetBufferAccessor<Employee>(ref this.m_EmployeeType);
				NativeArray<CompanyData> nativeArray4 = chunk.GetNativeArray<CompanyData>(ref this.m_CompanyDataType);
				NativeArray<TaxPayer> nativeArray5 = chunk.GetNativeArray<TaxPayer>(ref this.m_TaxPayerType);
				bool flag = chunk.Has<ServiceAvailable>(ref this.m_ServiceAvailableType);
				for (int i = 0; i < chunk.Count; i++)
				{
					Entity entity = nativeArray[i];
					Entity prefab = nativeArray2[i].m_Prefab;
					Entity property = nativeArray3[i].m_Property;
					ServiceAvailable serviceAvailable = default(ServiceAvailable);
					ServiceCompanyData serviceCompanyData = default(ServiceCompanyData);
					if (this.m_ServiceAvailables.HasComponent(entity))
					{
						serviceAvailable = this.m_ServiceAvailables[entity];
					}
					if (this.m_ServiceCompanyDatas.HasComponent(prefab))
					{
						serviceCompanyData = this.m_ServiceCompanyDatas[prefab];
					}
					ref CompanyData ptr = ref nativeArray4.ElementAt(i);
					if (this.m_Buildings.HasComponent(property))
					{
						bool flag2 = this.m_OfficeProperties.HasComponent(property);
						DynamicBuffer<Game.Economy.Resources> dynamicBuffer3 = bufferAccessor[i];
						IndustrialProcessData industrialProcessData = this.m_IndustrialProcessDatas[prefab];
						StorageLimitData storageLimitData = this.m_Limits[prefab];
						float num = 1f;
						bool flag3 = false;
						DynamicBuffer<Efficiency> dynamicBuffer4;
						if (this.m_BuildingEfficiencies.TryGetBuffer(property, out dynamicBuffer4))
						{
							this.UpdateEfficiencyFactors(industrialProcessData, flag, dynamicBuffer4, dynamicBuffer, dynamicBuffer2);
							num = BuildingUtils.GetEfficiencyExcludingFactor(dynamicBuffer4, EfficiencyFactor.LackResources);
							flag3 = true;
						}
						int companyProductionPerDay = EconomyUtils.GetCompanyProductionPerDay(num, !flag, bufferAccessor2[i], industrialProcessData, this.m_ResourcePrefabs, ref this.m_ResourceDatas, ref this.m_Citizens, ref this.m_EconomyParameters, serviceAvailable, serviceCompanyData);
						int num2 = MathUtils.RoundToIntRandom(ref random, 1f * (float)companyProductionPerDay / (float)EconomyUtils.kCompanyUpdatesPerDay);
						ResourceStack input = industrialProcessData.m_Input1;
						ResourceStack input2 = industrialProcessData.m_Input2;
						ResourceStack output = industrialProcessData.m_Output;
						if (input.m_Resource != output.m_Resource || input2.m_Resource != Resource.NoResource || input.m_Amount != output.m_Amount)
						{
							float num3 = 1f;
							float num4 = 1f;
							int num5 = 0;
							int num6 = 0;
							if (input.m_Resource != Resource.NoResource && (float)input.m_Amount > 0f)
							{
								int resources = EconomyUtils.GetResources(input.m_Resource, dynamicBuffer3);
								num3 = (float)input.m_Amount * 1f / (float)output.m_Amount;
								num2 = math.min(num2, (int)((float)resources / num3));
							}
							if (input2.m_Resource != Resource.NoResource && (float)input2.m_Amount > 0f)
							{
								int resources2 = EconomyUtils.GetResources(input2.m_Resource, dynamicBuffer3);
								num4 = (float)input2.m_Amount * 1f / (float)output.m_Amount;
								num2 = math.min(num2, (int)((float)resources2 / num4));
							}
							if (flag3)
							{
                                BuildingUtils.SetEfficiencyFactor(dynamicBuffer4, EfficiencyFactor.LackResources, (float)((num2 == 0) ? 0 : 1));
                            }
							int num11;
							if ((float)num2 > 0f)
							{
								int num7 = 0;
								if (flag && EconomyUtils.GetResources(output.m_Resource, dynamicBuffer3) > 5000)
								{
									goto IL_0688;
								}
								if (input.m_Resource != Resource.NoResource)
								{
									num5 = -MathUtils.RoundToIntRandom(ref ptr.m_RandomSeed, (float)num2 * num3);
									int num8 = EconomyUtils.AddResources(input.m_Resource, num5, dynamicBuffer3);
									num7 += ((EconomyUtils.GetWeight(input.m_Resource, this.m_ResourcePrefabs, ref this.m_ResourceDatas) > 0f) ? num8 : 0);
								}
								if (input2.m_Resource != Resource.NoResource)
								{
									num6 = -MathUtils.RoundToIntRandom(ref ptr.m_RandomSeed, (float)num2 * num4);
									int num9 = EconomyUtils.AddResources(input2.m_Resource, num6, dynamicBuffer3);
									num7 += ((EconomyUtils.GetWeight(input2.m_Resource, this.m_ResourcePrefabs, ref this.m_ResourceDatas) > 0f) ? num9 : 0);
								}
								int num10 = storageLimitData.m_Limit - num7;
								if (EconomyUtils.IsResourceHasWeight(output.m_Resource, this.m_ResourcePrefabs, ref this.m_ResourceDatas))
								{
									num2 = math.min(num10, num2);
								}
								else
								{
									num11 = EconomyUtils.GetResources(output.m_Resource, dynamicBuffer3);
									num2 = math.clamp(IndustrialAISystem.kMaxVirtualResourceStorage - num11, 0, num2);
								}
								if (!flag && !flag2)
								{
									Interlocked.Add(ref UnsafeUtility.AsRef<int>((void*)this.m_OfficeResourceConsumptionAmount.GetUnsafePtr<int>()), num2);
								}
								num11 = EconomyUtils.AddResources(output.m_Resource, num2, dynamicBuffer3);
								this.AddProducedResource(output.m_Resource, num2);
								this.m_CountQueue.Enqueue(new CityProductionStatisticSystem.CompanyProcessingEvent
								{
									m_Consume1 = input.m_Resource,
									m_Consume1Amount = num5,
									m_Consume2 = input2.m_Resource,
									m_Consume2Amount = num6,
									m_Produce = output.m_Resource,
									m_ProduceAmount = num2
								});
							}
							else
							{
								num11 = EconomyUtils.GetResources(output.m_Resource, dynamicBuffer3);
							}
							int num12 = EconomyUtils.GetCompanyProfitPerDay(num, !flag, bufferAccessor2[i], industrialProcessData, this.m_ResourcePrefabs, ref this.m_ResourceDatas, ref this.m_Citizens, ref this.m_EconomyParameters, serviceAvailable, serviceCompanyData) / EconomyUtils.kCompanyUpdatesPerDay;
							TaxPayer taxPayer = nativeArray5[i];
							int num13 = (flag ? TaxSystem.GetCommercialTaxRate(output.m_Resource, this.m_TaxRates) : TaxSystem.GetIndustrialTaxRate(output.m_Resource, this.m_TaxRates));
							if (input.m_Resource != output.m_Resource && (float)num12 > 0f)
							{
								if (num12 > 0)
								{
									taxPayer.m_AverageTaxRate = Mathf.RoundToInt(math.lerp((float)taxPayer.m_AverageTaxRate, (float)num13, (float)num12 / (float)(num12 + taxPayer.m_UntaxedIncome)));
								}
								taxPayer.m_UntaxedIncome += num12;
								nativeArray5[i] = taxPayer;
							}
							if (!flag && EconomyUtils.IsResourceHasWeight(output.m_Resource, this.m_ResourcePrefabs, ref this.m_ResourceDatas) && num11 > 0)
							{
								DeliveryTruckSelectItem deliveryTruckSelectItem;
								this.m_DeliveryTruckSelectData.TrySelectItem(ref random, output.m_Resource, num11, out deliveryTruckSelectItem);
								if ((float)deliveryTruckSelectItem.m_Cost / (float)math.min(num11, deliveryTruckSelectItem.m_Capacity) < 0.03f)
								{
									this.m_CommandBuffer.AddComponent<ResourceExporter>(unfilteredChunkIndex, entity, new ResourceExporter
									{
										m_Resource = output.m_Resource,
										m_Amount = math.max(0, math.min(deliveryTruckSelectItem.m_Capacity, num11))
									});
								}
							}
						}
					}
					IL_0688:;
				}
			}

			// Token: 0x060069A1 RID: 27041 RVA: 0x0039D4C8 File Offset: 0x0039B6C8
			private void UpdateEfficiencyFactors(IndustrialProcessData process, bool isCommercial, DynamicBuffer<Efficiency> efficiencies, DynamicBuffer<CityModifier> cityModifiers, DynamicBuffer<SpecializationBonus> specializations)
			{
				if (this.IsOffice(process))
				{
					float num = 100f;
					if (!isCommercial)
					{
						CityUtils.ApplyModifier(ref num, cityModifiers, CityModifierType.OfficeEfficiency);
					}
					BuildingUtils.SetEfficiencyFactor(efficiencies, EfficiencyFactor.CityModifierOfficeEfficiency, num / 100f);
				}
				else if (!isCommercial)
				{
					float num2 = 100f;
					CityUtils.ApplyModifier(ref num2, cityModifiers, CityModifierType.IndustrialEfficiency);
					BuildingUtils.SetEfficiencyFactor(efficiencies, EfficiencyFactor.CityModifierIndustrialEfficiency, num2 / 100f);
				}
				if (process.m_Input1.m_Resource == Resource.Fish || process.m_Input2.m_Resource == Resource.Fish)
				{
					float num3 = 100f;
					CityUtils.ApplyModifier(ref num3, cityModifiers, CityModifierType.IndustrialFishInputEfficiency);
					BuildingUtils.SetEfficiencyFactor(efficiencies, EfficiencyFactor.CityModifierFishInput, num3 / 100f);
				}
				if (process.m_Output.m_Resource == Resource.Software)
				{
					float num4 = 100f;
					CityUtils.ApplyModifier(ref num4, cityModifiers, CityModifierType.OfficeSoftwareEfficiency);
					BuildingUtils.SetEfficiencyFactor(efficiencies, EfficiencyFactor.CityModifierSoftware, num4 / 100f);
				}
				else if (process.m_Output.m_Resource == Resource.Electronics)
				{
					float num5 = 100f;
					CityUtils.ApplyModifier(ref num5, cityModifiers, CityModifierType.IndustrialElectronicsEfficiency);
					BuildingUtils.SetEfficiencyFactor(efficiencies, EfficiencyFactor.CityModifierElectronics, num5 / 100f);
				}
				int resourceIndex = EconomyUtils.GetResourceIndex(process.m_Output.m_Resource);
				if (specializations.Length > resourceIndex)
				{
					float num6 = 1f + specializations[resourceIndex].GetBonus(this.m_EconomyParameters.m_MaxCitySpecializationBonus, this.m_EconomyParameters.m_ResourceProductionCoefficient);
					BuildingUtils.SetEfficiencyFactor(efficiencies, EfficiencyFactor.SpecializationBonus, num6);
				}
			}

			// Token: 0x060069A2 RID: 27042 RVA: 0x0039D632 File Offset: 0x0039B832
			private bool IsOffice(IndustrialProcessData process)
			{
				return !EconomyUtils.IsResourceHasWeight(process.m_Output.m_Resource, this.m_ResourcePrefabs, ref this.m_ResourceDatas);
			}

			// Token: 0x060069A3 RID: 27043 RVA: 0x0039D654 File Offset: 0x0039B854
			private Resource GetRandomUpkeepResource(CompanyData companyData, Resource outputResource)
			{
				switch (companyData.m_RandomSeed.NextInt(4))
				{
				case 0:
					return Resource.Software;
				case 1:
					return Resource.Telecom;
				case 2:
					return Resource.Financial;
				case 3:
					if (EconomyUtils.IsResourceHasWeight(outputResource, this.m_ResourcePrefabs, ref this.m_ResourceDatas))
					{
						return Resource.Machinery;
					}
					if (!companyData.m_RandomSeed.NextBool())
					{
						return Resource.Furniture;
					}
					return Resource.Paper;
				default:
					return Resource.NoResource;
				}
			}

			// Token: 0x060069A4 RID: 27044 RVA: 0x0039D6DC File Offset: 0x0039B8DC
			private unsafe void AddProducedResource(Resource resource, int amount)
			{
				if (resource != Resource.NoResource)
				{
					long* ptr = (long*)this.m_ProducedResources.GetUnsafePtr<long>();
					ptr += EconomyUtils.GetResourceIndex(resource);
					Interlocked.Add(ref *ptr, (long)amount);
					this.m_ProductionQueue.Enqueue(new ProductionSpecializationSystem.ProducedResource
					{
						m_Resource = resource,
						m_Amount = amount
					});
				}
			}

			// Token: 0x060069A5 RID: 27045 RVA: 0x0039D731 File Offset: 0x0039B931
			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}

			// Token: 0x04009C2C RID: 39980
			[ReadOnly]
			public EntityTypeHandle m_EntityType;

			// Token: 0x04009C2D RID: 39981
			[ReadOnly]
			public SharedComponentTypeHandle<UpdateFrame> m_UpdateFrameType;

			// Token: 0x04009C2E RID: 39982
			[ReadOnly]
			public ComponentTypeHandle<PrefabRef> m_PrefabType;

			// Token: 0x04009C2F RID: 39983
			[ReadOnly]
			public ComponentTypeHandle<PropertyRenter> m_PropertyType;

			// Token: 0x04009C30 RID: 39984
			[ReadOnly]
			public BufferTypeHandle<Employee> m_EmployeeType;

			// Token: 0x04009C31 RID: 39985
			[ReadOnly]
			public ComponentTypeHandle<ServiceAvailable> m_ServiceAvailableType;

			// Token: 0x04009C32 RID: 39986
			public BufferTypeHandle<Game.Economy.Resources> m_ResourceType;

			// Token: 0x04009C33 RID: 39987
			public ComponentTypeHandle<CompanyData> m_CompanyDataType;

			// Token: 0x04009C34 RID: 39988
			public ComponentTypeHandle<TaxPayer> m_TaxPayerType;

			// Token: 0x04009C35 RID: 39989
			[ReadOnly]
			public ComponentLookup<IndustrialProcessData> m_IndustrialProcessDatas;

			// Token: 0x04009C36 RID: 39990
			[ReadOnly]
			public ComponentLookup<ResourceData> m_ResourceDatas;

			// Token: 0x04009C37 RID: 39991
			[ReadOnly]
			public ComponentLookup<StorageLimitData> m_Limits;

			// Token: 0x04009C38 RID: 39992
			[ReadOnly]
			public ComponentLookup<Building> m_Buildings;

			// Token: 0x04009C39 RID: 39993
			[ReadOnly]
			public ComponentLookup<Citizen> m_Citizens;

			// Token: 0x04009C3A RID: 39994
			[ReadOnly]
			public ComponentLookup<OfficeProperty> m_OfficeProperties;

			// Token: 0x04009C3B RID: 39995
			[ReadOnly]
			public BufferLookup<SpecializationBonus> m_Specializations;

			// Token: 0x04009C3C RID: 39996
			[ReadOnly]
			public BufferLookup<CityModifier> m_CityModifiers;

			// Token: 0x04009C3D RID: 39997
			[ReadOnly]
			public ComponentLookup<ServiceAvailable> m_ServiceAvailables;

			// Token: 0x04009C3E RID: 39998
			[ReadOnly]
			public ComponentLookup<ServiceCompanyData> m_ServiceCompanyDatas;

			// Token: 0x04009C3F RID: 39999
			[NativeDisableParallelForRestriction]
			public BufferLookup<Efficiency> m_BuildingEfficiencies;

			// Token: 0x04009C40 RID: 40000
			[ReadOnly]
			public NativeArray<int> m_TaxRates;

			// Token: 0x04009C41 RID: 40001
			[ReadOnly]
			public ResourcePrefabs m_ResourcePrefabs;

			// Token: 0x04009C42 RID: 40002
			[ReadOnly]
			public DeliveryTruckSelectData m_DeliveryTruckSelectData;

			// Token: 0x04009C43 RID: 40003
			public NativeArray<long> m_ProducedResources;

			// Token: 0x04009C44 RID: 40004
			public NativeQueue<ProductionSpecializationSystem.ProducedResource>.ParallelWriter m_ProductionQueue;

			// Token: 0x04009C45 RID: 40005
			public NativeQueue<CityProductionStatisticSystem.CompanyProcessingEvent>.ParallelWriter m_CountQueue;

			// Token: 0x04009C46 RID: 40006
			[NativeDisableParallelForRestriction]
			public NativeReference<int> m_OfficeResourceConsumptionAmount;

			// Token: 0x04009C47 RID: 40007
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

			// Token: 0x04009C48 RID: 40008
			public EconomyParameterData m_EconomyParameters;

			// Token: 0x04009C49 RID: 40009
			public RandomSeed m_RandomSeed;

			// Token: 0x04009C4A RID: 40010
			public Entity m_City;

			// Token: 0x04009C4B RID: 40011
			public uint m_UpdateFrameIndex;
		}

		// Token: 0x0200158E RID: 5518
		private struct TypeHandle
		{
			// Token: 0x060069A6 RID: 27046 RVA: 0x0039D740 File Offset: 0x0039B940
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void __AssignHandles(ref SystemState state)
			{
				this.__Game_Prefabs_ResourceData_RO_ComponentLookup = state.GetComponentLookup<ResourceData>(true);
				this.__Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
				this.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle = state.GetSharedComponentTypeHandle<UpdateFrame>();
				this.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle = state.GetComponentTypeHandle<PrefabRef>(true);
				this.__Game_Buildings_PropertyRenter_RO_ComponentTypeHandle = state.GetComponentTypeHandle<PropertyRenter>(true);
				this.__Game_Companies_Employee_RO_BufferTypeHandle = state.GetBufferTypeHandle<Employee>(true);
				this.__Game_Companies_ServiceAvailable_RO_ComponentTypeHandle = state.GetComponentTypeHandle<ServiceAvailable>(true);
				this.__Game_Economy_Resources_RW_BufferTypeHandle = state.GetBufferTypeHandle<Game.Economy.Resources>(false);
				this.__Game_Companies_CompanyData_RW_ComponentTypeHandle = state.GetComponentTypeHandle<CompanyData>(false);
				this.__Game_Agents_TaxPayer_RW_ComponentTypeHandle = state.GetComponentTypeHandle<TaxPayer>(false);
				this.__Game_Prefabs_IndustrialProcessData_RO_ComponentLookup = state.GetComponentLookup<IndustrialProcessData>(true);
				this.__Game_Companies_StorageLimitData_RO_ComponentLookup = state.GetComponentLookup<StorageLimitData>(true);
				this.__Game_Buildings_Building_RO_ComponentLookup = state.GetComponentLookup<Building>(true);
				this.__Game_City_SpecializationBonus_RO_BufferLookup = state.GetBufferLookup<SpecializationBonus>(true);
				this.__Game_City_CityModifier_RO_BufferLookup = state.GetBufferLookup<CityModifier>(true);
				this.__Game_Citizens_Citizen_RO_ComponentLookup = state.GetComponentLookup<Citizen>(true);
				this.__Game_Buildings_Efficiency_RW_BufferLookup = state.GetBufferLookup<Efficiency>(false);
				this.__Game_Buildings_OfficeProperty_RO_ComponentLookup = state.GetComponentLookup<OfficeProperty>(true);
				this.__Game_Companies_ServiceAvailable_RO_ComponentLookup = state.GetComponentLookup<ServiceAvailable>(true);
				this.__Game_Companies_ServiceCompanyData_RO_ComponentLookup = state.GetComponentLookup<ServiceCompanyData>(true);
			}

			// Token: 0x04009C4C RID: 40012
			[ReadOnly]
			public ComponentLookup<ResourceData> __Game_Prefabs_ResourceData_RO_ComponentLookup;

			// Token: 0x04009C4D RID: 40013
			[ReadOnly]
			public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;

			// Token: 0x04009C4E RID: 40014
			public SharedComponentTypeHandle<UpdateFrame> __Game_Simulation_UpdateFrame_SharedComponentTypeHandle;

			// Token: 0x04009C4F RID: 40015
			[ReadOnly]
			public ComponentTypeHandle<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentTypeHandle;

			// Token: 0x04009C50 RID: 40016
			[ReadOnly]
			public ComponentTypeHandle<PropertyRenter> __Game_Buildings_PropertyRenter_RO_ComponentTypeHandle;

			// Token: 0x04009C51 RID: 40017
			[ReadOnly]
			public BufferTypeHandle<Employee> __Game_Companies_Employee_RO_BufferTypeHandle;

			// Token: 0x04009C52 RID: 40018
			[ReadOnly]
			public ComponentTypeHandle<ServiceAvailable> __Game_Companies_ServiceAvailable_RO_ComponentTypeHandle;

			// Token: 0x04009C53 RID: 40019
			public BufferTypeHandle<Game.Economy.Resources> __Game_Economy_Resources_RW_BufferTypeHandle;

			// Token: 0x04009C54 RID: 40020
			public ComponentTypeHandle<CompanyData> __Game_Companies_CompanyData_RW_ComponentTypeHandle;

			// Token: 0x04009C55 RID: 40021
			public ComponentTypeHandle<TaxPayer> __Game_Agents_TaxPayer_RW_ComponentTypeHandle;

			// Token: 0x04009C56 RID: 40022
			[ReadOnly]
			public ComponentLookup<IndustrialProcessData> __Game_Prefabs_IndustrialProcessData_RO_ComponentLookup;

			// Token: 0x04009C57 RID: 40023
			[ReadOnly]
			public ComponentLookup<StorageLimitData> __Game_Companies_StorageLimitData_RO_ComponentLookup;

			// Token: 0x04009C58 RID: 40024
			[ReadOnly]
			public ComponentLookup<Building> __Game_Buildings_Building_RO_ComponentLookup;

			// Token: 0x04009C59 RID: 40025
			[ReadOnly]
			public BufferLookup<SpecializationBonus> __Game_City_SpecializationBonus_RO_BufferLookup;

			// Token: 0x04009C5A RID: 40026
			[ReadOnly]
			public BufferLookup<CityModifier> __Game_City_CityModifier_RO_BufferLookup;

			// Token: 0x04009C5B RID: 40027
			[ReadOnly]
			public ComponentLookup<Citizen> __Game_Citizens_Citizen_RO_ComponentLookup;

			// Token: 0x04009C5C RID: 40028
			public BufferLookup<Efficiency> __Game_Buildings_Efficiency_RW_BufferLookup;

			// Token: 0x04009C5D RID: 40029
			[ReadOnly]
			public ComponentLookup<OfficeProperty> __Game_Buildings_OfficeProperty_RO_ComponentLookup;

			// Token: 0x04009C5E RID: 40030
			[ReadOnly]
			public ComponentLookup<ServiceAvailable> __Game_Companies_ServiceAvailable_RO_ComponentLookup;

			// Token: 0x04009C5F RID: 40031
			[ReadOnly]
			public ComponentLookup<ServiceCompanyData> __Game_Companies_ServiceCompanyData_RO_ComponentLookup;
		}
	}
}
