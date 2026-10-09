using System;
using Colossal.Serialization.Entities;
using Unity.Entities;

namespace BitulaMod.Components {
    public struct CmpSmallCityJobs : IComponentData, ISerializable {
        private const int SerializationVersion = 1;
        public bool m_UseSmallCityBehaviour;
        public bool m_FoundHigherJob;
        public bool m_FoundCloserJob;
        public SmallCityJobsPhase m_phase;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter {
            writer.Write(SerializationVersion);
            writer.Write(m_UseSmallCityBehaviour);
            writer.Write(m_FoundHigherJob);
            writer.Write(m_FoundCloserJob);
            writer.Write((int)m_phase);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader {
            reader.Read(out int version);
            reader.Read(out m_UseSmallCityBehaviour);
            reader.Read(out m_FoundHigherJob);
            reader.Read(out m_FoundCloserJob);
            reader.Read(out int phase);
            m_phase = (SmallCityJobsPhase)phase;
        }
    }
}