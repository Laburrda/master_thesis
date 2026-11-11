using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.Domain.Models;

namespace Shop.Infrastructure.Persistence.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            // Table Name
            builder.ToTable("products");

            // Primary Key
            builder.HasKey(p => p.Id);

            // Colname mapping
            builder.Property(p => p.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            builder.Property(p => p.Name)
                .HasColumnName("name")
                .IsRequired();

            builder.Property(p => p.Category)
                .HasColumnName("category")
                .IsRequired();

            builder.Property(p => p.Price)
                .HasColumnName("price")
                .HasPrecision(10, 2)
                .IsRequired();

            builder.Property(p => p.StockInitial)
                .HasColumnName("stock_initial")
                .IsRequired();

            builder.Property(p => p.StockAvailable)
                .HasColumnName("stock_available")
                .IsRequired();

        }
    }
}
