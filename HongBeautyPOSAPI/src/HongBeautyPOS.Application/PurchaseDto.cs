using System;

namespace HongBeautyPOS.Application;

public sealed record PurchaseDto(Guid Id, string ReceiptNo, Guid? SupplierId, string Status, decimal Total, DateTimeOffset CreatedAt);
