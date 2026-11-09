using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace HongBeautyPOS.Infrastructure.Migrations;

[DbContext(typeof(PosDbContext))]
[Migration("20261106030236_PurchasesAndSuppliers")]
public class PurchasesAndSuppliers : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable("suppliers", (ColumnsBuilder table) =>
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			int? maxLength = 80;
			OperationBuilder<AddColumnOperation> code = table.Column<string>("character varying(80)", null, maxLength);
			maxLength = 200;
			OperationBuilder<AddColumnOperation> name = table.Column<string>("character varying(200)", null, maxLength);
			maxLength = 40;
			OperationBuilder<AddColumnOperation> phone = table.Column<string>("character varying(40)", null, maxLength, rowVersion: false, null, nullable: true);
			maxLength = 320;
			return new
			{
				Id = id,
				Code = code,
				Name = name,
				Phone = phone,
				Email = table.Column<string>("character varying(320)", null, maxLength, rowVersion: false, null, nullable: true),
				Active = table.Column<bool>("boolean"),
				CreatedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_suppliers", x => x.Id);
		});
		migrationBuilder.CreateTable("purchase_receipts", (ColumnsBuilder table) =>
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			int? maxLength = 80;
			OperationBuilder<AddColumnOperation> receiptNo = table.Column<string>("character varying(80)", null, maxLength);
			OperationBuilder<AddColumnOperation> supplierId = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true);
			maxLength = 16;
			OperationBuilder<AddColumnOperation> status = table.Column<string>("character varying(16)", null, maxLength);
			maxLength = 18;
			int? scale = 2;
			return new
			{
				Id = id,
				ReceiptNo = receiptNo,
				SupplierId = supplierId,
				Status = status,
				Total = table.Column<decimal>("numeric(18,2)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, maxLength, scale),
				CreatedBy = table.Column<Guid>("uuid"),
				CreatedAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				CompletedAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_purchase_receipts", x => x.Id);
			table.CheckConstraint("CK_purchase_receipts_total", "\"Total\" >= 0");
			table.ForeignKey("FK_purchase_receipts_suppliers_SupplierId", x => x.SupplierId, "suppliers", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_purchase_receipts_users_CreatedBy", x => x.CreatedBy, "users", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("purchase_items", (ColumnsBuilder table) =>
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> receiptId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> productId = table.Column<Guid>("uuid");
			int? precision = 18;
			int? scale = 3;
			OperationBuilder<AddColumnOperation> quantity = table.Column<decimal>("numeric(18,3)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, precision, scale);
			scale = 18;
			precision = 2;
			return new
			{
				Id = id,
				ReceiptId = receiptId,
				ProductId = productId,
				Quantity = quantity,
				UnitCost = table.Column<decimal>("numeric(18,2)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, scale, precision)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_purchase_items", x => x.Id);
			table.CheckConstraint("CK_purchase_items_quantity", "\"Quantity\" > 0");
			table.CheckConstraint("CK_purchase_items_unit_cost", "\"UnitCost\" >= 0");
			table.ForeignKey("FK_purchase_items_products_ProductId", x => x.ProductId, "products", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_purchase_items_purchase_receipts_ReceiptId", x => x.ReceiptId, "purchase_receipts", "Id", null, ReferentialAction.NoAction, ReferentialAction.Cascade);
		});
		migrationBuilder.CreateIndex("IX_purchase_items_ProductId", "purchase_items", "ProductId");
		migrationBuilder.CreateIndex("IX_purchase_items_ReceiptId_ProductId", "purchase_items", new string[2] { "ReceiptId", "ProductId" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_purchase_receipts_CreatedBy", "purchase_receipts", "CreatedBy");
		migrationBuilder.CreateIndex("IX_purchase_receipts_ReceiptNo", "purchase_receipts", "ReceiptNo", null, unique: true);
		migrationBuilder.CreateIndex("IX_purchase_receipts_SupplierId", "purchase_receipts", "SupplierId");
		migrationBuilder.CreateIndex("IX_suppliers_Code", "suppliers", "Code", null, unique: true);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable("purchase_items");
		migrationBuilder.DropTable("purchase_receipts");
		migrationBuilder.DropTable("suppliers");
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
		modelBuilder.Entity("HongBeautyPOS.Domain.EmployeePermission", (EntityTypeBuilder b) =>
		{
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<string>("Permission").HasMaxLength(80).HasColumnType("character varying(80)");
			b.Property<DateTimeOffset>("GrantedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("GrantedBy").HasColumnType("uuid");
			b.HasKey("UserId", "Permission");
			b.ToTable("employee_permissions", (string?)null);
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
		modelBuilder.Entity("HongBeautyPOS.Domain.User", (EntityTypeBuilder b) =>
		{
			b.Navigation("Permissions");
		});
	}
}
