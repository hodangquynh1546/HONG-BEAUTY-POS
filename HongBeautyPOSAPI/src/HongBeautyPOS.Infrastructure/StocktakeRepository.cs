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

public sealed class StocktakeRepository(PosDbContext db) : IStocktakeRepository
{
	public async Task<IReadOnlyList<StocktakeDto>> ListAsync(CancellationToken cancellationToken)
	{
		return (await (from s in db.StocktakeSessions.AsNoTracking()
			orderby s.CreatedAt descending
			select s).Take(30).ToListAsync(cancellationToken)).Select((StocktakeSession s) => new StocktakeDto(s.Id, s.Status.ToString(), s.CreatedAt, s.CompletedAt, Array.Empty<StocktakeLineDto>())).ToList();
	}

	public async Task<StocktakeDto?> GetAsync(Guid id, CancellationToken cancellationToken)
	{
		StocktakeSession session = await db.StocktakeSessions.AsNoTracking().SingleOrDefaultAsync((StocktakeSession s) => s.Id == id, cancellationToken);
		if (session == null)
		{
			return null;
		}
		return new StocktakeDto(Items: await (from i in db.StocktakeItems.AsNoTracking()
			join p in db.Products.AsNoTracking() on i.ProductId equals p.Id
			where i.SessionId == id
			orderby p.Name
			select new StocktakeLineDto(i.Id, i.ProductId, p.Sku, p.Name, i.ExpectedQuantity, i.ActualQuantity)).ToListAsync(cancellationToken), Id: session.Id, Status: session.Status.ToString(), CreatedAt: session.CreatedAt, CompletedAt: session.CompletedAt);
	}

	public async Task<StocktakeDto> CreateAsync(Guid actorId, CancellationToken cancellationToken)
	{
		var snapshot = await (from p in db.Products.AsNoTracking()
			where p.Active
			select new
			{
				Id = p.Id,
				Quantity = ((p.Balance == null) ? 0m : p.Balance.Quantity)
			}).ToListAsync(cancellationToken);
		if (snapshot.Count == 0)
		{
			throw new InvalidOperationException("Chưa có sản phẩm để kiểm kho.");
		}
		StocktakeSession session = new StocktakeSession
		{
			CreatedBy = actorId,
			Items = snapshot.Select(p => new StocktakeItem
			{
				ProductId = p.Id,
				ExpectedQuantity = p.Quantity
			}).ToList()
		};
		db.StocktakeSessions.Add(session);
		await db.SaveChangesAsync(cancellationToken);
		return await GetAsync(session.Id, cancellationToken);
	}

	public async Task<StocktakeDto?> CountAsync(Guid sessionId, Guid itemId, decimal actualQuantity, CancellationToken cancellationToken)
	{
		StocktakeDto result;
		await using (IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken))
		{
			StocktakeDto stocktakeDto;
			await using (DbCommand lockCommand = db.Database.GetDbConnection().CreateCommand())
			{
				lockCommand.Transaction = transaction.GetDbTransaction();
				lockCommand.CommandText = "SELECT \"Status\" FROM stocktake_sessions WHERE \"Id\" = @id FOR UPDATE";
				AddParameter(lockCommand, "id", sessionId);
				if ((await lockCommand.ExecuteScalarAsync(cancellationToken)) as string != "Draft")
				{
					stocktakeDto = null;
					goto IL_0643;
				}
			}
			StocktakeItem item = await db.StocktakeItems.SingleOrDefaultAsync((StocktakeItem i) => i.Id == itemId && i.SessionId == sessionId, cancellationToken);
			if (item == null)
			{
				result = null;
			}
			else
			{
				item.ActualQuantity = actualQuantity;
				await db.SaveChangesAsync(cancellationToken);
				await transaction.CommitAsync(cancellationToken);
				result = await GetAsync(sessionId, cancellationToken);
			}
			goto end_IL_0145;
			IL_0643:
			result = stocktakeDto;
			end_IL_0145:;
		}
		return result;
	}

	public async Task<StocktakeDto?> CompleteAsync(Guid sessionId, Guid actorId, CancellationToken cancellationToken)
	{
		StocktakeDto result;
		await using (IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken))
		{
			StocktakeDto stocktakeDto;
			await using (DbCommand lockCommand = db.Database.GetDbConnection().CreateCommand())
			{
				lockCommand.Transaction = transaction.GetDbTransaction();
				lockCommand.CommandText = "SELECT \"Status\" FROM stocktake_sessions WHERE \"Id\" = @id FOR UPDATE";
				AddParameter(lockCommand, "id", sessionId);
				if ((await lockCommand.ExecuteScalarAsync(cancellationToken)) as string != "Draft")
				{
					stocktakeDto = null;
					goto IL_0a30;
				}
			}
			StocktakeSession session = await db.StocktakeSessions.Include((StocktakeSession s) => s.Items).SingleAsync((StocktakeSession s) => s.Id == sessionId, cancellationToken);
			if (session.Items.Count == 0 || session.Items.Any((StocktakeItem i) => !i.ActualQuantity.HasValue))
			{
				throw new InvalidOperationException("Cần nhập số lượng thực tế cho tất cả sản phẩm.");
			}
			foreach (StocktakeItem item in session.Items.OrderBy((StocktakeItem i) => i.ProductId))
			{
				decimal actual = item.ActualQuantity.Value;
				await using DbCommand command = db.Database.GetDbConnection().CreateCommand();
				command.Transaction = transaction.GetDbTransaction();
				command.CommandText = "UPDATE stock_balances SET \"Quantity\" = @actual\nWHERE \"ProductId\" = @productId AND \"Quantity\" = @expected\nRETURNING \"Quantity\"";
				AddParameter(command, "actual", actual);
				AddParameter(command, "productId", item.ProductId);
				AddParameter(command, "expected", item.ExpectedQuantity);
				if (await command.ExecuteScalarAsync(cancellationToken) == null)
				{
					throw new InvalidOperationException("Tồn kho đã thay đổi trong lúc kiểm. Hãy tạo phiên kiểm kho mới.");
				}
				if (actual != item.ExpectedQuantity)
				{
					db.StockMovements.Add(new StockMovement
					{
						ProductId = item.ProductId,
						Type = "STOCKTAKE",
						Delta = actual - item.ExpectedQuantity,
						Before = item.ExpectedQuantity,
						After = actual,
						SourceId = item.Id,
						Reason = $"Kiểm kho {session.Id}",
						PerformedBy = actorId
					});
				}
			}
			session.Status = StocktakeStatus.Completed;
			session.CompletedAt = DateTimeOffset.UtcNow;
			await db.SaveChangesAsync(cancellationToken);
			await transaction.CommitAsync(cancellationToken);
			result = await GetAsync(sessionId, cancellationToken);
			goto end_IL_0146;
			IL_0a30:
			result = stocktakeDto;
			end_IL_0146:;
		}
		return result;
	}

	private static void AddParameter(DbCommand command, string name, object value)
	{
		DbParameter dbParameter = command.CreateParameter();
		dbParameter.ParameterName = name;
		dbParameter.Value = value;
		command.Parameters.Add(dbParameter);
	}
}
