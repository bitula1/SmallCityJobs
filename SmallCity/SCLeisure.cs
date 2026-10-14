using BitulaMod.Components;
using Game.Agents;
using Game.Citizens;
using Game.Economy;
using Unity.Entities;
using Unity.Mathematics;

namespace BitulaMod.SmallCity {
    public struct SCLeisure : ISCFeature<SCLeisure> {
        private bool m_NormalLeisure;
        private int m_NormalLeisureMin;
        private int m_NormalLeisureMax;
        private int m_LeisureIntervalMin;
        private int m_LeisureIntervalMax;
        private int m_SpendingLeisureMin;
        private int m_SpendingLeisureMax;
        private SCCommon m_Common;
        private EntityCommandBuffer.ParallelWriter m_CommandBuffer;
        private ComponentLookup<CmpDesiredLeisureType> m_DesiredLeisureTypes;
        public ComponentLookup<CmpDesiredProviderFound> m_DesiredProviderFound;
        public ComponentLookup<CmpAvailableProviderFound> m_AvailableProviderFound;
        private ComponentLookup<CmpLeisureStart> m_LeisureStarts;

        public SCLeisure Create(ref SystemState state, in SCCommon common) {
           
            return new SCLeisure {
                m_CommandBuffer = common.CommandBuffer,
                m_Common = common,

                m_NormalLeisure = Mod.Settings.NormalLeisure,
                m_NormalLeisureMin = Mod.Settings.NormalLeisureMin,
                m_NormalLeisureMax = Mod.Settings.NormalLeisureMax,
                m_LeisureIntervalMin = Mod.Settings.LeisureIntervalMin,
                m_LeisureIntervalMax = Mod.Settings.LeisureIntervalMax,
                m_SpendingLeisureMin = Mod.Settings.SpendingLeisureMin,
                m_SpendingLeisureMax = Mod.Settings.SpendingLeisureMax,

                m_DesiredLeisureTypes = state.GetComponentLookup<CmpDesiredLeisureType>(true),
                m_DesiredProviderFound = state.GetComponentLookup<CmpDesiredProviderFound>(true),
                m_AvailableProviderFound = state.GetComponentLookup<CmpAvailableProviderFound>(true),
                m_LeisureStarts = state.GetComponentLookup<CmpLeisureStart>(true),
            };
        }

        public SCLeisure Create() {
            return new SCLeisure {
                m_NormalLeisure = Mod.Settings.NormalLeisure,
                m_NormalLeisureMin = Mod.Settings.NormalLeisureMin,
                m_NormalLeisureMax = Mod.Settings.NormalLeisureMax,
                m_LeisureIntervalMin = Mod.Settings.LeisureIntervalMin,
                m_LeisureIntervalMax = Mod.Settings.LeisureIntervalMax,
                m_SpendingLeisureMin = Mod.Settings.SpendingLeisureMin,
                m_SpendingLeisureMax = Mod.Settings.SpendingLeisureMax,
            };
        }
        public bool init(Entity citizen, SmallCityJobsPhase phase) {
            return m_Common.init(citizen, phase);
        }
        public bool UseSmallCityBehavior() {
            return m_Common.UseSmallCityBehavior();
        }

        public int GetLeisureChance(Entity citizen) {
            Citizen citizenData = m_Common.GetCitizenData(citizen);
            return Unity.Mathematics.Random.CreateFromIndex(
                (uint)((int)citizenData.m_PseudoRandom + 10000)
            ).NextInt(m_NormalLeisureMin, m_NormalLeisureMax + 1);
        }

        public int GetLeisureChance(Citizen citizen) {
            return Unity.Mathematics.Random.CreateFromIndex(
                (uint)((int)citizen.m_PseudoRandom + 10000)
            ).NextInt(m_NormalLeisureMin, m_NormalLeisureMax + 1);
        }

        public byte GetLeisureDesireLevel(Citizen citizen) {
            int chance = GetLeisureChance(citizen);
            return (byte)(1 + math.clamp((chance - m_NormalLeisureMin) * 5 / (m_NormalLeisureMax - m_NormalLeisureMin + 1), 0, 4));
        }

        public bool DoLeisure(Entity citizen, uint simulationFrame) {
            if (!m_Common.UseSmallCityBehavior() || !m_NormalLeisure)
                return false;

            Citizen citizenData = m_Common.GetCitizenData(citizen);
            int chance = GetLeisureChance(citizen);
            uint period = simulationFrame / CitizenBehaviorSystem.kLeisureSeekerCooldownFrames;
            int roll = Unity.Mathematics.Random.CreateFromIndex(
                (uint)citizenData.m_PseudoRandom + 20000u + period
            ).NextInt(100);
            bool result = roll < chance;
            if (result) {
                m_Common.SetEventToWatch(CustomEventType.NoCityIndoors);
                m_Common.SetEventToWatch(CustomEventType.NoEntertainment);
                m_Common.SetEventToWatch(CustomEventType.NoCityPark);
                m_Common.SetEventToWatch(CustomEventType.NoMeals);
                m_Common.SetEventToWatch(CustomEventType.NoCommercial);
                m_Common.SetEventToWatch(CustomEventType.NoCityBeach);
                m_Common.SetHint(EventHint.CounterWatchEvent);
                m_Common.SetHint(EventHint.DirectPreviousMessage);
                m_Common.SetWatchHint(EventHint.WatchEvent);
                m_Common.Send(citizen, CustomEventType.DoLeisure);
            } else {
                m_Common.SetHint(EventHint.SendThenWatchEvent);
                m_Common.SetEventToWatch(CustomEventType.DoLeisure);
                m_Common.Send(citizen, CustomEventType.WantNoLeisure);
            }
            return result;
        }

        public void PrintLeisureIssue(Entity citizen, LeisureType desired) {
            if (desired == LeisureType.Count)
                return;

            if (GetDesiredProviderFound(citizen) &&
                !GetAvailableProviderFound(citizen)) {
                m_Common.Send(citizen, CustomEventType.ServiceFull);
                return;
            }

            switch (desired) {
                case LeisureType.Meals:
                    m_Common.SetWatchHint(EventHint.CounterWatchEvent);
                    m_Common.Send(citizen, CustomEventType.NoMeals);
                    break;
                case LeisureType.CityPark:
                    m_Common.SetWatchHint(EventHint.CounterWatchEvent);
                    m_Common.Send(citizen, CustomEventType.NoCityPark);
                    break;
                case LeisureType.CityIndoors:
                    m_Common.SetWatchHint(EventHint.CounterWatchEvent);
                    m_Common.Send(citizen, CustomEventType.NoCityIndoors);
                    break;
                case LeisureType.Entertainment:
                    m_Common.SetWatchHint(EventHint.CounterWatchEvent);
                    m_Common.Send(citizen, CustomEventType.NoEntertainment);
                    break;
                case LeisureType.Commercial:
                    m_Common.SetWatchHint(EventHint.CounterWatchEvent);
                    m_Common.Send(citizen, CustomEventType.NoCommercial);
                    break;
                case LeisureType.CityBeach:
                    m_Common.SetWatchHint(EventHint.CounterWatchEvent);
                    m_Common.Send(citizen, CustomEventType.NoCityBeach);
                    break;

                default:
                    m_Common.Send(citizen, CustomEventType.NoLeisureProvider);
                    break;
            }
        }

        public void PrintDestination(Entity entity, Entity destination) {
            m_Common.Send(entity, CustomEventType.FoundLeisureAt, destination);
        }

        public void SetDesiredLeisureType(Entity citizen, LeisureType type, byte desire) {
            CmpDesiredLeisureType component = new CmpDesiredLeisureType {
                m_Type = type,
                m_Desire = desire
            };

            if (m_DesiredLeisureTypes.HasComponent(citizen))
                m_CommandBuffer.SetComponent(citizen.Index, citizen, component);
            else
                m_CommandBuffer.AddComponent(citizen.Index, citizen, component);
        }

        public LeisureType GetDesiredLeisureType(Entity citizen) {
            if (!m_DesiredLeisureTypes.HasComponent(citizen))
                return LeisureType.Count;

            return m_DesiredLeisureTypes[citizen].m_Type;
        }

        private void SetLeisureStart(Entity citizen, uint startFrame, int startMoney) {
            CmpLeisureStart component = new CmpLeisureStart {
                m_StartFrame = startFrame,
                m_StartMoney = startMoney
            };

            if (m_LeisureStarts.HasComponent(citizen))
                m_CommandBuffer.SetComponent(citizen.Index, citizen, component);
            else
                m_CommandBuffer.AddComponent(citizen.Index, citizen, component);
        }

        public void SetLeisureStart(Entity citizen, uint simulationFrame) {
            int startMoney = 0;

            if (m_Common.GetHouseholdMembers().TryGetComponent(citizen, out HouseholdMember householdMember) &&
                m_Common.GetResources().TryGetBuffer(householdMember.m_Household, out DynamicBuffer<Game.Economy.Resources> resources)) {

                startMoney = EconomyUtils.GetResources(Resource.Money, resources);
            }

            SetLeisureStart(citizen, simulationFrame, startMoney);
        }

        public uint GetLeisureStartFrame(Entity citizen) {
            if (!m_LeisureStarts.HasComponent(citizen))
                return 0;

            return m_LeisureStarts[citizen].m_StartFrame;
        }

        public void SetDesiredProviderFound(Entity citizen) {
            m_CommandBuffer.SetComponent(
                citizen.Index,
                citizen,
                new CmpDesiredProviderFound {
                    m_Value = true
                });
        }

        public void SetAvailableProviderFound(Entity citizen) {
            m_CommandBuffer.SetComponent(
                citizen.Index,
                citizen,
                new CmpAvailableProviderFound {
                    m_Value = true
                });
        }

        public void CreateDesiredProviderFound(Entity citizen) {
            CmpDesiredProviderFound component = new CmpDesiredProviderFound {
                m_Value = false
            };

            if (m_DesiredProviderFound.HasComponent(citizen))
                m_CommandBuffer.SetComponent(citizen.Index, citizen, component);
            else
                m_CommandBuffer.AddComponent(citizen.Index, citizen, component);
        }

        public void CreateAvailableProviderFound(Entity citizen) {
            CmpAvailableProviderFound component = new CmpAvailableProviderFound {
                m_Value = false
            };

            if (m_AvailableProviderFound.HasComponent(citizen))
                m_CommandBuffer.SetComponent(citizen.Index, citizen, component);
            else
                m_CommandBuffer.AddComponent(citizen.Index, citizen, component);
        }

        public bool GetDesiredProviderFound(Entity citizen) {
            return m_DesiredProviderFound.HasComponent(citizen) &&
                   m_DesiredProviderFound[citizen].m_Value;
        }

        public bool GetAvailableProviderFound(Entity citizen) {
            return m_AvailableProviderFound.HasComponent(citizen) &&
                   m_AvailableProviderFound[citizen].m_Value;
        }

        public uint LeisureEndFrame(Entity citizen, uint lastPossibleFrame) {
            Citizen citizenData = m_Common.GetCitizenData(citizen);
            uint startFrame = GetLeisureStartFrame(citizen);
            return LeisureEndFrame(citizenData, lastPossibleFrame, startFrame);
        }
        public uint LeisureEndFrame(Citizen citizenData, uint lastPossibleFrame, uint startFrame) {
            if (startFrame == 0 || lastPossibleFrame <= startFrame)
                return 0;

            uint day = startFrame / SCCommon.kFramesPerDay;

            float percent = Unity.Mathematics.Random.CreateFromIndex(
                (uint)citizenData.m_PseudoRandom + 30000u + day
            ).NextFloat(m_LeisureIntervalMin / 100f, (m_LeisureIntervalMax - DesireLengthPercentReduction(citizenData)) / 100f);

            uint duration = lastPossibleFrame - startFrame;
            uint endFrame = startFrame + (uint)(duration * percent);

            return endFrame;
        }

        public bool IsEndOfLeisure(Entity citizen, uint simulationFrame, uint lastPossibleFrame) {
            if (!m_Common.UseSmallCityBehavior() || !m_NormalLeisure)
                return false;

            uint endFrame = LeisureEndFrame(citizen, lastPossibleFrame);

            if (endFrame == 0)
                return true;
            return simulationFrame >= endFrame;
        }

        private float DesireLengthPercentReduction(Citizen citizen) {
            if (m_NormalLeisureMax <= m_NormalLeisureMin)
                return 0f;

            int desire = GetLeisureChance(citizen);

            float desireFactor =
                (desire - m_NormalLeisureMin) /
                (float)(m_NormalLeisureMax - m_NormalLeisureMin);

            return (m_LeisureIntervalMax - m_LeisureIntervalMin) * (1f - desireFactor);
        }
        public bool IsEndOfSpending(Entity citizen) {
            if (!m_Common.UseSmallCityBehavior() || !m_NormalLeisure)
                return false;

            if (!m_LeisureStarts.TryGetComponent(citizen, out CmpLeisureStart leisureStart))
                return false;

            if (leisureStart.m_StartMoney <= 0)
                return true;

            if (!m_Common.GetHouseholdMembers().TryGetComponent(citizen, out HouseholdMember householdMember))
                return false;

            if (!m_Common.GetResources().TryGetBuffer(householdMember.m_Household, out DynamicBuffer<Game.Economy.Resources> resources))
                return false;

            int currentMoney = EconomyUtils.GetResources(Resource.Money, resources);

            Citizen citizenData = m_Common.GetCitizenData(citizen);

            uint day = leisureStart.m_StartFrame / SCCommon.kFramesPerDay;

            float percent = Unity.Mathematics.Random.CreateFromIndex(
                (uint)citizenData.m_PseudoRandom + 40000u + day
            ).NextFloat(
                m_SpendingLeisureMin / 100f,
                m_SpendingLeisureMax / 100f
            );

            int maxSpend = (int)(leisureStart.m_StartMoney * percent);
            int spent = leisureStart.m_StartMoney - currentMoney;

            return spent >= maxSpend;
        }

    }
}
