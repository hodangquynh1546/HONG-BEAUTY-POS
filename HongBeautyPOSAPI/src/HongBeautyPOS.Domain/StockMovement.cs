using System;

namespace HongBeautyPOS.Domain;

public sealed class StockMovement
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid ProductId { get; set; }

	public required string Type { get; set; }

	public decimal Delta { get; set; }

	public decimal Before { get; set; }

	public decimal After { get; set; }

	public Guid SourceId { get; set; }

	public required string Reason { get; set; }

	public Guid PerformedBy { get; set; }

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
