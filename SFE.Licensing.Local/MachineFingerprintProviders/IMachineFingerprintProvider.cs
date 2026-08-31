using SFE.Licensing.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SFE.Licensing.Local.MachineFingerprintProviders
{
    public interface IMachineFingerprintProvider
    {
        MachineFingerprint Compute();
    }
}
