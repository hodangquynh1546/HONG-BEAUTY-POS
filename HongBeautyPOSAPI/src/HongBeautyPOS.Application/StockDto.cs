using System;

namespace HongBeautyPOS.Application;

public sealed record StockDto(Guid ProductId, string Sku, string Name, decimal Quantity, decimal ReorderLevel);
