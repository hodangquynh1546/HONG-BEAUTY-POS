using System;

namespace HongBeautyPOS.Application;

public sealed record CreateProductRequest(string Sku, string Name, decimal SalePrice, decimal CostPrice, Guid? CategoryId = null, string? Barcode = null, string Unit = "cái", decimal ReorderLevel = 0, Guid[]? AttributeOptionIds = null);
