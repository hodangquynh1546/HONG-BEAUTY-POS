using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HongBeautyPOS.Infrastructure.Migrations;

[DbContext(typeof(PosDbContext))]
[Migration("20261106025900_StockMovementReason")]
public class StockMovementReason : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		int? maxLength = 500;
		migrationBuilder.AddColumn<string>("Reason", "stock_movements", "character varying(500)", null, maxLength, rowVersion: false, null, nullable: false, "");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn("Reason", "stock_movements");
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
