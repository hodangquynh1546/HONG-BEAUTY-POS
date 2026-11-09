using System;
using System.Data.Common;
using Npgsql;

namespace HongBeautyPOS.Infrastructure;

public static class DatabaseConnection
{
	public static string Normalize(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new ArgumentException("DATABASE_URL is required", "value");
		}
		if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
		{
			return new NpgsqlConnectionStringBuilder(value).ConnectionString;
		}
		if (!Uri.TryCreate(value, UriKind.Absolute, out Uri result) || string.IsNullOrWhiteSpace(result.Host))
		{
			throw new ArgumentException("DATABASE_URL is not a valid PostgreSQL URL", "value");
		}
		string[] array = result.UserInfo.Split(':', 2);
		if (array.Length != 2 || string.IsNullOrWhiteSpace(array[0]))
		{
			throw new ArgumentException("DATABASE_URL must include a username and password", "value");
		}
		string text = Uri.UnescapeDataString(result.AbsolutePath.Trim('/'));
		if (string.IsNullOrWhiteSpace(text))
		{
			throw new ArgumentException("DATABASE_URL must include a database name", "value");
		}
		NpgsqlConnectionStringBuilder npgsqlConnectionStringBuilder = new NpgsqlConnectionStringBuilder
		{
			Host = result.Host,
			Port = (result.IsDefaultPort ? 5432 : result.Port),
			Database = text,
			Username = Uri.UnescapeDataString(array[0]),
			Password = Uri.UnescapeDataString(array[1])
		};
		string[] array2 = result.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
		string[] array3 = array2;
		foreach (string text2 in array3)
		{
			string[] array4 = text2.Split('=', 2);
			string text3 = Uri.UnescapeDataString(array4[0]).ToLowerInvariant();
			string value2 = ((array4.Length == 2) ? Uri.UnescapeDataString(array4[1]) : string.Empty);
			if (text3 == "sslmode")
			{
				((DbConnectionStringBuilder)npgsqlConnectionStringBuilder)["SSL Mode"] = value2;
			}
			else if (text3 == "channel_binding")
			{
				((DbConnectionStringBuilder)npgsqlConnectionStringBuilder)["Channel Binding"] = value2;
			}
		}
		return ((DbConnectionStringBuilder)npgsqlConnectionStringBuilder).ConnectionString;
	}
}
