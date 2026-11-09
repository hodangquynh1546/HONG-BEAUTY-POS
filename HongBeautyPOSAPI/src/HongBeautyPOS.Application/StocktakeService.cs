using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HongBeautyPOS.Application;

public sealed class StocktakeService(IStocktakeRepository repository)
{
	public Task<IReadOnlyList<StocktakeDto>> ListAsync(CancellationToken cancellationToken)
	{
		return repository.ListAsync(cancellationToken);
	}

	public Task<StocktakeDto?> GetAsync(Guid id, CancellationToken cancellationToken)
	{
		return repository.GetAsync(id, cancellationToken);
	}

	public Task<StocktakeDto> CreateAsync(Guid actorId, CancellationToken cancellationToken)
	{
		if (actorId == Guid.Empty)
		{
			throw new ArgumentException("Người kiểm kho không hợp lệ.");
		}
		return repository.CreateAsync(actorId, cancellationToken);
	}

	public Task<StocktakeDto?> CountAsync(Guid sessionId, Guid itemId, decimal actualQuantity, CancellationToken cancellationToken)
	{
		if (sessionId == Guid.Empty || itemId == Guid.Empty || actualQuantity < 0m)
		{
			throw new ArgumentException("Số lượng thực tế không hợp lệ.");
		}
		return repository.CountAsync(sessionId, itemId, actualQuantity, cancellationToken);
	}

	public Task<StocktakeDto?> CompleteAsync(Guid sessionId, Guid actorId, CancellationToken cancellationToken)
	{
		if (sessionId == Guid.Empty || actorId == Guid.Empty)
		{
			throw new ArgumentException("Phiên kiểm kho không hợp lệ.");
		}
		return repository.CompleteAsync(sessionId, actorId, cancellationToken);
	}
}
