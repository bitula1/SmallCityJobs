using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitulaMod {
    public enum SmallCityJobsPhase : byte {
        None,
        LookingForJob,
        FindJob,
        SetupPath,
        StartWorking
    }
}
