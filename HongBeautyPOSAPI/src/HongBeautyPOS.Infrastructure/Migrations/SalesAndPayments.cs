using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace HongBeautyPOS.Infrastructure.Migrations;

[DbContext(typeof(PosDbContext))]
[Migration("20261106030733_SalesAndPayments")]
public class SalesAndPayments : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable("customers", (ColumnsBuilder table) =>
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			int? maxLength = 200;
			OperationBuilder<AddColumnOperation> fullName = table.Column<string>("character varying(200)", null, maxLength);
			maxLength = 40;
			OperationBuilder<AddColumnOperation> phone = table.Column<string>("character varying(40)", null, maxLength, rowVersion: false, null, nullable: true);
			maxLength = 320;
			return new
			{
				Id = id,
				FullName = fullName,
				Phone = phone,
				Email = table.Column<string>("character varying(320)", null, maxLength, rowVersion: false, null, nullable: true),
				Active = table.Column<bool>("boolean")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_customers", x => x.Id);
		});
		migrationBuilder.CreateTable("sales_orders", (ColumnsBuilder table) =>
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			int? maxLength = 80;
			OperationBuilder<AddColumnOperation> orderNo = table.Column<string>("character varying(80)", null, maxLength);
			OperationBuilder<AddColumnOperation> idempotencyKey = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> customerId = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true);
			OperationBuilder<AddColumnOperation> soldBy = table.Column<Guid>("uuid");
			maxLength = 24;
			OperationBuilder<AddColumnOperation> status = table.Column<string>("character varying(24)", null, maxLength);
			maxLength = 18;
			int? scale = 2;
			OperationBuilder<AddColumnOperation> subtotal = table.Column<decimal>("numeric(18,2)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, maxLength, scale);
			scale = 18;
			maxLength = 2;
			OperationBuilder<AddColumnOperation> discount = table.Column<decimal>("numeric(18,2)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, scale, maxLength);
			maxLength = 18;
			scale = 2;
			OperationBuilder<AddColumnOperation> total = table.Column<decimal>("numeric(18,2)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, maxLength, scale);
			scale = 18;
			maxLength = 2;
			return new
			{
				Id = id,
				OrderNo = orderNo,
				IdempotencyKey = idempotencyKey,
				CustomerId = customerId,
				SoldBy = soldBy,
				Status = status,
				Subtotal = subtotal,
				Discount = discount,
				Total = total,
				PaidTotal = table.Column<decimal>("numeric(18,2)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, scale, maxLength),
				CreatedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_sales_orders", x => x.Id);
			table.CheckConstraint("CK_sales_orders_paid", "\"PaidTotal\" >= 0 AND \"PaidTotal\" <= \"Total\"");
			table.CheckConstraint("CK_sales_orders_total", "\"Subtotal\" >= 0 AND \"Discount\" >= 0 AND \"Total\" >= 0");
			table.ForeignKey("FK_sales_orders_customers_CustomerId", x => x.CustomerId, "customers", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_sales_orders_users_SoldBy", x => x.SoldBy, "users", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("payments", (ColumnsBuilder table) =>
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> orderId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> idempotencyKey = table.Column<Guid>("uuid");
			int? maxLength = 24;
			OperationBuilder<AddColumnOperation> method = table.Column<string>("character varying(24)", null, maxLength);
			maxLength = 18;
			int? scale = 2;
			return new
			{
				Id = id,
				OrderId = orderId,
				IdempotencyKey = idempotencyKey,
				Method = method,
				Amount = table.Column<decimal>("numeric(18,2)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, maxLength, scale),
				ReceivedBy = table.Column<Guid>("uuid"),
				ReceivedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_payments", x => x.Id);
			table.CheckConstraint("CK_payments_amount", "\"Amount\" > 0");
			table.ForeignKey("FK_payments_sales_orders_OrderId", x => x.OrderId, "sales_orders", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_payments_users_ReceivedBy", x => x.ReceivedBy, "users", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("sales_order_items", (ColumnsBuilder table) =>
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> orderId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> productId = table.Column<Guid>("uuid");
			int? maxLength = 100;
			OperationBuilder<AddColumnOperation> skuSnapshot = table.Column<string>("character varying(100)", null, maxLength);
			maxLength = 240;
			OperationBuilder<AddColumnOperation> nameSnapshot = table.Column<string>("character varying(240)", null, maxLength);
			maxLength = 18;
			int? scale = 3;
			OperationBuilder<AddColumnOperation> quantity = table.Column<decimal>("numeric(18,3)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, maxLength, scale);
			scale = 18;
			maxLength = 2;
			OperationBuilder<AddColumnOperation> unitPrice = table.Column<decimal>("numeric(18,2)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, scale, maxLength);
			maxLength = 18;
			scale = 2;
			return new
			{
				Id = id,
				OrderId = orderId,
				ProductId = productId,
				SkuSnapshot = skuSnapshot,
				NameSnapshot = nameSnapshot,
				Quantity = quantity,
				UnitPrice = unitPrice,
				UnitCost = table.Column<decimal>("numeric(18,2)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, maxLength, scale)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_sales_order_items", x => x.Id);
			table.CheckConstraint("CK_sales_order_items_quantity", "\"Quantity\" > 0");
			table.ForeignKey("FK_sales_order_items_products_ProductId", x => x.ProductId, "products", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_sales_order_items_sales_orders_OrderId", x => x.OrderId, "sales_orders", "Id", null, ReferentialAction.NoAction, ReferentialAction.Cascade);
		});
		migrationBuilder.CreateIndex("IX_customers_Phone", "customers", "Phone");
		migrationBuilder.CreateIndex("IX_payments_IdempotencyKey", "payments", "IdempotencyKey", null, unique: true);
		migrationBuilder.CreateIndex("IX_payments_OrderId", "payments", "OrderId");
		migrationBuilder.CreateIndex("IX_payments_ReceivedBy", "payments", "ReceivedBy");
		migrationBuilder.CreateIndex("IX_sales_order_items_OrderId_ProductId", "sales_order_items", new string[2] { "OrderId", "ProductId" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_sales_order_items_ProductId", "sales_order_items", "ProductId");
		migrationBuilder.CreateIndex("IX_sales_orders_CreatedAt", "sales_orders", "CreatedAt");
		migrationBuilder.CreateIndex("IX_sales_orders_CustomerId", "sales_orders", "CustomerId");
		migrationBuilder.CreateIndex("IX_sales_orders_IdempotencyKey", "sales_orders", "IdempotencyKey", null, unique: true);
		migrationBuilder.CreateIndex("IX_sales_orders_OrderNo", "sales_orders", "OrderNo", null, unique: true);
		migrationBuilder.CreateIndex("IX_sales_orders_SoldBy", "sales_orders", "SoldBy");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable("payments");
		migrationBuilder.DropTable("sales_order_items");
		migrationBuilder.DropTable("sales_orders");
		migrationBuilder.DropTable("customers");
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
		modelBuilder.Entity("HongBeautyPOS.Domain.User", (EntityTypeBuilder b) =>
		{
			b.Navigation("Permissions");
		});
	}
}
