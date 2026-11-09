using System;

namespace HongBeautyPOS.Application;

public sealed record ReturnDto(Guid Id, Guid OrderId, decimal Total, decimal RefundAmount, string RefundMethod, DateTimeOffset CreatedAt);
