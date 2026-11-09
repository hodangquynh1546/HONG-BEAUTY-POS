using System;

namespace HongBeautyPOS.Domain;

public sealed class AuditEvent
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid ActorId { get; set; }

	public required string Action { get; set; }

	public Guid SubjectId { get; set; }

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
