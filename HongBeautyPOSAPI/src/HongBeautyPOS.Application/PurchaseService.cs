using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HongBeautyPOS.Domain;

namespace HongBeautyPOS.Application;

public sealed class PurchaseService(IPurchaseRepository repository)
{
	public Task<IReadOnlyList<PurchaseDto>> ListAsync(CancellationToken cancellationToken)
	{
		return repository.ListAsync(cancellationToken);
	}

	public Task<PurchaseDto> CreateAsync(CreatePurchaseRequest request, Guid actorId, CancellationToken cancellationToken)
	{
		bool flag = actorId == Guid.Empty || request.Items == null;
		bool flag2 = flag;
		if (!flag2)
		{
			int num = request.Items.Length;
			bool flag3 = ((num < 1 || num > 100) ? true : false);
			flag2 = flag3;
		}
		if (flag2 || request.Items.Any((PurchaseLineRequest i) => i.ProductId == Guid.Empty || i.Quantity <= 0m || i.UnitCost < 0m) || request.Items.Select((PurchaseLineRequest i) => i.ProductId).Distinct().Count() != request.Items.Length)
		{
			throw new ArgumentException("Phiếu nhập không hợp lệ.");
		}
		PurchaseReceipt receipt = new PurchaseReceipt
		{
			ReceiptNo = $"PN-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".Substring(0, 32),
			SupplierId = request.SupplierId,
			CreatedBy = actorId,
			Total = request.Items.Sum((PurchaseLineRequest i) => i.Quantity * i.UnitCost),
			Items = request.Items.Select((PurchaseLineRequest i) => new PurchaseItem
			{
				ProductId = i.ProductId,
				Quantity = i.Quantity,
				UnitCost = i.UnitCost
			}).ToList()
		};
		return repository.CreateAsync(receipt, cancellationToken);
	}

	public Task<PurchaseDto?> CompleteAsync(Guid receiptId, Guid actorId, CancellationToken cancellationToken)
	{
		if (receiptId == Guid.Empty || actorId == Guid.Empty)
		{
			throw new ArgumentException("Phiếu nhập không hợp lệ.");
		}
		return repository.CompleteAsync(receiptId, actorId, cancellationToken);
	}
}
