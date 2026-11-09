using System;

namespace HongBeautyPOS.Domain;

public sealed class StockBalance
{
	public Guid ProductId { get; set; }

	public decimal Quantity { get; set; }

	public uint Version { get; set; }
}
