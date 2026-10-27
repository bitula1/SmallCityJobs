using Game.Areas;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace BitulaMod.SmallCity {
    public struct SmallCity<T> where T : unmanaged, ISCFeature<T> {

        public T Feature;

        public static SmallCity<T> Create( ref SystemState state, EntityCommandBuffer.ParallelWriter? commandBuffer = null) {

            SCCommon common = SCCommon.Create(ref state, commandBuffer);
            T feature = default;
            feature = feature.Create(ref state, in common);

            return new SmallCity<T> {
                Feature = feature
            };
        }
        public static SmallCity<T> Create() {

            T feature = default;
            feature = feature.Create();

            return new SmallCity<T> {
                Feature = feature
            };
        }
        public void Update(ref SystemState state) {
            Feature.Update(ref state);
        }

        public bool CitizenExists(Entity citizenEntity) {
            SCCommon common = Feature.Common;
            return common.CitizenExists(citizenEntity);
        }

        public void SetCommandBuffer(EntityCommandBuffer.ParallelWriter buffer, int index) {
            SCCommon common = Feature.Common;
            common.SetCommandBuffer(buffer, index);
            Feature.Common = common;
        }

        public static void AddProducer(ref SystemState state, JobHandle dependency) {
            var eventSender =
                state.World.GetOrCreateSystemManaged<LifePathEventSenderSystem>();

            eventSender.AddProducer(dependency);
        }


        public static int GetAppliedPercentage(int population, int milestone, int increment) {
            int passedMilestones = math.max(0, population - 1) / milestone;

            return math.min(100, passedMilestones * increment);
        }

        public static int GetProgression(int population) {
            int passedMilestones = math.max(0, population - 1) / Mod.Settings.JobSeekerMilestone;

            return math.min(100, passedMilestones * Mod.Settings.JobSeekerFailureIncrement);
        }
    }
}
