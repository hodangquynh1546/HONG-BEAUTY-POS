using System;

namespace HongBeautyPOS.Domain;

public sealed class Customer
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public required string FullName { get; set; }

	public string? Phone { get; set; }
	public string? Phone2 { get; set; }
	public string? Gender { get; set; }
	public string? Address { get; set; }
	public string? Contact { get; set; }
	public string? Notes { get; set; }
	public string? AvatarData { get; set; }
	public Guid? GroupId { get; set; }

	public string? Email { get; set; }

	public bool Active { get; set; } = true;
}

public sealed class CustomerGroup
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public required string Name { get; set; }
	public bool Active { get; set; } = true;
}
