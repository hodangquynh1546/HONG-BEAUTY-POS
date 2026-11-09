using System;

namespace HongBeautyPOS.Application;

public sealed record StocktakeLineDto(Guid Id, Guid ProductId, string Sku, string Name, decimal ExpectedQuantity, decimal? ActualQuantity);
