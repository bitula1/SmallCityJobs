using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitulaMod
{
    public enum CustomEventType {
        StartedLookingForWork = 100,
        NoJobsAvailable = 101,
        DoesntLikeAnyJobs = 102,
        StartedLookingForAnotherJob = 103,
        TooFewBetterJobs = 104,
        DoesntWantBetterJob = 105,
        WorkplaceGone = 106,
        EmployerGone = 107,
        CantSwitchJob = 108,
        DebugMessage = 200
    }
}
