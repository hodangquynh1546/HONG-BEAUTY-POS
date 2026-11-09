using System;

namespace HongBeautyPOS.Domain;

public sealed class EmployeePermission
{
	public Guid UserId { get; set; }

	public required string Permission { get; set; }

	public Guid GrantedBy { get; set; }

	public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
}
