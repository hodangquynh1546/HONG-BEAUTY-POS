using System;

namespace HongBeautyPOS.Domain;

public sealed class CashEntry
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid IdempotencyKey { get; set; }

	public CashEntryKind Kind { get; set; }

	public PaymentMethod Method { get; set; }

	public decimal Amount { get; set; }

	public required string Description { get; set; }

	public Guid CreatedBy { get; set; }

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
