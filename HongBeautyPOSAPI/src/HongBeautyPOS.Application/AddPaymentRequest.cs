using System;
using HongBeautyPOS.Domain;

namespace HongBeautyPOS.Application;

public sealed record AddPaymentRequest(Guid IdempotencyKey, PaymentMethod Method, decimal Amount);
