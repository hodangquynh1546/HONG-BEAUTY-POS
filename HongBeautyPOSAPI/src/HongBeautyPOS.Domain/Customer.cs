using System;

namespace HongBeautyPOS.Domain;

public sealed class Customer
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public required string FullName { get; set; }

	public string? Phone { get; set; }

	public string? Email { get; set; }

	public bool Active { get; set; } = true;
}
