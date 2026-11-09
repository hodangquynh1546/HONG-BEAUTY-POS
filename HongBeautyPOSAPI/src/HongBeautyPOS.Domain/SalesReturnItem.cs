using System;

namespace HongBeautyPOS.Domain;

public sealed class SalesReturnItem
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid ReturnId { get; set; }

	public Guid OrderItemId { get; set; }

	public decimal Quantity { get; set; }

	public bool Restock { get; set; }

	public decimal Amount { get; set; }
}
