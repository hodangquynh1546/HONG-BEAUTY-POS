using System;

namespace HongBeautyPOS.Application;

public sealed record StockAdjustmentResult(Guid MovementId, decimal Before, decimal After);
