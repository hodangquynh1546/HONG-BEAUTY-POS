using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace HongBeautyPOS.Infrastructure.Migrations;

[DbContext(typeof(PosDbContext))]
[Migration("20261106022237_InitialCore")]
public class InitialCore : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable("categories", (ColumnsBuilder table) =>
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> parentId = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true);
			int? maxLength = 160;
			return new
			{
				Id = id,
				ParentId = parentId,
				Name = table.Column<string>("character varying(160)", null, maxLength),
				Active = table.Column<bool>("boolean")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_categories", x => x.Id);
			table.ForeignKey("FK_categories_categories_ParentId", x => x.ParentId, "categories", "Id");
		});
		migrationBuilder.CreateTable("users", (ColumnsBuilder table) =>
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			int? maxLength = 320;
			OperationBuilder<AddColumnOperation> email = table.Column<string>("character varying(320)", null, maxLength);
			OperationBuilder<AddColumnOperation> passwordHash = table.Column<string>("text");
			maxLength = 200;
			OperationBuilder<AddColumnOperation> fullName = table.Column<string>("character varying(200)", null, maxLength);
			maxLength = 16;
			return new
			{
				Id = id,
				Email = email,
				PasswordHash = passwordHash,
				FullName = fullName,
				Role = table.Column<string>("character varying(16)", null, maxLength),
				Active = table.Column<bool>("boolean"),
				CreatedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_users", x => x.Id);
		});
		migrationBuilder.CreateTable("products", (ColumnsBuilder table) =>
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> categoryId = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true);
			int? maxLength = 100;
			OperationBuilder<AddColumnOperation> sku = table.Column<string>("character varying(100)", null, maxLength);
			OperationBuilder<AddColumnOperation> barcode = table.Column<string>("text", null, null, rowVersion: false, null, nullable: true);
			maxLength = 240;
			OperationBuilder<AddColumnOperation> name = table.Column<string>("character varying(240)", null, maxLength);
			OperationBuilder<AddColumnOperation> unit = table.Column<string>("text");
			maxLength = 18;
			int? scale = 2;
			OperationBuilder<AddColumnOperation> costPrice = table.Column<decimal>("numeric(18,2)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, maxLength, scale);
			scale = 18;
			maxLength = 2;
			OperationBuilder<AddColumnOperation> salePrice = table.Column<decimal>("numeric(18,2)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, scale, maxLength);
			maxLength = 18;
			scale = 3;
			return new
			{
				Id = id,
				CategoryId = categoryId,
				Sku = sku,
				Barcode = barcode,
				Name = name,
				Unit = unit,
				CostPrice = costPrice,
				SalePrice = salePrice,
				ReorderLevel = table.Column<decimal>("numeric(18,3)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, maxLength, scale),
				Active = table.Column<bool>("boolean"),
				CreatedAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				UpdatedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_products", x => x.Id);
			table.CheckConstraint("CK_products_cost_price", "\"CostPrice\" >= 0");
			table.CheckConstraint("CK_products_sale_price", "\"SalePrice\" >= 0");
			table.ForeignKey("FK_products_categories_CategoryId", x => x.CategoryId, "categories", "Id");
		});
		migrationBuilder.CreateTable("employee_permissions", (ColumnsBuilder table) =>
		{
			OperationBuilder<AddColumnOperation> userId = table.Column<Guid>("uuid");
			int? maxLength = 80;
			return new
			{
				UserId = userId,
				Permission = table.Column<string>("character varying(80)", null, maxLength),
				GrantedBy = table.Column<Guid>("uuid"),
				GrantedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_employee_permissions", x => new { x.UserId, x.Permission });
			table.ForeignKey("FK_employee_permissions_users_UserId", x => x.UserId, "users", "Id", null, ReferentialAction.NoAction, ReferentialAction.Cascade);
		});
		migrationBuilder.CreateTable("stock_balances", (ColumnsBuilder table) =>
		{
			OperationBuilder<AddColumnOperation> productId = table.Column<Guid>("uuid");
			int? precision = 18;
			int? scale = 3;
			return new
			{
				ProductId = productId,
				Quantity = table.Column<decimal>("numeric(18,3)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, precision, scale),
				xmin = table.Column<uint>("xid", null, null, rowVersion: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_stock_balances", x => x.ProductId);
			table.CheckConstraint("CK_stock_balances_quantity", "\"Quantity\" >= 0");
			table.ForeignKey("FK_stock_balances_products_ProductId", x => x.ProductId, "products", "Id", null, ReferentialAction.NoAction, ReferentialAction.Cascade);
		});
		migrationBuilder.CreateTable("stock_movements", (ColumnsBuilder table) =>
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> productId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> type = table.Column<string>("text");
			int? precision = 18;
			int? scale = 3;
			OperationBuilder<AddColumnOperation> delta = table.Column<decimal>("numeric(18,3)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, precision, scale);
			scale = 18;
			precision = 3;
			OperationBuilder<AddColumnOperation> before = table.Column<decimal>("numeric(18,3)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, scale, precision);
			precision = 18;
			scale = 3;
			return new
			{
				Id = id,
				ProductId = productId,
				Type = type,
				Delta = delta,
				Before = before,
				After = table.Column<decimal>("numeric(18,3)", null, null, rowVersion: false, null, nullable: false, null, null, null, null, null, null, precision, scale),
				SourceId = table.Column<Guid>("uuid"),
				PerformedBy = table.Column<Guid>("uuid"),
				CreatedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_stock_movements", x => x.Id);
			table.CheckConstraint("CK_stock_movements_balance", "\"After\" = \"Before\" + \"Delta\"");
			table.ForeignKey("FK_stock_movements_products_ProductId", x => x.ProductId, "products", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_stock_movements_users_PerformedBy", x => x.PerformedBy, "users", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateIndex("IX_categories_ParentId", "categories", "ParentId");
		migrationBuilder.CreateIndex("IX_products_Barcode", "products", "Barcode", null, unique: true);
		migrationBuilder.CreateIndex("IX_products_CategoryId", "products", "CategoryId");
		migrationBuilder.CreateIndex("IX_products_Sku", "products", "Sku", null, unique: true);
		migrationBuilder.CreateIndex("IX_stock_movements_PerformedBy", "stock_movements", "PerformedBy");
		migrationBuilder.CreateIndex("IX_stock_movements_ProductId_CreatedAt", "stock_movements", new string[2] { "ProductId", "CreatedAt" });
		migrationBuilder.CreateIndex("IX_users_Email", "users", "Email", null, unique: true);
		migrationBuilder.CreateIndex("IX_users_Role", "users", "Role", null, unique: true, "\"Role\" = 'Owner'");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable("employee_permissions");
		migrationBuilder.DropTable("stock_balances");
		migrationBuilder.DropTable("stock_movements");
		migrationBuilder.DropTable("products");
		migrationBuilder.DropTable("users");
		migrationBuilder.DropTable("categories");
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
		modelBuilder.Entity("HongBeautyPOS.Domain.User", (EntityTypeBuilder b) =>
		{
			b.Navigation("Permissions");
		});
	}
}
