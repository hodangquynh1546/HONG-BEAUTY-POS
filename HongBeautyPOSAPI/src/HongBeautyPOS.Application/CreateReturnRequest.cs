using System;
using HongBeautyPOS.Domain;

namespace HongBeautyPOS.Application;

public sealed record CreateReturnRequest(Guid IdempotencyKey, string Reason, decimal RefundAmount, PaymentMethod RefundMethod, ReturnLineRequest[] Items);
