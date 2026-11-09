using System;
using System.Collections.Generic;

namespace HongBeautyPOS.Domain;

public sealed class SalesReturn
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid OrderId { get; set; }

	public Guid IdempotencyKey { get; set; }

	public required string Reason { get; set; }

	public decimal Total { get; set; }

	public decimal RefundAmount { get; set; }

	public PaymentMethod RefundMethod { get; set; }

	public Guid CreatedBy { get; set; }

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

	public List<SalesReturnItem> Items { get; set; } = new List<SalesReturnItem>();
}
