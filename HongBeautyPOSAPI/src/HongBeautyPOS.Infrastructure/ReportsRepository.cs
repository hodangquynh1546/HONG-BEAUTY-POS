using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using HongBeautyPOS.Application;
using HongBeautyPOS.Domain;
using Microsoft.EntityFrameworkCore;

namespace HongBeautyPOS.Infrastructure;

public sealed class ReportsRepository(PosDbContext db) : IReportsRepository
{
	public async Task<ReportSummaryDto> SummaryAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
	{
		TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
		DateTimeOffset start = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), zone));
		DateTimeOffset end = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), zone));
		IQueryable<SalesOrder> sales = from o in db.SalesOrders.AsNoTracking()
			where o.CreatedAt >= start && o.CreatedAt < end && (int)o.Status != 3
			select o;
		IQueryable<SalesReturn> returns = from r in db.SalesReturns.AsNoTracking()
			where r.CreatedAt >= start && r.CreatedAt < end
			select r;
		IQueryable<Payment> payments = from p in db.Payments.AsNoTracking()
			where p.ReceivedAt >= start && p.ReceivedAt < end
			select p;
		IQueryable<SalesOrderItem> soldItems = from item in db.SalesOrderItems.AsNoTracking()
			join order in sales on item.OrderId equals order.Id
			select item;
		var restockedItems = from item in db.SalesReturnItems.AsNoTracking()
			join ret in returns on item.ReturnId equals ret.Id
			join sold in db.SalesOrderItems.AsNoTracking() on item.OrderItemId equals sold.Id
			where item.Restock
			select new { item.Quantity, sold.UnitCost };
		decimal gross = (await sales.SumAsync((Expression<Func<SalesOrder, decimal?>>)((SalesOrder o) => o.Total), cancellationToken)).GetValueOrDefault();
		decimal returned = (await returns.SumAsync((Expression<Func<SalesReturn, decimal?>>)((SalesReturn r) => r.Total), cancellationToken)).GetValueOrDefault();
		decimal received = (await payments.SumAsync((Expression<Func<Payment, decimal?>>)((Payment p) => p.Amount), cancellationToken)).GetValueOrDefault();
		decimal refunded = (await returns.SumAsync((Expression<Func<SalesReturn, decimal?>>)((SalesReturn r) => r.RefundAmount), cancellationToken)).GetValueOrDefault();
		IQueryable<CashEntry> entries = from e in db.CashEntries.AsNoTracking()
			where e.CreatedAt >= start && e.CreatedAt < end
			select e;
		decimal otherIncome = (await entries.Where((CashEntry e) => (int)e.Kind == 0).SumAsync((Expression<Func<CashEntry, decimal?>>)((CashEntry e) => e.Amount), cancellationToken)).GetValueOrDefault();
		decimal operatingExpenses = (await entries.Where((CashEntry e) => (int)e.Kind == 1).SumAsync((Expression<Func<CashEntry, decimal?>>)((CashEntry e) => e.Amount), cancellationToken)).GetValueOrDefault();
		decimal supplierPayments = (await entries.Where((CashEntry e) => (int)e.Kind == 2).SumAsync((Expression<Func<CashEntry, decimal?>>)((CashEntry e) => e.Amount), cancellationToken)).GetValueOrDefault();
		decimal otherExpenses = (await entries.Where((CashEntry e) => (int)e.Kind == 3).SumAsync((Expression<Func<CashEntry, decimal?>>)((CashEntry e) => e.Amount), cancellationToken)).GetValueOrDefault();
		decimal soldCost = (await soldItems.SumAsync((Expression<Func<SalesOrderItem, decimal?>>)((SalesOrderItem i) => i.Quantity * i.UnitCost), cancellationToken)).GetValueOrDefault();
		decimal returnedCost = (await restockedItems.SumAsync(i => (decimal?)(i.Quantity * i.UnitCost), cancellationToken)).GetValueOrDefault();
		decimal inventory = (await db.StockBalances.AsNoTracking().Join((IEnumerable<Product>)db.Products.AsNoTracking(), (Expression<Func<StockBalance, Guid>>)((StockBalance balance) => balance.ProductId), (Expression<Func<Product, Guid>>)((Product product) => product.Id), (Expression<Func<StockBalance, Product, decimal?>>)((StockBalance balance, Product product) => balance.Quantity * product.CostPrice)).SumAsync(cancellationToken)).GetValueOrDefault();
		int lowStock = await db.Products.AsNoTracking().CountAsync((Product p) => p.Active && p.Balance != null && p.Balance.Quantity > 0m && p.Balance.Quantity <= p.ReorderLevel, cancellationToken);
		int outOfStock = await db.Products.AsNoTracking().CountAsync((Product p) => p.Active && (p.Balance == null || p.Balance.Quantity <= 0m), cancellationToken);
		int count = await sales.CountAsync(cancellationToken);
		decimal units = (await soldItems.SumAsync((Expression<Func<SalesOrderItem, decimal?>>)((SalesOrderItem i) => i.Quantity), cancellationToken)).GetValueOrDefault();
		decimal cost = soldCost - returnedCost;
		return new ReportSummaryDto(from, to, gross, returned, gross - returned, received, refunded, received - refunded + otherIncome - operatingExpenses - supplierPayments - otherExpenses, cost, gross - returned - cost, inventory, count, units, lowStock, outOfStock, otherIncome, operatingExpenses, supplierPayments, otherExpenses, gross - returned - cost - operatingExpenses - otherExpenses);
	}
}
