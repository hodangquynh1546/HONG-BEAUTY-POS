using System;

namespace HongBeautyPOS.Application;

public sealed record StockAdjustmentRequest(Guid ProductId, decimal Delta, string Reason);
