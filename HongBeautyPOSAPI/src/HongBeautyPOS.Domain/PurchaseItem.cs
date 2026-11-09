using System;

namespace HongBeautyPOS.Domain;

public sealed class PurchaseItem
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid ReceiptId { get; set; }

	public Guid ProductId { get; set; }

	public decimal Quantity { get; set; }

	public decimal UnitCost { get; set; }
}
