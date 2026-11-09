using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HongBeautyPOS.Application;

public interface ISalesRepository
{
	Task<IReadOnlyList<SalesOrderDto>> ListAsync(CancellationToken cancellationToken);

	Task<SalesOrderDto> CreateAsync(CreateSaleRequest request, Guid actorId, CancellationToken cancellationToken);

	Task<PaymentDto?> AddPaymentAsync(Guid orderId, AddPaymentRequest request, Guid actorId, CancellationToken cancellationToken);
}
