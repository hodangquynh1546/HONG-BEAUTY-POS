using System;
using System.Collections.Generic;

namespace HongBeautyPOS.Domain;

public sealed class StocktakeSession
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public StocktakeStatus Status { get; set; } = StocktakeStatus.Draft;

	public Guid CreatedBy { get; set; }

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

	public DateTimeOffset? CompletedAt { get; set; }

	public List<StocktakeItem> Items { get; set; } = new List<StocktakeItem>();
}
