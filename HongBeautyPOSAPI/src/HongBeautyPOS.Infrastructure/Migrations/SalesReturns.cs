using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace HongBeautyPOS.Infrastructure.Migrations;

[DbContext(typeof(PosDbContext))]
[Migration("20261106041559_SalesReturns")]
public class SalesReturns : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		int? precision = 18;
		int? scale = 2;
		migrationBuilder.AddColumn<decimal>("RefundedTotal", "sales_orders", "numeric(18,2)", null, null, rowVersion: false, null, nullable: false, 0m, null, null, null, null, null, precision, scale);
		scale = 18;
		precision = 2;
		migrationBuilder.AddColumn<decimal>("ReturnedTotal", "sales_orders", "numeric(18,2)", null, null, rowVersion: false, null, nullable: false, 0m, null, null, null, null, null, scale, precision);
		migrationBuilder.CreateTable("sales_returns", (ColumnsBuilder table) =>
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> orderId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> idempotencyKey = table.Column<Guid>("uuid");
			int? maxLength = 500;
			OperationBuilder<AddColumnOperation> reason = table.Column<string>("character varying(500)", null, maxLength);
			maxLength = 18;
			int? scale2 = 2;
			OperationBuilder<AddColumnOperation> total = table.Column<decimal>("numeric(18,2)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, maxLength, scale2);
			scale2 = 18;
			maxLength = 2;
			OperationBuilder<AddColumnOperation> refundAmount = table.Column<decimal>("numeric(18,2)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, scale2, maxLength);
			maxLength = 24;
			return new
			{
				Id = id,
				OrderId = orderId,
				IdempotencyKey = idempotencyKey,
				Reason = reason,
				Total = total,
				RefundAmount = refundAmount,
				RefundMethod = table.Column<string>("character varying(24)", null, maxLength),
				CreatedBy = table.Column<Guid>("uuid"),
				CreatedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_sales_returns", x => x.Id);
			table.CheckConstraint("CK_sales_returns_total", "\"Total\" >= 0 AND \"RefundAmount\" >= 0 AND \"RefundAmount\" <= \"Total\"");
			table.ForeignKey("FK_sales_returns_sales_orders_OrderId", x => x.OrderId, "sales_orders", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_sales_returns_users_CreatedBy", x => x.CreatedBy, "users", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("sales_return_items", (ColumnsBuilder table) =>
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> returnId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> orderItemId = table.Column<Guid>("uuid");
			int? precision2 = 18;
			int? scale2 = 3;
			OperationBuilder<AddColumnOperation> quantity = table.Column<decimal>("numeric(18,3)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, precision2, scale2);
			OperationBuilder<AddColumnOperation> restock = table.Column<bool>("boolean");
			scale2 = 18;
			precision2 = 2;
			return new
			{
				Id = id,
				ReturnId = returnId,
				OrderItemId = orderItemId,
				Quantity = quantity,
				Restock = restock,
				Amount = table.Column<decimal>("numeric(18,2)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, scale2, precision2)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_sales_return_items", x => x.Id);
			table.CheckConstraint("CK_sales_return_items_amount", "\"Amount\" >= 0");
			table.CheckConstraint("CK_sales_return_items_quantity", "\"Quantity\" > 0");
			table.ForeignKey("FK_sales_return_items_sales_order_items_OrderItemId", x => x.OrderItemId, "sales_order_items", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_sales_return_items_sales_returns_ReturnId", x => x.ReturnId, "sales_returns", "Id", null, ReferentialAction.NoAction, ReferentialAction.Cascade);
		});
		migrationBuilder.AddCheckConstraint("CK_sales_orders_returned", "sales_orders", "\"ReturnedTotal\" >= 0 AND \"ReturnedTotal\" <= \"Total\" AND \"RefundedTotal\" >= 0 AND \"RefundedTotal\" <= \"PaidTotal\"");
		migrationBuilder.CreateIndex("IX_sales_return_items_OrderItemId", "sales_return_items", "OrderItemId");
		migrationBuilder.CreateIndex("IX_sales_return_items_ReturnId_OrderItemId", "sales_return_items", new string[2] { "ReturnId", "OrderItemId" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_sales_returns_CreatedBy", "sales_returns", "CreatedBy");
		migrationBuilder.CreateIndex("IX_sales_returns_IdempotencyKey", "sales_returns", "IdempotencyKey", null, unique: true);
		migrationBuilder.CreateIndex("IX_sales_returns_OrderId_CreatedAt", "sales_returns", new string[2] { "OrderId", "CreatedAt" });
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable("sales_return_items");
		migrationBuilder.DropTable("sales_returns");
		migrationBuilder.DropCheckConstraint("CK_sales_orders_returned", "sales_orders");
		migrationBuilder.DropColumn("RefundedTotal", "sales_orders");
		migrationBuilder.DropColumn("ReturnedTotal", "sales_orders");
	}

	protected override void BuildTargetModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "10.0.4").HasAnnotation("Relational:MaxIdentifierLength", 63);
		modelBuilder.UseIdentityByDefaultColumns();
		modelBuilder.Entity("HongBeautyPOS.Domain.Category", (EntityTypeBuilder b) =>
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<bool>("Active").HasColumnType("boolean");
			b.Property<string>("Name").IsRequired().HasMaxLength(160)
				.HasColumnType("character varying(160)");
			b.Property<Guid?>("ParentId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("ParentId");
			b.ToTable("categories", (string?)null);
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.Customer", (EntityTypeBuilder b) =>
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<bool>("Active").HasColumnType("boolean");
			b.Property<string>("Email").HasMaxLength(320).HasColumnType("character varying(320)");
			b.Property<string>("FullName").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("Phone").HasMaxLength(40).HasColumnType("character varying(40)");
			b.HasKey("Id");
			b.HasIndex("Phone");
			b.ToTable("customers", (string?)null);
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.EmployeePermission", (EntityTypeBuilder b) =>
		{
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<string>("Permission").HasMaxLength(80).HasColumnType("character varying(80)");
			b.Property<DateTimeOffset>("GrantedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("GrantedBy").HasColumnType("uuid");
			b.HasKey("UserId", "Permission");
			b.ToTable("employee_permissions", (string?)null);
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.Payment", (EntityTypeBuilder b) =>
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<decimal>("Amount").HasPrecision(18, 2).HasColumnType("numeric(18,2)");
			b.Property<Guid>("IdempotencyKey").HasColumnType("uuid");
			b.Property<string>("Method").IsRequired().HasMaxLength(24)
				.HasColumnType("character varying(24)");
			b.Property<Guid>("OrderId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("ReceivedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("ReceivedBy").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("IdempotencyKey").IsUnique();
			b.HasIndex("OrderId");
			b.HasIndex("ReceivedBy");
			b.ToTable("payments", null, (TableBuilder t) =>
			{
				t.HasCheckConstraint("CK_payments_amount", "\"Amount\" > 0");
			});
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.Product", (EntityTypeBuilder b) =>
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<bool>("Active").HasColumnType("boolean");
			b.Property<string>("Barcode").HasColumnType("text");
			b.Property<Guid?>("CategoryId").HasColumnType("uuid");
			b.Property<decimal>("CostPrice").HasPrecision(18, 2).HasColumnType("numeric(18,2)");
			b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Name").IsRequired().HasMaxLength(240)
				.HasColumnType("character varying(240)");
			b.Property<decimal>("ReorderLevel").HasPrecision(18, 3).HasColumnType("numeric(18,3)");
			b.Property<decimal>("SalePrice").HasPrecision(18, 2).HasColumnType("numeric(18,2)");
			b.Property<string>("Sku").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("Unit").IsRequired().HasColumnType("text");
			b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.HasKey("Id");
			b.HasIndex("Barcode").IsUnique();
			b.HasIndex("CategoryId");
			b.HasIndex("Sku").IsUnique();
			b.ToTable("products", null, (TableBuilder t) =>
			{
				t.HasCheckConstraint("CK_products_cost_price", "\"CostPrice\" >= 0");
				t.HasCheckConstraint("CK_products_sale_price", "\"SalePrice\" >= 0");
			});
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.PurchaseItem", (EntityTypeBuilder b) =>
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<decimal>("Quantity").HasPrecision(18, 3).HasColumnType("numeric(18,3)");
			b.Property<Guid>("ReceiptId").HasColumnType("uuid");
			b.Property<decimal>("UnitCost").HasPrecision(18, 2).HasColumnType("numeric(18,2)");
			b.HasKey("Id");
			b.HasIndex("ProductId");
			b.HasIndex("ReceiptId", "ProductId").IsUnique();
			b.ToTable("purchase_items", null, (TableBuilder t) =>
			{
				t.HasCheckConstraint("CK_purchase_items_quantity", "\"Quantity\" > 0");
				t.HasCheckConstraint("CK_purchase_items_unit_cost", "\"UnitCost\" >= 0");
			});
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.PurchaseReceipt", (EntityTypeBuilder b) =>
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset?>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CreatedBy").HasColumnType("uuid");
			b.Property<string>("ReceiptNo").IsRequired().HasMaxLength(80)
				.HasColumnType("character varying(80)");
			b.Property<string>("Status").IsRequired().HasMaxLength(16)
				.HasColumnType("character varying(16)");
			b.Property<Guid?>("SupplierId").HasColumnType("uuid");
			b.Property<decimal>("Total").HasPrecision(18, 2).HasColumnType("numeric(18,2)");
			b.HasKey("Id");
			b.HasIndex("CreatedBy");
			b.HasIndex("ReceiptNo").IsUnique();
			b.HasIndex("SupplierId");
			b.ToTable("purchase_receipts", null, (TableBuilder t) =>
			{
				t.HasCheckConstraint("CK_purchase_receipts_total", "\"Total\" >= 0");
			});
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.SalesOrder", (EntityTypeBuilder b) =>
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("CustomerId").HasColumnType("uuid");
			b.Property<decimal>("Discount").HasPrecision(18, 2).HasColumnType("numeric(18,2)");
			b.Property<Guid>("IdempotencyKey").HasColumnType("uuid");
			b.Property<string>("OrderNo").IsRequired().HasMaxLength(80)
				.HasColumnType("character varying(80)");
			b.Property<decimal>("PaidTotal").HasPrecision(18, 2).HasColumnType("numeric(18,2)");
			b.Property<decimal>("RefundedTotal").HasPrecision(18, 2).HasColumnType("numeric(18,2)");
			b.Property<decimal>("ReturnedTotal").HasPrecision(18, 2).HasColumnType("numeric(18,2)");
			b.Property<Guid>("SoldBy").HasColumnType("uuid");
			b.Property<string>("Status").IsRequired().HasMaxLength(24)
				.HasColumnType("character varying(24)");
			b.Property<decimal>("Subtotal").HasPrecision(18, 2).HasColumnType("numeric(18,2)");
			b.Property<decimal>("Total").HasPrecision(18, 2).HasColumnType("numeric(18,2)");
			b.HasKey("Id");
			b.HasIndex("CreatedAt");
			b.HasIndex("CustomerId");
			b.HasIndex("IdempotencyKey").IsUnique();
			b.HasIndex("OrderNo").IsUnique();
			b.HasIndex("SoldBy");
			b.ToTable("sales_orders", null, (TableBuilder t) =>
			{
				t.HasCheckConstraint("CK_sales_orders_paid", "\"PaidTotal\" >= 0 AND \"PaidTotal\" <= \"Total\"");
				t.HasCheckConstraint("CK_sales_orders_returned", "\"ReturnedTotal\" >= 0 AND \"ReturnedTotal\" <= \"Total\" AND \"RefundedTotal\" >= 0 AND \"RefundedTotal\" <= \"PaidTotal\"");
				t.HasCheckConstraint("CK_sales_orders_total", "\"Subtotal\" >= 0 AND \"Discount\" >= 0 AND \"Total\" >= 0");
			});
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.SalesOrderItem", (EntityTypeBuilder b) =>
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("NameSnapshot").IsRequired().HasMaxLength(240)
				.HasColumnType("character varying(240)");
			b.Property<Guid>("OrderId").HasColumnType("uuid");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<decimal>("Quantity").HasPrecision(18, 3).HasColumnType("numeric(18,3)");
			b.Property<string>("SkuSnapshot").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<decimal>("UnitCost").HasPrecision(18, 2).HasColumnType("numeric(18,2)");
			b.Property<decimal>("UnitPrice").HasPrecision(18, 2).HasColumnType("numeric(18,2)");
			b.HasKey("Id");
			b.HasIndex("ProductId");
			b.HasIndex("OrderId", "ProductId").IsUnique();
			b.ToTable("sales_order_items", null, (TableBuilder t) =>
			{
				t.HasCheckConstraint("CK_sales_order_items_quantity", "\"Quantity\" > 0");
			});
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.SalesReturn", (EntityTypeBuilder b) =>
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CreatedBy").HasColumnType("uuid");
			b.Property<Guid>("IdempotencyKey").HasColumnType("uuid");
			b.Property<Guid>("OrderId").HasColumnType("uuid");
			b.Property<string>("Reason").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<decimal>("RefundAmount").HasPrecision(18, 2).HasColumnType("numeric(18,2)");
			b.Property<string>("RefundMethod").IsRequired().HasMaxLength(24)
				.HasColumnType("character varying(24)");
			b.Property<decimal>("Total").HasPrecision(18, 2).HasColumnType("numeric(18,2)");
			b.HasKey("Id");
			b.HasIndex("CreatedBy");
			b.HasIndex("IdempotencyKey").IsUnique();
			b.HasIndex("OrderId", "CreatedAt");
			b.ToTable("sales_returns", null, (TableBuilder t) =>
			{
				t.HasCheckConstraint("CK_sales_returns_total", "\"Total\" >= 0 AND \"RefundAmount\" >= 0 AND \"RefundAmount\" <= \"Total\"");
			});
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.SalesReturnItem", (EntityTypeBuilder b) =>
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<decimal>("Amount").HasPrecision(18, 2).HasColumnType("numeric(18,2)");
			b.Property<Guid>("OrderItemId").HasColumnType("uuid");
			b.Property<decimal>("Quantity").HasPrecision(18, 3).HasColumnType("numeric(18,3)");
			b.Property<bool>("Restock").HasColumnType("boolean");
			b.Property<Guid>("ReturnId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("OrderItemId");
			b.HasIndex("ReturnId", "OrderItemId").IsUnique();
			b.ToTable("sales_return_items", null, (TableBuilder t) =>
			{
				t.HasCheckConstraint("CK_sales_return_items_amount", "\"Amount\" >= 0");
				t.HasCheckConstraint("CK_sales_return_items_quantity", "\"Quantity\" > 0");
			});
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.StockBalance", (EntityTypeBuilder b) =>
		{
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<decimal>("Quantity").HasPrecision(18, 3).HasColumnType("numeric(18,3)");
			b.Property<uint>("Version").IsConcurrencyToken().ValueGeneratedOnAddOrUpdate()
				.HasColumnType("xid")
				.HasColumnName("xmin");
			b.HasKey("ProductId");
			b.ToTable("stock_balances", null, (TableBuilder t) =>
			{
				t.HasCheckConstraint("CK_stock_balances_quantity", "\"Quantity\" >= 0");
			});
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.StockMovement", (EntityTypeBuilder b) =>
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<decimal>("After").HasPrecision(18, 3).HasColumnType("numeric(18,3)");
			b.Property<decimal>("Before").HasPrecision(18, 3).HasColumnType("numeric(18,3)");
			b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<decimal>("Delta").HasPrecision(18, 3).HasColumnType("numeric(18,3)");
			b.Property<Guid>("PerformedBy").HasColumnType("uuid");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<string>("Reason").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<Guid>("SourceId").HasColumnType("uuid");
			b.Property<string>("Type").IsRequired().HasColumnType("text");
			b.HasKey("Id");
			b.HasIndex("PerformedBy");
			b.HasIndex("ProductId", "CreatedAt");
			b.ToTable("stock_movements", null, (TableBuilder t) =>
			{
				t.HasCheckConstraint("CK_stock_movements_balance", "\"After\" = \"Before\" + \"Delta\"");
			});
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.StocktakeItem", (EntityTypeBuilder b) =>
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<decimal?>("ActualQuantity").HasPrecision(18, 3).HasColumnType("numeric(18,3)");
			b.Property<decimal>("ExpectedQuantity").HasPrecision(18, 3).HasColumnType("numeric(18,3)");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<Guid>("SessionId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("ProductId");
			b.HasIndex("SessionId", "ProductId").IsUnique();
			b.ToTable("stocktake_items", null, (TableBuilder t) =>
			{
				t.HasCheckConstraint("CK_stocktake_items_actual", "\"ActualQuantity\" IS NULL OR \"ActualQuantity\" >= 0");
				t.HasCheckConstraint("CK_stocktake_items_expected", "\"ExpectedQuantity\" >= 0");
			});
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.StocktakeSession", (EntityTypeBuilder b) =>
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset?>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CreatedBy").HasColumnType("uuid");
			b.Property<string>("Status").IsRequired().HasMaxLength(16)
				.HasColumnType("character varying(16)");
			b.HasKey("Id");
			b.HasIndex("CreatedAt");
			b.HasIndex("CreatedBy");
			b.ToTable("stocktake_sessions", (string?)null);
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.Supplier", (EntityTypeBuilder b) =>
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<bool>("Active").HasColumnType("boolean");
			b.Property<string>("Code").IsRequired().HasMaxLength(80)
				.HasColumnType("character varying(80)");
			b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Email").HasMaxLength(320).HasColumnType("character varying(320)");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("Phone").HasMaxLength(40).HasColumnType("character varying(40)");
			b.HasKey("Id");
			b.HasIndex("Code").IsUnique();
			b.ToTable("suppliers", (string?)null);
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.User", (EntityTypeBuilder b) =>
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<bool>("Active").HasColumnType("boolean");
			b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Email").IsRequired().HasMaxLength(320)
				.HasColumnType("character varying(320)");
			b.Property<string>("FullName").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("PasswordHash").IsRequired().HasColumnType("text");
			b.Property<string>("Role").IsRequired().HasMaxLength(16)
				.HasColumnType("character varying(16)");
			b.HasKey("Id");
			b.HasIndex("Email").IsUnique();
			b.HasIndex("Role").IsUnique().HasFilter("\"Role\" = 'Owner'");
			b.ToTable("users", (string?)null);
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.Category", (EntityTypeBuilder b) =>
		{
			b.HasOne("HongBeautyPOS.Domain.Category", null).WithMany().HasForeignKey("ParentId");
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.EmployeePermission", (EntityTypeBuilder b) =>
		{
			b.HasOne("HongBeautyPOS.Domain.User", null).WithMany("Permissions").HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.Payment", (EntityTypeBuilder b) =>
		{
			b.HasOne("HongBeautyPOS.Domain.SalesOrder", null).WithMany().HasForeignKey("OrderId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("HongBeautyPOS.Domain.User", null).WithMany().HasForeignKey("ReceivedBy")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.Product", (EntityTypeBuilder b) =>
		{
			b.HasOne("HongBeautyPOS.Domain.Category", null).WithMany().HasForeignKey("CategoryId");
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.PurchaseItem", (EntityTypeBuilder b) =>
		{
			b.HasOne("HongBeautyPOS.Domain.Product", null).WithMany().HasForeignKey("ProductId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("HongBeautyPOS.Domain.PurchaseReceipt", null).WithMany("Items").HasForeignKey("ReceiptId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.PurchaseReceipt", (EntityTypeBuilder b) =>
		{
			b.HasOne("HongBeautyPOS.Domain.User", null).WithMany().HasForeignKey("CreatedBy")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("HongBeautyPOS.Domain.Supplier", null).WithMany().HasForeignKey("SupplierId")
				.OnDelete(DeleteBehavior.Restrict);
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.SalesOrder", (EntityTypeBuilder b) =>
		{
			b.HasOne("HongBeautyPOS.Domain.Customer", null).WithMany().HasForeignKey("CustomerId")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("HongBeautyPOS.Domain.User", null).WithMany().HasForeignKey("SoldBy")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.SalesOrderItem", (EntityTypeBuilder b) =>
		{
			b.HasOne("HongBeautyPOS.Domain.SalesOrder", null).WithMany("Items").HasForeignKey("OrderId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("HongBeautyPOS.Domain.Product", null).WithMany().HasForeignKey("ProductId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.SalesReturn", (EntityTypeBuilder b) =>
		{
			b.HasOne("HongBeautyPOS.Domain.User", null).WithMany().HasForeignKey("CreatedBy")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("HongBeautyPOS.Domain.SalesOrder", null).WithMany().HasForeignKey("OrderId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.SalesReturnItem", (EntityTypeBuilder b) =>
		{
			b.HasOne("HongBeautyPOS.Domain.SalesOrderItem", null).WithMany().HasForeignKey("OrderItemId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("HongBeautyPOS.Domain.SalesReturn", null).WithMany("Items").HasForeignKey("ReturnId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.StockBalance", (EntityTypeBuilder b) =>
		{
			b.HasOne("HongBeautyPOS.Domain.Product", null).WithOne("Balance").HasForeignKey("HongBeautyPOS.Domain.StockBalance", "ProductId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.StockMovement", (EntityTypeBuilder b) =>
		{
			b.HasOne("HongBeautyPOS.Domain.User", null).WithMany().HasForeignKey("PerformedBy")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("HongBeautyPOS.Domain.Product", null).WithMany().HasForeignKey("ProductId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.StocktakeItem", (EntityTypeBuilder b) =>
		{
			b.HasOne("HongBeautyPOS.Domain.Product", null).WithMany().HasForeignKey("ProductId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("HongBeautyPOS.Domain.StocktakeSession", null).WithMany("Items").HasForeignKey("SessionId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.StocktakeSession", (EntityTypeBuilder b) =>
		{
			b.HasOne("HongBeautyPOS.Domain.User", null).WithMany().HasForeignKey("CreatedBy")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.Product", (EntityTypeBuilder b) =>
		{
			b.Navigation("Balance");
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.PurchaseReceipt", (EntityTypeBuilder b) =>
		{
			b.Navigation("Items");
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.SalesOrder", (EntityTypeBuilder b) =>
		{
			b.Navigation("Items");
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.SalesReturn", (EntityTypeBuilder b) =>
		{
			b.Navigation("Items");
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.StocktakeSession", (EntityTypeBuilder b) =>
		{
			b.Navigation("Items");
		});
		modelBuilder.Entity("HongBeautyPOS.Domain.User", (EntityTypeBuilder b) =>
		{
			b.Navigation("Permissions");
		});
	}
}
