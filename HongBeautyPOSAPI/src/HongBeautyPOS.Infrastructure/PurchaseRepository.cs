using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HongBeautyPOS.Application;
using HongBeautyPOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace HongBeautyPOS.Infrastructure;

public sealed class PurchaseRepository(PosDbContext db) : IPurchaseRepository
{
	public async Task<IReadOnlyList<PurchaseDto>> ListAsync(CancellationToken cancellationToken)
	{
		return (await (from r in db.PurchaseReceipts.AsNoTracking()
			orderby r.CreatedAt descending
			select r).Take(100).ToListAsync(cancellationToken)).Select(Map).ToList();
	}

	public async Task<PurchaseDto> CreateAsync(PurchaseReceipt receipt, CancellationToken cancellationToken)
	{
		db.PurchaseReceipts.Add(receipt);
		await db.SaveChangesAsync(cancellationToken);
		return Map(receipt);
	}

	public async Task<PurchaseDto?> CompleteAsync(Guid receiptId, Guid actorId, CancellationToken cancellationToken)
	{
		PurchaseDto result;
		await using (IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken))
		{
			PurchaseDto purchaseDto;
			await using (DbCommand lockCommand = db.Database.GetDbConnection().CreateCommand())
			{
				lockCommand.Transaction = transaction.GetDbTransaction();
				lockCommand.CommandText = "SELECT \"Status\" FROM purchase_receipts WHERE \"Id\" = @id FOR UPDATE";
				DbParameter parameter = lockCommand.CreateParameter();
				parameter.ParameterName = "id";
				parameter.Value = receiptId;
				lockCommand.Parameters.Add(parameter);
				string status = (await lockCommand.ExecuteScalarAsync(cancellationToken)) as string;
				if (status != "Draft")
				{
					purchaseDto = null;
					goto IL_0cae;
				}
			}
			PurchaseReceipt receipt = await db.PurchaseReceipts.Include((PurchaseReceipt r) => r.Items).SingleAsync((PurchaseReceipt r) => r.Id == receiptId, cancellationToken);
			if (receipt.Items.Count == 0)
			{
				throw new InvalidOperationException("Phiếu nhập không có sản phẩm.");
			}
			foreach (PurchaseItem item in receipt.Items.OrderBy((PurchaseItem i) => i.ProductId))
			{
				await using DbCommand command = db.Database.GetDbConnection().CreateCommand();
				command.Transaction = transaction.GetDbTransaction();
				command.CommandText = "UPDATE stock_balances SET \"Quantity\" = \"Quantity\" + @quantity\nWHERE \"ProductId\" = @productId\nRETURNING \"Quantity\" - @quantity AS before_quantity, \"Quantity\" AS after_quantity";
				DbParameter quantity = command.CreateParameter();
				quantity.ParameterName = "quantity";
				quantity.Value = item.Quantity;
				command.Parameters.Add(quantity);
				DbParameter product = command.CreateParameter();
				product.ParameterName = "productId";
				product.Value = item.ProductId;
				command.Parameters.Add(product);
				decimal before;
				decimal after;
				await using (DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
				{
					if (!(await reader.ReadAsync(cancellationToken)))
					{
						throw new InvalidOperationException("Sản phẩm không có tồn kho.");
					}
					before = reader.GetDecimal(0);
					after = reader.GetDecimal(1);
				}
				await db.Products.Where((Product p) => p.Id == item.ProductId).ExecuteUpdateAsync((UpdateSettersBuilder<Product> setters) =>
				{
					setters.SetProperty((Product p) => p.CostPrice, item.UnitCost);
				}, cancellationToken);
				db.StockMovements.Add(new StockMovement
				{
					ProductId = item.ProductId,
					Type = "PURCHASE",
					Delta = item.Quantity,
					Before = before,
					After = after,
					SourceId = item.Id,
					Reason = "Nhập hàng " + receipt.ReceiptNo,
					PerformedBy = actorId
				});
			}
			receipt.Status = PurchaseStatus.Completed;
			receipt.CompletedAt = DateTimeOffset.UtcNow;
			await db.SaveChangesAsync(cancellationToken);
			await transaction.CommitAsync(cancellationToken);
			result = Map(receipt);
			goto end_IL_0151;
			IL_0cae:
			result = purchaseDto;
			end_IL_0151:;
		}
		return result;
	}

	private static PurchaseDto Map(PurchaseReceipt receipt)
	{
		return new PurchaseDto(receipt.Id, receipt.ReceiptNo, receipt.SupplierId, receipt.Status.ToString(), receipt.Total, receipt.CreatedAt);
	}
}
