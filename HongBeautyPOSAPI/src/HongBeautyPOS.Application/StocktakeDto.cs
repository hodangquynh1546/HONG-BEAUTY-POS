using System;
using System.Collections.Generic;

namespace HongBeautyPOS.Application;

public sealed record StocktakeDto(Guid Id, string Status, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, IReadOnlyList<StocktakeLineDto> Items);
