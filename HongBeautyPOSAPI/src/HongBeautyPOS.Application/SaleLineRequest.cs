using System;

namespace HongBeautyPOS.Application;

public sealed record SaleLineRequest(Guid ProductId, decimal Quantity);
