using System;

namespace HongBeautyPOS.Application;

public sealed record ReportSummaryDto(DateOnly From, DateOnly To, decimal GrossSales, decimal ReturnedSales, decimal NetRevenue, decimal CashReceived, decimal CashRefunded, decimal NetCashFlow, decimal CostOfGoods, decimal EstimatedGrossProfit, decimal InventoryValue, int OrderCount, decimal UnitsSold, int LowStockProducts, int OutOfStockProducts, decimal OtherIncome, decimal OperatingExpenses, decimal SupplierPayments, decimal OtherExpenses, decimal EstimatedNetProfit);
