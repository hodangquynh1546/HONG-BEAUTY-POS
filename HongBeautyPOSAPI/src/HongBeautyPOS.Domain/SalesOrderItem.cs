using System;

namespace HongBeautyPOS.Domain;

public sealed class SalesOrderItem
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid OrderId { get; set; }

	public Guid ProductId { get; set; }

	public required string SkuSnapshot { get; set; }

	public required string NameSnapshot { get; set; }

	public decimal Quantity { get; set; }

	public decimal UnitPrice { get; set; }
	public decimal DiscountPercent { get; set; }
	public decimal DiscountAmount { get; set; }

	public decimal UnitCost { get; set; }
}
