using System;
using System.Collections.Generic;

namespace HongBeautyPOS.Domain;

public sealed class PurchaseReceipt
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public required string ReceiptNo { get; set; }

	public Guid? SupplierId { get; set; }

	public PurchaseStatus Status { get; set; } = PurchaseStatus.Draft;

	public decimal Total { get; set; }

	public Guid CreatedBy { get; set; }

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

	public DateTimeOffset? CompletedAt { get; set; }

	public List<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
}
