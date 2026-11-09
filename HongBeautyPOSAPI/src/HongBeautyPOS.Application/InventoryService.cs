using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HongBeautyPOS.Application;

public sealed class InventoryService(IStockRepository repository)
{
	public Task<IReadOnlyList<StockDto>> ListAsync(CancellationToken cancellationToken)
	{
		return repository.ListAsync(cancellationToken);
	}

	public Task<StockAdjustmentResult?> AdjustAsync(StockAdjustmentRequest request, Guid actorId, CancellationToken cancellationToken)
	{
		if (request.ProductId == Guid.Empty || actorId == Guid.Empty || request.Delta == 0m || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 5)
		{
			throw new ArgumentException("Sản phẩm, số lượng thay đổi và lý do hợp lệ là bắt buộc.");
		}
		return repository.AdjustAsync(request.ProductId, request.Delta, request.Reason.Trim(), actorId, cancellationToken);
	}
}
