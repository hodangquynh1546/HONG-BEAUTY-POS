using System;

namespace HongBeautyPOS.Application;

public sealed record SalesOrderDto(Guid Id, string OrderNo, Guid? CustomerId, string Status, decimal Total, decimal PaidTotal, DateTimeOffset CreatedAt, decimal ReturnedTotal = 0m, decimal RefundedTotal = 0m);
