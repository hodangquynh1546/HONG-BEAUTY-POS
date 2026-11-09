using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HongBeautyPOS.Application;

public interface IReturnRepository
{
	Task<IReadOnlyList<OrderLineDto>> GetOrderLinesAsync(Guid orderId, CancellationToken cancellationToken);

	Task<IReadOnlyList<ReturnDto>> ListAsync(Guid orderId, CancellationToken cancellationToken);

	Task<ReturnDto> CreateAsync(Guid orderId, CreateReturnRequest request, Guid actorId, CancellationToken cancellationToken);
}
