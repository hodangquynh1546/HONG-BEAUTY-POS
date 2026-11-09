using System;
using System.Collections.Generic;

namespace HongBeautyPOS.Domain;

public sealed class User
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public required string Email { get; set; }

	public required string PasswordHash { get; set; }

	public required string FullName { get; set; }

	public UserRole Role { get; set; }

	public bool Active { get; set; } = true;

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

	public List<EmployeePermission> Permissions { get; set; } = new List<EmployeePermission>();
}
