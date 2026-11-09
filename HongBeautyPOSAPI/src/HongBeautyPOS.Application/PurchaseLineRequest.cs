using System;

namespace HongBeautyPOS.Application;

public sealed record PurchaseLineRequest(Guid ProductId, decimal Quantity, decimal UnitCost);
