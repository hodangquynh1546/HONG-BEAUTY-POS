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

public sealed class SalesRepository(PosDbContext db) : ISalesRepository
{
	public async Task<IReadOnlyList<SalesOrderDto>> ListAsync(CancellationToken cancellationToken)
	{
		return (await (from o in db.SalesOrders.AsNoTracking()
			orderby o.CreatedAt descending
			select o).Take(100).ToListAsync(cancellationToken)).Select(Map).ToList();
	}

	public async Task<SalesOrderDto> CreateAsync(CreateSaleRequest request, Guid actorId, CancellationToken cancellationToken)
	{
		SalesOrderDto result;
		await using (IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken))
		{
			await LockKeyAsync(request.IdempotencyKey, transaction, cancellationToken);
			SalesOrder existing = await db.SalesOrders.AsNoTracking().SingleOrDefaultAsync((SalesOrder o) => o.IdempotencyKey == request.IdempotencyKey, cancellationToken);
			if (existing != null)
			{
				result = Map(existing);
			}
			else
			{
				Guid[] productIds = request.Items.Select((SaleLineRequest i) => i.ProductId).ToArray();
				List<Product> products = await (from p in db.Products.AsNoTracking()
					where productIds.Contains(p.Id) && p.Active
					select p).ToListAsync(cancellationToken);
				if (products.Count != request.Items.Length)
				{
					throw new InvalidOperationException("Sản phẩm không tồn tại hoặc đã ngừng bán.");
				}
				Dictionary<Guid, Product> byId = products.ToDictionary((Product p) => p.Id);
				decimal subtotal = request.Items.Sum((SaleLineRequest i) => i.Quantity * byId[i.ProductId].SalePrice);
				decimal lineDiscount = request.Items.Sum(i => decimal.Round(i.Quantity * byId[i.ProductId].SalePrice * i.DiscountPercent / 100m, 0, MidpointRounding.AwayFromZero));
				if (request.Discount > subtotal - lineDiscount)
				{
					throw new ArgumentException("Chiết khấu vượt tổng tiền hàng.");
				}
				decimal total = subtotal - lineDiscount - request.Discount;
				SalesOrder order = new SalesOrder
				{
					OrderNo = $"BH-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".Substring(0, 32),
					IdempotencyKey = request.IdempotencyKey,
					CustomerId = request.CustomerId,
					SoldBy = actorId,
					Subtotal = subtotal,
					Discount = lineDiscount + request.Discount,
					Total = total,
					Status = ((total == 0m) ? SaleStatus.Paid : SaleStatus.Unpaid),
					Items = request.Items.Select((SaleLineRequest i) =>
					{
						Product product = byId[i.ProductId];
						return new SalesOrderItem
						{
							ProductId = product.Id,
							SkuSnapshot = product.Sku,
							NameSnapshot = product.Name,
							Quantity = i.Quantity,
							UnitPrice = product.SalePrice,
							DiscountPercent = i.DiscountPercent,
							DiscountAmount = decimal.Round(i.Quantity * product.SalePrice * i.DiscountPercent / 100m, 0, MidpointRounding.AwayFromZero),
							UnitCost = product.CostPrice
						};
					}).ToList()
				};
				db.SalesOrders.Add(order);
				await db.SaveChangesAsync(cancellationToken);
				foreach (SalesOrderItem item in order.Items.OrderBy((SalesOrderItem i) => i.ProductId))
				{
					(decimal Before, decimal After)? amounts = await ChangeStockAsync(item.ProductId, -item.Quantity, transaction, cancellationToken);
					if (!amounts.HasValue)
					{
						throw new InvalidOperationException("Không đủ tồn kho cho " + item.NameSnapshot + ".");
					}
					db.StockMovements.Add(new StockMovement
					{
						ProductId = item.ProductId,
						Type = "SALE",
						Delta = -item.Quantity,
						Before = amounts.Value.Before,
						After = amounts.Value.After,
						SourceId = item.Id,
						Reason = "Bán hàng " + order.OrderNo,
						PerformedBy = actorId
					});
				}
				await db.SaveChangesAsync(cancellationToken);
				await transaction.CommitAsync(cancellationToken);
				result = Map(order);
			}
		}
		return result;
	}

	public async Task<PaymentDto?> AddPaymentAsync(Guid orderId, AddPaymentRequest request, Guid actorId, CancellationToken cancellationToken)
	{
		PaymentDto result;
		await using (IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken))
		{
			await LockKeyAsync(request.IdempotencyKey, transaction, cancellationToken);
			Payment existing = await db.Payments.AsNoTracking().SingleOrDefaultAsync((Payment p) => p.IdempotencyKey == request.IdempotencyKey, cancellationToken);
			PaymentDto paymentDto;
			if (existing != null)
			{
				if (existing.OrderId != orderId)
				{
					throw new InvalidOperationException("Khóa giao dịch đã dùng cho đơn khác.");
				}
				SalesOrder existingOrder = await db.SalesOrders.AsNoTracking().SingleAsync((SalesOrder o) => o.Id == orderId, cancellationToken);
				result = new PaymentDto(existing.Id, orderId, existing.Method.ToString(), existing.Amount, existingOrder.PaidTotal, existingOrder.Status.ToString());
			}
			else
			{
				await using (DbCommand lockCommand = db.Database.GetDbConnection().CreateCommand())
				{
					lockCommand.Transaction = transaction.GetDbTransaction();
					lockCommand.CommandText = "SELECT \"Id\" FROM sales_orders WHERE \"Id\" = @id FOR UPDATE";
					AddParameter(lockCommand, "id", orderId);
					if (await lockCommand.ExecuteScalarAsync(cancellationToken) == null)
					{
						paymentDto = null;
						goto IL_0ab1;
					}
				}
				SalesOrder order = await db.SalesOrders.SingleAsync((SalesOrder o) => o.Id == orderId, cancellationToken);
				SaleStatus status = order.Status;
				if (((status == SaleStatus.Cancelled || status == SaleStatus.Returned) ? 1 : 0) != 0)
				{
					result = null;
				}
				else if (order.PaidTotal - order.RefundedTotal + request.Amount > order.Total - order.ReturnedTotal)
				{
					result = null;
				}
				else
				{
					Payment payment = new Payment
					{
						OrderId = orderId,
						IdempotencyKey = request.IdempotencyKey,
						Method = request.Method,
						Amount = request.Amount,
						ReceivedBy = actorId
					};
					db.Payments.Add(payment);
					order.PaidTotal += request.Amount;
					if (order.Status != SaleStatus.PartiallyReturned)
					{
						order.Status = ((!(order.PaidTotal == order.Total)) ? SaleStatus.PartiallyPaid : SaleStatus.Paid);
					}
					await db.SaveChangesAsync(cancellationToken);
					await transaction.CommitAsync(cancellationToken);
					result = new PaymentDto(payment.Id, orderId, payment.Method.ToString(), payment.Amount, order.PaidTotal, order.Status.ToString());
				}
			}
			goto end_IL_0162;
			IL_0ab1:
			result = paymentDto;
			end_IL_0162:;
		}
		return result;
	}

	private async Task LockKeyAsync(Guid key, IDbContextTransaction transaction, CancellationToken cancellationToken)
	{
		await using DbCommand command = db.Database.GetDbConnection().CreateCommand();
		command.Transaction = transaction.GetDbTransaction();
		command.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))";
		AddParameter(command, "key", key.ToString());
		await command.ExecuteScalarAsync(cancellationToken);
	}

	private async Task<(decimal Before, decimal After)?> ChangeStockAsync(Guid productId, decimal delta, IDbContextTransaction transaction, CancellationToken cancellationToken)
	{
		(decimal Before, decimal After)? result;
		await using (DbCommand command = db.Database.GetDbConnection().CreateCommand())
		{
			command.Transaction = transaction.GetDbTransaction();
			command.CommandText = "UPDATE stock_balances SET \"Quantity\" = \"Quantity\" + @delta\nWHERE \"ProductId\" = @productId AND \"Quantity\" + @delta >= 0\nRETURNING \"Quantity\" - @delta, \"Quantity\"";
			AddParameter(command, "delta", delta);
			AddParameter(command, "productId", productId);
			(decimal Before, decimal After)? tuple;
			await using (DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
			{
				tuple = ((await reader.ReadAsync(cancellationToken)) ? new (decimal, decimal)?((reader.GetDecimal(0), reader.GetDecimal(1))) : (((decimal, decimal)?)null));
			}
			result = tuple;
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

	private static SalesOrderDto Map(SalesOrder order)
	{
		return new SalesOrderDto(order.Id, order.OrderNo, order.CustomerId, order.Status.ToString(), order.Total, order.PaidTotal, order.CreatedAt, order.ReturnedTotal, order.RefundedTotal);
	}
}
