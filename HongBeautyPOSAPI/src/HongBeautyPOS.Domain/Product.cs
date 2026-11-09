using System;

namespace HongBeautyPOS.Domain;

public sealed class Product
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid? CategoryId { get; set; }

	public required string Sku { get; set; }

	public string? Barcode { get; set; }

	public required string Name { get; set; }

	public string Unit { get; set; } = "cái";

	public decimal CostPrice { get; set; }

	public decimal SalePrice { get; set; }

	public decimal ReorderLevel { get; set; }

	public bool Active { get; set; } = true;

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

	public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

	public StockBalance? Balance { get; set; }
}
