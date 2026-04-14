using SFE.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SFE.Application.Interfaces
{
    public interface IAppSettingsRepository
    {
        Task<AppSettings?> GetCurrentAsync();
        Task UpdateAsync(AppSettings settings);
    }
}
