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

public sealed class ReturnRepository(PosDbContext db) : IReturnRepository
{
	public async Task<IReadOnlyList<OrderLineDto>> GetOrderLinesAsync(Guid orderId, CancellationToken cancellationToken)
	{
		return await (from i in db.SalesOrderItems.AsNoTracking()
			where i.OrderId == orderId
			select new OrderLineDto(i.Id, i.ProductId, i.NameSnapshot, i.SkuSnapshot, i.Quantity, i.UnitPrice)).ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<ReturnDto>> ListAsync(Guid orderId, CancellationToken cancellationToken)
	{
		return await (from r in db.SalesReturns.AsNoTracking()
			where r.OrderId == orderId
			orderby r.CreatedAt descending
			select new ReturnDto(r.Id, r.OrderId, r.Total, r.RefundAmount, r.RefundMethod.ToString(), r.CreatedAt)).ToListAsync(cancellationToken);
	}

	public async Task<ReturnDto> CreateAsync(Guid orderId, CreateReturnRequest request, Guid actorId, CancellationToken cancellationToken)
	{
		ReturnDto result;
		await using (IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken))
		{
			await using (DbCommand lockKey = db.Database.GetDbConnection().CreateCommand())
			{
				lockKey.Transaction = transaction.GetDbTransaction();
				lockKey.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))";
				AddParameter(lockKey, "key", request.IdempotencyKey.ToString());
				await lockKey.ExecuteScalarAsync(cancellationToken);
			}
			SalesReturn previous = await db.SalesReturns.AsNoTracking().SingleOrDefaultAsync((SalesReturn r) => r.IdempotencyKey == request.IdempotencyKey, cancellationToken);
			if (previous != null)
			{
				if (previous.OrderId != orderId)
				{
					throw new InvalidOperationException("Mã giao dịch đã dùng cho đơn khác.");
				}
				result = Map(previous);
			}
			else
			{
				await using (DbCommand lockOrder = db.Database.GetDbConnection().CreateCommand())
				{
					lockOrder.Transaction = transaction.GetDbTransaction();
					lockOrder.CommandText = "SELECT \"Id\" FROM sales_orders WHERE \"Id\" = @id FOR UPDATE";
					AddParameter(lockOrder, "id", orderId);
					if (await lockOrder.ExecuteScalarAsync(cancellationToken) == null)
					{
						throw new InvalidOperationException("Đơn bán hàng không tồn tại.");
					}
				}
				SalesOrder order = await db.SalesOrders.SingleAsync((SalesOrder o) => o.Id == orderId, cancellationToken);
				SaleStatus status = order.Status;
				if (((status == SaleStatus.Cancelled || status == SaleStatus.Returned) ? 1 : 0) != 0)
				{
					throw new InvalidOperationException("Đơn hàng không còn được trả.");
				}
				List<SalesOrderItem> sold = await db.SalesOrderItems.Where((SalesOrderItem i) => i.OrderId == orderId).ToListAsync(cancellationToken);
				Dictionary<Guid, SalesOrderItem> soldById = sold.ToDictionary((SalesOrderItem i) => i.Id);
				Dictionary<Guid, decimal> alreadyReturned = (from i in await (from item in db.SalesReturnItems.AsNoTracking()
						join ret in db.SalesReturns.AsNoTracking() on item.ReturnId equals ret.Id
						where ret.OrderId == orderId
						select new { item.OrderItemId, item.Quantity }).ToListAsync(cancellationToken)
					group i by i.OrderItemId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
				ReturnLineRequest[] items = request.Items;
				ReturnLineRequest[] array = items;
				foreach (ReturnLineRequest requested in array)
				{
					if (!soldById.TryGetValue(requested.OrderItemId, out SalesOrderItem line) || requested.Quantity + alreadyReturned.GetValueOrDefault(requested.OrderItemId) > line.Quantity)
					{
						throw new InvalidOperationException("Số lượng trả vượt số đã bán hoặc sản phẩm không thuộc đơn.");
					}
					line = null;
				}
				decimal factor = ((order.Subtotal == 0m) ? 0m : (order.Total / order.Subtotal));
				List<SalesReturnItem> lines = request.Items.Select((ReturnLineRequest i) =>
				{
					SalesOrderItem salesOrderItem = soldById[i.OrderItemId];
					return new SalesReturnItem
					{
						OrderItemId = i.OrderItemId,
						Quantity = i.Quantity,
						Restock = i.Restock,
						Amount = decimal.Round(i.Quantity * salesOrderItem.UnitPrice * factor, 2, MidpointRounding.AwayFromZero)
					};
				}).ToList();
				decimal remainingValue = order.Total - order.ReturnedTotal;
				bool allItemsReturned = sold.All((SalesOrderItem i) => alreadyReturned.GetValueOrDefault(i.Id) + request.Items.Where((ReturnLineRequest r) => r.OrderItemId == i.Id).Sum((ReturnLineRequest r) => r.Quantity) == i.Quantity);
				decimal returnTotal = (allItemsReturned ? remainingValue : Math.Min(lines.Sum((SalesReturnItem i) => i.Amount), remainingValue));
				if (lines.Count > 0 && returnTotal != lines.Sum((SalesReturnItem i) => i.Amount))
				{
					lines[lines.Count - 1].Amount += returnTotal - lines.Sum((SalesReturnItem i) => i.Amount);
				}
				if (lines.Any((SalesReturnItem i) => i.Amount < 0m))
				{
					throw new InvalidOperationException("Giá trị trả hàng không hợp lệ.");
				}
				decimal minimumRefund = Math.Max(0m, order.PaidTotal - order.RefundedTotal - (order.Total - order.ReturnedTotal - returnTotal));
				if (request.RefundAmount < minimumRefund || request.RefundAmount > returnTotal || request.RefundAmount > order.PaidTotal - order.RefundedTotal)
				{
					throw new InvalidOperationException($"Số tiền hoàn phải từ {minimumRefund} đến {Math.Min(returnTotal, order.PaidTotal - order.RefundedTotal)}.");
				}
				SalesReturn saleReturn = new SalesReturn
				{
					OrderId = orderId,
					IdempotencyKey = request.IdempotencyKey,
					Reason = request.Reason.Trim(),
					Total = returnTotal,
					RefundAmount = request.RefundAmount,
					RefundMethod = request.RefundMethod,
					CreatedBy = actorId,
					Items = lines
				};
				db.SalesReturns.Add(saleReturn);
				db.AuditEvents.Add(new AuditEvent
				{
					ActorId = actorId,
					Action = "SALE_RETURN_CREATED",
					SubjectId = saleReturn.Id
				});
				order.ReturnedTotal += returnTotal;
				order.RefundedTotal += request.RefundAmount;
				order.Status = (allItemsReturned ? SaleStatus.Returned : SaleStatus.PartiallyReturned);
				await db.SaveChangesAsync(cancellationToken);
				foreach (SalesReturnItem line2 in from i in lines
					where i.Restock
					orderby soldById[i.OrderItemId].ProductId
					select i)
				{
					Guid productId = soldById[line2.OrderItemId].ProductId;
					await using DbCommand command = db.Database.GetDbConnection().CreateCommand();
					command.Transaction = transaction.GetDbTransaction();
					command.CommandText = "UPDATE stock_balances SET \"Quantity\" = \"Quantity\" + @quantity\nWHERE \"ProductId\" = @productId\nRETURNING \"Quantity\" - @quantity, \"Quantity\"";
					AddParameter(command, "quantity", line2.Quantity);
					AddParameter(command, "productId", productId);
					decimal before;
					decimal after;
					await using (DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
					{
						if (!(await reader.ReadAsync(cancellationToken)))
						{
							throw new InvalidOperationException("Không tìm thấy tồn kho để nhập lại.");
						}
						before = reader.GetDecimal(0);
						after = reader.GetDecimal(1);
					}
					db.StockMovements.Add(new StockMovement
					{
						ProductId = productId,
						Type = "RETURN",
						Delta = line2.Quantity,
						Before = before,
						After = after,
						SourceId = line2.Id,
						Reason = "Trả hàng đơn " + order.OrderNo + ": " + saleReturn.Reason,
						PerformedBy = actorId
					});
				}
				await db.SaveChangesAsync(cancellationToken);
				await transaction.CommitAsync(cancellationToken);
				result = Map(saleReturn);
			}
		}
		return result;
	}

	private static ReturnDto Map(SalesReturn value)
	{
		return new ReturnDto(value.Id, value.OrderId, value.Total, value.RefundAmount, value.RefundMethod.ToString(), value.CreatedAt);
	}

	private static void AddParameter(DbCommand command, string name, object value)
	{
		DbParameter dbParameter = command.CreateParameter();
		dbParameter.ParameterName = name;
		dbParameter.Value = value;
		command.Parameters.Add(dbParameter);
	}
}
