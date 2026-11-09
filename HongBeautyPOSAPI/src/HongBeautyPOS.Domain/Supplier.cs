using System;

namespace HongBeautyPOS.Domain;

public sealed class Supplier
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public required string Code { get; set; }

	public required string Name { get; set; }

	public string? Phone { get; set; }

	public string? Email { get; set; }

	public bool Active { get; set; } = true;

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
