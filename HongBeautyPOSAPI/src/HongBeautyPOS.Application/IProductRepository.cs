using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HongBeautyPOS.Domain;

namespace HongBeautyPOS.Application;

public interface IProductRepository
{
	Task<IReadOnlyList<ProductDto>> ListAsync(string? search, CancellationToken cancellationToken);

	Task<ProductDto> CreateAsync(Product product, CancellationToken cancellationToken);
}
