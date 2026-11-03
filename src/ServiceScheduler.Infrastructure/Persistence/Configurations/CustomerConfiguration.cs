using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ServiceScheduler.Infrastructure.Persistence.Rows;

namespace ServiceScheduler.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<CustomerRow>
{
    public void Configure(EntityTypeBuilder<CustomerRow> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(customer => customer.Id).HasName("pk_customers");

        builder.Property(customer => customer.Id).HasColumnName("id");
        builder.Property(customer => customer.FullName).HasColumnName("full_name");
        builder.Property(customer => customer.Email).HasColumnName("email");
        builder.Property(customer => customer.Phone).HasColumnName("phone");

        builder.HasData(new CustomerRow
        {
            Id = SeedData.CustomerId,
            FullName = "Sample Customer",
            Email = "customer@example.com",
            Phone = "+00 000 000 000",
        });
    }
}
