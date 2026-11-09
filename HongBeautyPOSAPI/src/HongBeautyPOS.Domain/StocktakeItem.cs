using System;

namespace HongBeautyPOS.Domain;

public sealed class StocktakeItem
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid SessionId { get; set; }

	public Guid ProductId { get; set; }

	public decimal ExpectedQuantity { get; set; }

	public decimal? ActualQuantity { get; set; }
}
