using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

namespace BitulaMod.SmallCity {
    public struct SCInfo<T> where T : unmanaged, ISCInfo<T> {
        public T Info;

        public static SCInfo<T> Create(ref SystemState state) {
            return new SCInfo<T> {
                Info = default(T).Create(ref state)
            };
        }

        public void Update(ref SystemState state) {
            Info.Update(ref state);
        }

        public T Get() {
            return Info;
        }
    }
}
