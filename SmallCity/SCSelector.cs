using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

namespace BitulaMod.SmallCity {
    public interface ISCFeature<T> where T : unmanaged {
        T Create(ref SystemState state, in SCCommon common);
        T Create();
    }
    public static class SmallCitySelector {

        public static ref T Get<T>(this ref SmallCity<T> scj)
            where T : unmanaged, ISCFeature<T> {

            return ref scj.Feature;
        }
    }
}
