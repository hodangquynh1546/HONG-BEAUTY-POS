using HongBeautyPOS.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HongBeautyPOS.Infrastructure;

public sealed class PosDbContext : DbContext
{
	public DbSet<User> Users => Set<User>();

	public DbSet<EmployeePermission> EmployeePermissions => Set<EmployeePermission>();

	public DbSet<Category> Categories => Set<Category>();

	public DbSet<Product> Products => Set<Product>();

	public DbSet<AttributeOption> AttributeOptions => Set<AttributeOption>();

	public DbSet<ProductAttributeSelection> ProductAttributeSelections => Set<ProductAttributeSelection>();

	public DbSet<StockBalance> StockBalances => Set<StockBalance>();

	public DbSet<StockMovement> StockMovements => Set<StockMovement>();

	public DbSet<StocktakeSession> StocktakeSessions => Set<StocktakeSession>();

	public DbSet<StocktakeItem> StocktakeItems => Set<StocktakeItem>();

	public DbSet<Supplier> Suppliers => Set<Supplier>();

	public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();

	public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();

	public DbSet<Customer> Customers => Set<Customer>();

	public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();

	public DbSet<SalesOrderItem> SalesOrderItems => Set<SalesOrderItem>();

	public DbSet<Payment> Payments => Set<Payment>();

	public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();

	public DbSet<SalesReturnItem> SalesReturnItems => Set<SalesReturnItem>();

	public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

	public DbSet<CashEntry> CashEntries => Set<CashEntry>();

	public PosDbContext(DbContextOptions<PosDbContext> options)
		: base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder model)
	{
		model.Entity((EntityTypeBuilder<User> e) =>
		{
			e.ToTable("users");
			e.HasKey((User x) => x.Id);
			e.HasIndex((User x) => x.Email).IsUnique();
			e.HasIndex((User x) => x.Role).IsUnique().HasFilter("\"Role\" = 'Owner'");
			e.Property((User x) => x.Role).HasConversion<string>().HasMaxLength(16);
			e.Property((User x) => x.Email).HasMaxLength(320);
			e.Property((User x) => x.FullName).HasMaxLength(200);
			e.HasMany((User x) => x.Permissions).WithOne().HasForeignKey((EmployeePermission x) => x.UserId);
		});
		model.Entity((EntityTypeBuilder<EmployeePermission> e) =>
		{
			e.ToTable("employee_permissions");
			e.HasKey((EmployeePermission x) => new { x.UserId, x.Permission });
			e.Property((EmployeePermission x) => x.Permission).HasMaxLength(80);
		});
		model.Entity((EntityTypeBuilder<Category> e) =>
		{
			e.ToTable("categories");
			e.HasKey((Category x) => x.Id);
			e.Property((Category x) => x.Name).HasMaxLength(160);
			e.HasOne<Category>().WithMany().HasForeignKey((Category x) => x.ParentId);
		});
		model.Entity((EntityTypeBuilder<Product> e) =>
		{
			e.ToTable("products", (TableBuilder<Product> table) =>
			{
				table.HasCheckConstraint("CK_products_cost_price", "\"CostPrice\" >= 0");
				table.HasCheckConstraint("CK_products_sale_price", "\"SalePrice\" >= 0");
			});
			e.HasKey((Product x) => x.Id);
			e.HasIndex((Product x) => x.Sku).IsUnique();
			e.HasIndex((Product x) => x.Barcode).IsUnique();
			e.Property((Product x) => x.Sku).HasMaxLength(100);
			e.Property((Product x) => x.Name).HasMaxLength(240);
			e.Property((Product x) => x.ImageData).HasColumnType("text");
			e.Property((Product x) => x.CostPrice).HasPrecision(18, 2);
			e.Property((Product x) => x.SalePrice).HasPrecision(18, 2);
			e.Property((Product x) => x.ReorderLevel).HasPrecision(18, 3);
			e.HasOne<Category>().WithMany().HasForeignKey((Product x) => x.CategoryId);
			e.HasOne((Product x) => x.Balance).WithOne().HasForeignKey((StockBalance x) => x.ProductId);
			e.HasMany((Product x) => x.AttributeSelections).WithOne().HasForeignKey((ProductAttributeSelection x) => x.ProductId);
		});
		model.Entity((EntityTypeBuilder<AttributeOption> e) =>
		{
			e.ToTable("attribute_options");
			e.HasKey(x => x.Id);
			e.Property(x => x.Name).HasMaxLength(80);
			e.Property(x => x.Value).HasMaxLength(100);
			e.Property(x => x.NameKey).HasMaxLength(80);
			e.Property(x => x.ValueKey).HasMaxLength(100);
			e.HasIndex(x => new { x.NameKey, x.ValueKey }).IsUnique();
		});
		model.Entity((EntityTypeBuilder<ProductAttributeSelection> e) =>
		{
			e.ToTable("product_attribute_selections");
			e.HasKey(x => new { x.ProductId, x.OptionId });
			e.HasOne<AttributeOption>().WithMany().HasForeignKey(x => x.OptionId).OnDelete(DeleteBehavior.Restrict);
			e.HasOne<Product>().WithMany(x => x.AttributeSelections).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
		});
		model.Entity((EntityTypeBuilder<CashEntry> e) =>
		{
			e.ToTable("cash_entries", (TableBuilder<CashEntry> table) =>
			{
				table.HasCheckConstraint("CK_cash_entries_amount", "\"Amount\" > 0");
			});
			e.HasKey((CashEntry x) => x.Id);
			e.HasIndex((CashEntry x) => x.IdempotencyKey).IsUnique();
			e.HasIndex((CashEntry x) => x.CreatedAt);
			e.Property((CashEntry x) => x.Kind).HasConversion<string>().HasMaxLength(32);
			e.Property((CashEntry x) => x.Method).HasConversion<string>().HasMaxLength(24);
			e.Property((CashEntry x) => x.Amount).HasPrecision(18, 2);
			e.Property((CashEntry x) => x.Description).HasMaxLength(500);
			e.HasOne<User>().WithMany().HasForeignKey((CashEntry x) => x.CreatedBy)
				.OnDelete(DeleteBehavior.Restrict);
		});
		model.Entity((EntityTypeBuilder<StockBalance> e) =>
		{
			e.ToTable("stock_balances", (TableBuilder<StockBalance> table) =>
			{
				table.HasCheckConstraint("CK_stock_balances_quantity", "\"Quantity\" >= 0");
			});
			e.HasKey((StockBalance x) => x.ProductId);
			e.Property((StockBalance x) => x.Quantity).HasPrecision(18, 3);
			e.Property((StockBalance x) => x.Version).IsRowVersion();
		});
		model.Entity((EntityTypeBuilder<StockMovement> e) =>
		{
			e.ToTable("stock_movements", (TableBuilder<StockMovement> table) =>
			{
				table.HasCheckConstraint("CK_stock_movements_balance", "\"After\" = \"Before\" + \"Delta\"");
			});
			e.HasKey((StockMovement x) => x.Id);
			e.HasOne<Product>().WithMany().HasForeignKey((StockMovement x) => x.ProductId)
				.OnDelete(DeleteBehavior.Restrict);
			e.HasOne<User>().WithMany().HasForeignKey((StockMovement x) => x.PerformedBy)
				.OnDelete(DeleteBehavior.Restrict);
			e.Property((StockMovement x) => x.Delta).HasPrecision(18, 3);
			e.Property((StockMovement x) => x.Before).HasPrecision(18, 3);
			e.Property((StockMovement x) => x.After).HasPrecision(18, 3);
			e.Property((StockMovement x) => x.Reason).HasMaxLength(500);
			e.HasIndex((StockMovement x) => new { x.ProductId, x.CreatedAt });
		});
		model.Entity((EntityTypeBuilder<StocktakeSession> e) =>
		{
			e.ToTable("stocktake_sessions");
			e.HasKey((StocktakeSession x) => x.Id);
			e.Property((StocktakeSession x) => x.Status).HasConversion<string>().HasMaxLength(16);
			e.HasOne<User>().WithMany().HasForeignKey((StocktakeSession x) => x.CreatedBy)
				.OnDelete(DeleteBehavior.Restrict);
			e.HasMany((StocktakeSession x) => x.Items).WithOne().HasForeignKey((StocktakeItem x) => x.SessionId);
			e.HasIndex((StocktakeSession x) => x.CreatedAt);
		});
		model.Entity((EntityTypeBuilder<StocktakeItem> e) =>
		{
			e.ToTable("stocktake_items", (TableBuilder<StocktakeItem> table) =>
			{
				table.HasCheckConstraint("CK_stocktake_items_expected", "\"ExpectedQuantity\" >= 0");
				table.HasCheckConstraint("CK_stocktake_items_actual", "\"ActualQuantity\" IS NULL OR \"ActualQuantity\" >= 0");
			});
			e.HasKey((StocktakeItem x) => x.Id);
			e.HasIndex((StocktakeItem x) => new { x.SessionId, x.ProductId }).IsUnique();
			e.Property((StocktakeItem x) => x.ExpectedQuantity).HasPrecision(18, 3);
			e.Property((StocktakeItem x) => x.ActualQuantity).HasPrecision(18, 3);
			e.HasOne<Product>().WithMany().HasForeignKey((StocktakeItem x) => x.ProductId)
				.OnDelete(DeleteBehavior.Restrict);
		});
		model.Entity((EntityTypeBuilder<Supplier> e) =>
		{
			e.ToTable("suppliers");
			e.HasKey((Supplier x) => x.Id);
			e.HasIndex((Supplier x) => x.Code).IsUnique();
			e.Property((Supplier x) => x.Code).HasMaxLength(80);
			e.Property((Supplier x) => x.Name).HasMaxLength(200);
			e.Property((Supplier x) => x.Phone).HasMaxLength(40);
			e.Property((Supplier x) => x.Email).HasMaxLength(320);
		});
		model.Entity((EntityTypeBuilder<PurchaseReceipt> e) =>
		{
			e.ToTable("purchase_receipts", (TableBuilder<PurchaseReceipt> table) =>
			{
				table.HasCheckConstraint("CK_purchase_receipts_total", "\"Total\" >= 0");
			});
			e.HasKey((PurchaseReceipt x) => x.Id);
			e.HasIndex((PurchaseReceipt x) => x.ReceiptNo).IsUnique();
			e.Property((PurchaseReceipt x) => x.ReceiptNo).HasMaxLength(80);
			e.Property((PurchaseReceipt x) => x.Status).HasConversion<string>().HasMaxLength(16);
			e.Property((PurchaseReceipt x) => x.Total).HasPrecision(18, 2);
			e.HasOne<Supplier>().WithMany().HasForeignKey((PurchaseReceipt x) => x.SupplierId)
				.OnDelete(DeleteBehavior.Restrict);
			e.HasOne<User>().WithMany().HasForeignKey((PurchaseReceipt x) => x.CreatedBy)
				.OnDelete(DeleteBehavior.Restrict);
			e.HasMany((PurchaseReceipt x) => x.Items).WithOne().HasForeignKey((PurchaseItem x) => x.ReceiptId);
		});
		model.Entity((EntityTypeBuilder<PurchaseItem> e) =>
		{
			e.ToTable("purchase_items", (TableBuilder<PurchaseItem> table) =>
			{
				table.HasCheckConstraint("CK_purchase_items_quantity", "\"Quantity\" > 0");
				table.HasCheckConstraint("CK_purchase_items_unit_cost", "\"UnitCost\" >= 0");
			});
			e.HasKey((PurchaseItem x) => x.Id);
			e.HasIndex((PurchaseItem x) => new { x.ReceiptId, x.ProductId }).IsUnique();
			e.Property((PurchaseItem x) => x.Quantity).HasPrecision(18, 3);
			e.Property((PurchaseItem x) => x.UnitCost).HasPrecision(18, 2);
			e.HasOne<Product>().WithMany().HasForeignKey((PurchaseItem x) => x.ProductId)
				.OnDelete(DeleteBehavior.Restrict);
		});
		model.Entity((EntityTypeBuilder<Customer> e) =>
		{
			e.ToTable("customers");
			e.HasKey((Customer x) => x.Id);
			e.Property((Customer x) => x.FullName).HasMaxLength(200);
			e.Property((Customer x) => x.Phone).HasMaxLength(40);
			e.Property((Customer x) => x.Email).HasMaxLength(320);
			e.HasIndex((Customer x) => x.Phone);
		});
		model.Entity((EntityTypeBuilder<SalesOrder> e) =>
		{
			e.ToTable("sales_orders", (TableBuilder<SalesOrder> table) =>
			{
				table.HasCheckConstraint("CK_sales_orders_total", "\"Subtotal\" >= 0 AND \"Discount\" >= 0 AND \"Total\" >= 0");
				table.HasCheckConstraint("CK_sales_orders_paid", "\"PaidTotal\" >= 0 AND \"PaidTotal\" <= \"Total\"");
				table.HasCheckConstraint("CK_sales_orders_returned", "\"ReturnedTotal\" >= 0 AND \"ReturnedTotal\" <= \"Total\" AND \"RefundedTotal\" >= 0 AND \"RefundedTotal\" <= \"PaidTotal\"");
			});
			e.HasKey((SalesOrder x) => x.Id);
			e.HasIndex((SalesOrder x) => x.OrderNo).IsUnique();
			e.HasIndex((SalesOrder x) => x.IdempotencyKey).IsUnique();
			e.HasIndex((SalesOrder x) => x.CreatedAt);
			e.Property((SalesOrder x) => x.OrderNo).HasMaxLength(80);
			e.Property((SalesOrder x) => x.Status).HasConversion<string>().HasMaxLength(24);
			e.Property((SalesOrder x) => x.Subtotal).HasPrecision(18, 2);
			e.Property((SalesOrder x) => x.Discount).HasPrecision(18, 2);
			e.Property((SalesOrder x) => x.Total).HasPrecision(18, 2);
			e.Property((SalesOrder x) => x.PaidTotal).HasPrecision(18, 2);
			e.Property((SalesOrder x) => x.ReturnedTotal).HasPrecision(18, 2);
			e.Property((SalesOrder x) => x.RefundedTotal).HasPrecision(18, 2);
			e.HasOne<Customer>().WithMany().HasForeignKey((SalesOrder x) => x.CustomerId)
				.OnDelete(DeleteBehavior.Restrict);
			e.HasOne<User>().WithMany().HasForeignKey((SalesOrder x) => x.SoldBy)
				.OnDelete(DeleteBehavior.Restrict);
			e.HasMany((SalesOrder x) => x.Items).WithOne().HasForeignKey((SalesOrderItem x) => x.OrderId);
		});
		model.Entity((EntityTypeBuilder<SalesOrderItem> e) =>
		{
			e.ToTable("sales_order_items", (TableBuilder<SalesOrderItem> table) =>
			{
				table.HasCheckConstraint("CK_sales_order_items_quantity", "\"Quantity\" > 0");
			});
			e.HasKey((SalesOrderItem x) => x.Id);
			e.HasIndex((SalesOrderItem x) => new { x.OrderId, x.ProductId }).IsUnique();
			e.Property((SalesOrderItem x) => x.SkuSnapshot).HasMaxLength(100);
			e.Property((SalesOrderItem x) => x.NameSnapshot).HasMaxLength(240);
			e.Property((SalesOrderItem x) => x.Quantity).HasPrecision(18, 3);
			e.Property((SalesOrderItem x) => x.UnitPrice).HasPrecision(18, 2);
			e.Property((SalesOrderItem x) => x.UnitCost).HasPrecision(18, 2);
			e.HasOne<Product>().WithMany().HasForeignKey((SalesOrderItem x) => x.ProductId)
				.OnDelete(DeleteBehavior.Restrict);
		});
		model.Entity((EntityTypeBuilder<Payment> e) =>
		{
			e.ToTable("payments", (TableBuilder<Payment> table) =>
			{
				table.HasCheckConstraint("CK_payments_amount", "\"Amount\" > 0");
			});
			e.HasKey((Payment x) => x.Id);
			e.HasIndex((Payment x) => x.IdempotencyKey).IsUnique();
			e.Property((Payment x) => x.Method).HasConversion<string>().HasMaxLength(24);
			e.Property((Payment x) => x.Amount).HasPrecision(18, 2);
			e.HasOne<SalesOrder>().WithMany().HasForeignKey((Payment x) => x.OrderId)
				.OnDelete(DeleteBehavior.Restrict);
			e.HasOne<User>().WithMany().HasForeignKey((Payment x) => x.ReceivedBy)
				.OnDelete(DeleteBehavior.Restrict);
		});
		model.Entity((EntityTypeBuilder<SalesReturn> e) =>
		{
			e.ToTable("sales_returns", (TableBuilder<SalesReturn> table) =>
			{
				table.HasCheckConstraint("CK_sales_returns_total", "\"Total\" >= 0 AND \"RefundAmount\" >= 0 AND \"RefundAmount\" <= \"Total\"");
			});
			e.HasKey((SalesReturn x) => x.Id);
			e.HasIndex((SalesReturn x) => x.IdempotencyKey).IsUnique();
			e.HasIndex((SalesReturn x) => new { x.OrderId, x.CreatedAt });
			e.Property((SalesReturn x) => x.Reason).HasMaxLength(500);
			e.Property((SalesReturn x) => x.Total).HasPrecision(18, 2);
			e.Property((SalesReturn x) => x.RefundAmount).HasPrecision(18, 2);
			e.Property((SalesReturn x) => x.RefundMethod).HasConversion<string>().HasMaxLength(24);
			e.HasOne<SalesOrder>().WithMany().HasForeignKey((SalesReturn x) => x.OrderId)
				.OnDelete(DeleteBehavior.Restrict);
			e.HasOne<User>().WithMany().HasForeignKey((SalesReturn x) => x.CreatedBy)
				.OnDelete(DeleteBehavior.Restrict);
			e.HasMany((SalesReturn x) => x.Items).WithOne().HasForeignKey((SalesReturnItem x) => x.ReturnId);
		});
		model.Entity((EntityTypeBuilder<SalesReturnItem> e) =>
		{
			e.ToTable("sales_return_items", (TableBuilder<SalesReturnItem> table) =>
			{
				table.HasCheckConstraint("CK_sales_return_items_quantity", "\"Quantity\" > 0");
				table.HasCheckConstraint("CK_sales_return_items_amount", "\"Amount\" >= 0");
			});
			e.HasKey((SalesReturnItem x) => x.Id);
			e.HasIndex((SalesReturnItem x) => new { x.ReturnId, x.OrderItemId }).IsUnique();
			e.Property((SalesReturnItem x) => x.Quantity).HasPrecision(18, 3);
			e.Property((SalesReturnItem x) => x.Amount).HasPrecision(18, 2);
			e.HasOne<SalesOrderItem>().WithMany().HasForeignKey((SalesReturnItem x) => x.OrderItemId)
				.OnDelete(DeleteBehavior.Restrict);
		});
		model.Entity((EntityTypeBuilder<AuditEvent> e) =>
		{
			e.ToTable("audit_events");
			e.HasKey((AuditEvent x) => x.Id);
			e.Property((AuditEvent x) => x.Action).HasMaxLength(100);
			e.HasOne<User>().WithMany().HasForeignKey((AuditEvent x) => x.ActorId)
				.OnDelete(DeleteBehavior.Restrict);
			e.HasIndex((AuditEvent x) => x.CreatedAt);
			e.HasIndex((AuditEvent x) => new { x.SubjectId, x.CreatedAt });
		});
	}
}
