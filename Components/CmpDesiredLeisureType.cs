using Colossal.Serialization.Entities;
using Game.Agents;
using Unity.Entities;

namespace BitulaMod.Components {
    public struct CmpDesiredLeisureType : IComponentData, ISerializable {
        private const int SerializationVersion = 1;

        public LeisureType m_Type;
        public byte m_Desire;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter {
            writer.Write(SerializationVersion);
            writer.Write((int)m_Type);
            writer.Write(m_Desire);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader {
            reader.Read(out int version);
            reader.Read(out int type);
            m_Type = (LeisureType)type;
            reader.Read(out m_Desire);
        }
    }
}