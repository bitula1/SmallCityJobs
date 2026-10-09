using Colossal.Serialization.Entities;
using Unity.Entities;

namespace BitulaMod.Components {
    public struct CmpLeisureStart : IComponentData, ISerializable {
        private const int SerializationVersion = 1;
        public uint m_StartFrame;
        public int m_StartMoney;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter {
            writer.Write(SerializationVersion);
            writer.Write(m_StartFrame);
            writer.Write(m_StartMoney);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader {
            reader.Read(out int version);
            reader.Read(out m_StartFrame);
            reader.Read(out m_StartMoney);
        }
    }
}