using System;

namespace HongBeautyPOS.Application;

public sealed record AttributeOptionDto(Guid Id, string Name, string Value);

public sealed record ProductDto(Guid Id, string Sku, string Name, decimal SalePrice, decimal CostPrice, decimal Quantity, bool Active, Guid? CategoryId = null, string? Barcode = null, string Unit = "cái", decimal ReorderLevel = 0, IReadOnlyList<AttributeOptionDto>? Attributes = null, string? ImageData = null);
