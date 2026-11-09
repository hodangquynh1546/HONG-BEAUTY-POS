using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HongBeautyPOS.Application;

public sealed class ReturnService(IReturnRepository repository)
{
	public Task<IReadOnlyList<OrderLineDto>> GetOrderLinesAsync(Guid orderId, CancellationToken cancellationToken)
	{
		return repository.GetOrderLinesAsync(orderId, cancellationToken);
	}

	public Task<IReadOnlyList<ReturnDto>> ListAsync(Guid orderId, CancellationToken cancellationToken)
	{
		return repository.ListAsync(orderId, cancellationToken);
	}

	public Task<ReturnDto> CreateAsync(Guid orderId, CreateReturnRequest request, Guid actorId, CancellationToken cancellationToken)
	{
		bool flag = orderId == Guid.Empty || actorId == Guid.Empty || request.IdempotencyKey == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 5 || request.Reason.Length > 500 || request.RefundAmount < 0m || !Enum.IsDefined(request.RefundMethod) || request.Items == null;
		bool flag2 = flag;
		if (!flag2)
		{
			int num = request.Items.Length;
			bool flag3 = ((num < 1 || num > 100) ? true : false);
			flag2 = flag3;
		}
		if (flag2 || request.Items.Any((ReturnLineRequest i) => i.OrderItemId == Guid.Empty || i.Quantity <= 0m) || request.Items.Select((ReturnLineRequest i) => i.OrderItemId).Distinct().Count() != request.Items.Length)
		{
			throw new ArgumentException("Thông tin trả hàng không hợp lệ.");
		}
		return repository.CreateAsync(orderId, request, actorId, cancellationToken);
	}
}
