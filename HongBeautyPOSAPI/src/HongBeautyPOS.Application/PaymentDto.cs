using System;

namespace HongBeautyPOS.Application;

public sealed record PaymentDto(Guid Id, Guid OrderId, string Method, decimal Amount, decimal PaidTotal, string OrderStatus);
