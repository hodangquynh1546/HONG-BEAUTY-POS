using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HongBeautyPOS.Domain;

namespace HongBeautyPOS.Application;

public interface IPurchaseRepository
{
	Task<IReadOnlyList<PurchaseDto>> ListAsync(CancellationToken cancellationToken);

	Task<PurchaseDto> CreateAsync(PurchaseReceipt receipt, CancellationToken cancellationToken);

	Task<PurchaseDto?> CompleteAsync(Guid receiptId, Guid actorId, CancellationToken cancellationToken);
}
