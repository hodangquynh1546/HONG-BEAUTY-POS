using System;

namespace HongBeautyPOS.Application;

public sealed record ReturnLineRequest(Guid OrderItemId, decimal Quantity, bool Restock);
