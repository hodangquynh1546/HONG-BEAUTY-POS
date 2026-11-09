using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HongBeautyPOS.Application;

public interface IStockRepository
{
	Task<IReadOnlyList<StockDto>> ListAsync(CancellationToken cancellationToken);

	Task<StockAdjustmentResult?> AdjustAsync(Guid productId, decimal delta, string reason, Guid actorId, CancellationToken cancellationToken);
}
