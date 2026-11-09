using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HongBeautyPOS.Application;
using HongBeautyPOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HongBeautyPOS.Infrastructure;

public sealed class StockRepository(PosDbContext db) : IStockRepository
{
	public async Task<IReadOnlyList<StockDto>> ListAsync(CancellationToken cancellationToken)
	{
		return await (from p in db.Products.AsNoTracking()
			orderby p.Name
			select new StockDto(p.Id, p.Sku, p.Name, (p.Balance == null) ? 0m : p.Balance.Quantity, p.ReorderLevel)).ToListAsync(cancellationToken);
	}

	public async Task<StockAdjustmentResult?> AdjustAsync(Guid productId, decimal delta, string reason, Guid actorId, CancellationToken cancellationToken)
	{
		StockAdjustmentResult result;
		await using (IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken))
		{
			if (delta > 0m)
			{
				await using DbCommand createBalance = db.Database.GetDbConnection().CreateCommand();
				createBalance.Transaction = transaction.GetDbTransaction();
				createBalance.CommandText = "INSERT INTO stock_balances (\"ProductId\", \"Quantity\") SELECT @productId, 0 FROM products WHERE \"Id\" = @productId ON CONFLICT (\"ProductId\") DO NOTHING";
				DbParameter createProductParameter = createBalance.CreateParameter();
				createProductParameter.ParameterName = "productId";
				createProductParameter.Value = productId;
				createBalance.Parameters.Add(createProductParameter);
				await createBalance.ExecuteNonQueryAsync(cancellationToken);
			}
			StockAdjustmentResult stockAdjustmentResult2;
			await using (DbCommand command = db.Database.GetDbConnection().CreateCommand())
			{
				command.Transaction = transaction.GetDbTransaction();
				command.CommandText = "UPDATE stock_balances\nSET \"Quantity\" = \"Quantity\" + @delta\nWHERE \"ProductId\" = @productId AND \"Quantity\" + @delta >= 0\nRETURNING \"Quantity\" - @delta AS before_quantity, \"Quantity\" AS after_quantity";
				DbParameter deltaParameter = command.CreateParameter();
				deltaParameter.ParameterName = "delta";
				deltaParameter.Value = delta;
				command.Parameters.Add(deltaParameter);
				DbParameter productParameter = command.CreateParameter();
				productParameter.ParameterName = "productId";
				productParameter.Value = productId;
				command.Parameters.Add(productParameter);
				StockAdjustmentResult stockAdjustmentResult;
				decimal before;
				decimal after;
				await using (DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
				{
					if (!(await reader.ReadAsync(cancellationToken)))
					{
						stockAdjustmentResult = null;
						goto IL_05d4;
					}
					before = reader.GetDecimal(0);
					after = reader.GetDecimal(1);
				}
				StockMovement movement = new StockMovement
				{
					ProductId = productId,
					Type = "ADJUSTMENT",
					Delta = delta,
					Before = before,
					After = after,
					SourceId = Guid.NewGuid(),
					Reason = reason,
					PerformedBy = actorId
				};
				db.StockMovements.Add(movement);
				await db.SaveChangesAsync(cancellationToken);
				await transaction.CommitAsync(cancellationToken);
				stockAdjustmentResult2 = new StockAdjustmentResult(movement.Id, before, after);
				goto end_IL_0154;
				IL_05d4:
				stockAdjustmentResult2 = stockAdjustmentResult;
				end_IL_0154:;
			}
			result = stockAdjustmentResult2;
		}
		return result;
	}
}
