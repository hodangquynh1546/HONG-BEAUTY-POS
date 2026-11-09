using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HongBeautyPOS.Application;
using HongBeautyPOS.Domain;
using Microsoft.EntityFrameworkCore;

namespace HongBeautyPOS.Infrastructure;

public sealed class ProductRepository(PosDbContext db) : IProductRepository
{
	public async Task<IReadOnlyList<ProductDto>> ListAsync(string? search, CancellationToken cancellationToken)
	{
		IQueryable<Product> query = db.Products.AsNoTracking();
		if (!string.IsNullOrWhiteSpace(search))
		{
			string term = search.Trim();
			query = query.Where((Product p) => EF.Functions.ILike(p.Name, $"%{term}%") || EF.Functions.ILike(p.Sku, $"%{term}%") || p.Barcode == term);
		}
		var products = await (from p in query.OrderBy((Product p) => p.Name).Take(100)
			select new ProductDto(p.Id, p.Sku, p.Name, p.SalePrice, p.CostPrice, (p.Balance == null) ? 0m : p.Balance.Quantity, p.Active, p.CategoryId, p.Barcode, p.Unit, p.ReorderLevel, null, p.ImageData)).ToListAsync(cancellationToken);
		var ids = products.Select(p => p.Id).ToArray();
		var selected = await (from choice in db.ProductAttributeSelections.AsNoTracking()
			join option in db.AttributeOptions.AsNoTracking() on choice.OptionId equals option.Id
			where ids.Contains(choice.ProductId)
			select new { choice.ProductId, option.Id, option.Name, option.Value }).ToListAsync(cancellationToken);
		var byProduct = selected.GroupBy(x => x.ProductId).ToDictionary(g => g.Key,
			g => (IReadOnlyList<AttributeOptionDto>)g.OrderBy(x => x.Name).ThenBy(x => x.Value)
				.Select(x => new AttributeOptionDto(x.Id, x.Name, x.Value)).ToList());
		return products.Select(p => p with { Attributes = byProduct.GetValueOrDefault(p.Id) ?? [] }).ToList();
	}

	public async Task<ProductDto> CreateAsync(Product product, CancellationToken cancellationToken)
	{
		db.Products.Add(product);
		await db.SaveChangesAsync(cancellationToken);
		return new ProductDto(product.Id, product.Sku, product.Name, product.SalePrice, product.CostPrice, 0m, product.Active, product.CategoryId, product.Barcode, product.Unit, product.ReorderLevel, null, product.ImageData);
	}
}
