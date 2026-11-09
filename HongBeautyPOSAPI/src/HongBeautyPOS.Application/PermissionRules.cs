using System;
using System.Collections.Generic;
using System.Linq;

namespace HongBeautyPOS.Application;

public static class PermissionRules
{
	public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
	{
		"products.read", "products.write", "inventory.read", "inventory.adjust", "purchases.read", "purchases.write", "sales.read", "sales.write", "payments.write", "returns.write",
		"customers.read", "customers.write", "suppliers.read", "suppliers.write", "reports.read", "cash.read", "cash.write"
	};

	public static bool AreValid(IEnumerable<string>? permissions)
	{
		return permissions?.All((string permission) => permission != null && Allowed.Contains(permission)) ?? false;
	}
}
