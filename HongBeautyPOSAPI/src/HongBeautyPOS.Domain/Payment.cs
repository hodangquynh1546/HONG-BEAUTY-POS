using System;

namespace HongBeautyPOS.Domain;

public sealed class Payment
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid OrderId { get; set; }

	public Guid IdempotencyKey { get; set; }

	public PaymentMethod Method { get; set; }

	public decimal Amount { get; set; }

	public Guid ReceivedBy { get; set; }

	public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}
