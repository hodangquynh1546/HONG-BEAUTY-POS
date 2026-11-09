using System;

namespace HongBeautyPOS.Application;

public sealed record CreatePurchaseRequest(Guid? SupplierId, PurchaseLineRequest[] Items);
