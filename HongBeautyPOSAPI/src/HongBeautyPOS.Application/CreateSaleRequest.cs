using System;

namespace HongBeautyPOS.Application;

public sealed record CreateSaleRequest(Guid IdempotencyKey, Guid? CustomerId, decimal Discount, SaleLineRequest[] Items);
