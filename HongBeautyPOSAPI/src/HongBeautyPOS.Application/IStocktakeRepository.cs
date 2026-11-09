using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HongBeautyPOS.Application;

public interface IStocktakeRepository
{
	Task<IReadOnlyList<StocktakeDto>> ListAsync(CancellationToken cancellationToken);

	Task<StocktakeDto?> GetAsync(Guid id, CancellationToken cancellationToken);

	Task<StocktakeDto> CreateAsync(Guid actorId, CancellationToken cancellationToken);

	Task<StocktakeDto?> CountAsync(Guid sessionId, Guid itemId, decimal actualQuantity, CancellationToken cancellationToken);

	Task<StocktakeDto?> CompleteAsync(Guid sessionId, Guid actorId, CancellationToken cancellationToken);
}
