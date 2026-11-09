using System;
using System.Collections.Generic;

namespace HongBeautyPOS.Domain;

public sealed class SalesOrder
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public required string OrderNo { get; set; }

	public Guid IdempotencyKey { get; set; }

	public Guid? CustomerId { get; set; }

	public Guid SoldBy { get; set; }

	public SaleStatus Status { get; set; } = SaleStatus.Unpaid;

	public decimal Subtotal { get; set; }

	public decimal Discount { get; set; }

	public decimal Total { get; set; }

	public decimal PaidTotal { get; set; }

	public decimal ReturnedTotal { get; set; }

	public decimal RefundedTotal { get; set; }

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

	public List<SalesOrderItem> Items { get; set; } = new List<SalesOrderItem>();
}
