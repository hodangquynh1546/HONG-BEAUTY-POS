using System;

namespace HongBeautyPOS.Domain;

public sealed class Category
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid? ParentId { get; set; }

	public required string Name { get; set; }

	public bool Active { get; set; } = true;
}
