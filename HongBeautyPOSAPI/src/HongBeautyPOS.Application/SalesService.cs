using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HongBeautyPOS.Application;

public sealed class SalesService(ISalesRepository repository)
{
	public Task<IReadOnlyList<SalesOrderDto>> ListAsync(CancellationToken cancellationToken)
	{
		return repository.ListAsync(cancellationToken);
	}

	public Task<SalesOrderDto> CreateAsync(CreateSaleRequest request, Guid actorId, CancellationToken cancellationToken)
	{
		bool flag = actorId == Guid.Empty || request.IdempotencyKey == Guid.Empty || request.Discount < 0m || request.Items == null;
		bool flag2 = flag;
		if (!flag2)
		{
			int num = request.Items.Length;
			bool flag3 = ((num < 1 || num > 100) ? true : false);
			flag2 = flag3;
		}
		if (flag2 || request.Items.Any((SaleLineRequest i) => i.ProductId == Guid.Empty || i.Quantity <= 0m) || request.Items.Select((SaleLineRequest i) => i.ProductId).Distinct().Count() != request.Items.Length)
		{
			throw new ArgumentException("Giỏ hàng không hợp lệ.");
		}
		return repository.CreateAsync(request, actorId, cancellationToken);
	}

	public Task<PaymentDto?> AddPaymentAsync(Guid orderId, AddPaymentRequest request, Guid actorId, CancellationToken cancellationToken)
	{
		if (orderId == Guid.Empty || actorId == Guid.Empty || request.IdempotencyKey == Guid.Empty || request.Amount <= 0m || !Enum.IsDefined(request.Method))
		{
			throw new ArgumentException("Khoản thanh toán không hợp lệ.");
		}
		return repository.AddPaymentAsync(orderId, request, actorId, cancellationToken);
	}
}
