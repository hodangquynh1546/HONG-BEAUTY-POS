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
		if (request.SalePrice < 0m || request.CostPrice < 0m || request.ReorderLevel < 0m || string.IsNullOrWhiteSpace(request.Unit) || request.Unit.Trim().Length > 40 || request.Barcode?.Trim().Length > 100)
		{
			throw new ArgumentOutOfRangeException("request", "Giá không được âm.");
		}
		return repository.CreateAsync(new Product
		{
			Sku = request.Sku.Trim(),
			Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim(),
			Name = request.Name.Trim(),
			Unit = request.Unit.Trim(),
			ReorderLevel = request.ReorderLevel,
			CategoryId = request.CategoryId,
			SalePrice = request.SalePrice,
			CostPrice = request.CostPrice,
			Balance = new StockBalance
			{
				Quantity = 0m
			},
			AttributeSelections = (request.AttributeOptionIds ?? []).Distinct().Select(id => new ProductAttributeSelection { OptionId = id }).ToList()
		}, cancellationToken);
	}
}
