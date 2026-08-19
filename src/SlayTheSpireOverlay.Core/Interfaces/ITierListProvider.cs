using System.Collections.Generic;
using System.Threading.Tasks;
using SlayTheSpireOverlay.Core.Models;

namespace SlayTheSpireOverlay.Core.Interfaces;

public interface ITierListProvider
{
    Task<IReadOnlyDictionary<string, CardTierData>> GetTierListAsync(bool forceRefresh = false);
}
