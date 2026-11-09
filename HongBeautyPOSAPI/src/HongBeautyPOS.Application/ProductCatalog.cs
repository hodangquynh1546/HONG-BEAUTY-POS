using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HongBeautyPOS.Domain;

namespace HongBeautyPOS.Application;

public sealed class ProductCatalog(IProductRepository repository)
{
	public Task<IReadOnlyList<ProductDto>> ListAsync(string? search, CancellationToken cancellationToken)
	{
		return repository.ListAsync(search, cancellationToken);
	}

	public Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(request.Sku) || string.IsNullOrWhiteSpace(request.Name) || request.Sku.Trim().Length > 100 || request.Name.Trim().Length > 240)
		{
			throw new ArgumentException("SKU và tên sản phẩm là bắt buộc.");
		}
		if (request.SalePrice < 0m || request.CostPrice < 0m)
		{
			throw new ArgumentOutOfRangeException("request", "Giá không được âm.");
		}
		return repository.CreateAsync(new Product
		{
			Sku = request.Sku.Trim(),
			Name = request.Name.Trim(),
			CategoryId = request.CategoryId,
			SalePrice = request.SalePrice,
			CostPrice = request.CostPrice,
			Balance = new StockBalance
			{
				Quantity = 0m
			}
		}, cancellationToken);
	}
}
