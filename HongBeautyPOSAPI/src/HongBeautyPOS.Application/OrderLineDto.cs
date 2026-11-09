using System;

namespace HongBeautyPOS.Application;

public sealed record OrderLineDto(Guid Id, Guid ProductId, string Name, string Sku, decimal Quantity, decimal UnitPrice);
