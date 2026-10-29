using Colossal.Serialization.Entities;
using Unity.Entities;

namespace BitulaMod.Components {
    public struct CmpShoppingStart : IComponentData, ISerializable {
        private const int SerializationVersion = 3;

        public uint m_StartFrame;
        public uint m_EndFrame;
        public bool m_PurchaseMade;
        public byte m_SaleFlags;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter {
            writer.Write(SerializationVersion);
            writer.Write(m_StartFrame);
            writer.Write(m_EndFrame);
            writer.Write(m_PurchaseMade);
            writer.Write(m_SaleFlags);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader {
            reader.Read(out int version);
            reader.Read(out m_StartFrame);

            if (version >= 2) {
                reader.Read(out m_EndFrame);
            } else {
                m_EndFrame = 0;
            }

            if (version >= 3) {
                reader.Read(out m_PurchaseMade);
                reader.Read(out m_SaleFlags);
            } else {
                m_PurchaseMade = false;
                m_SaleFlags = 0;
            }
        }
    }
}