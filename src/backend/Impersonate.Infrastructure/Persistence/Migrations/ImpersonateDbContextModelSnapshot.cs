using System;
using Impersonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable
namespace Impersonate.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ImpersonateDbContext))]
partial class ImpersonateDbContextModelSnapshot : ModelSnapshot
{
 protected override void BuildModel(ModelBuilder modelBuilder) { modelBuilder.HasAnnotation("ProductVersion", "10.0.0"); modelBuilder.Entity("Impersonate.Domain.Projects.Project", b => { b.Property<Guid>("Id"); b.Property<DateTimeOffset>("CreatedAtUtc"); b.Property<string>("DefaultBranch").IsRequired().HasMaxLength(200); b.Property<string>("Description").HasMaxLength(2000); b.Property<string>("Name").IsRequired().HasMaxLength(150); b.Property<string>("RepositoryUrl").IsRequired().HasMaxLength(500); b.Property<string>("Status").IsRequired().HasMaxLength(16); b.Property<DateTimeOffset>("UpdatedAtUtc"); b.HasKey("Id"); b.HasIndex("Name"); b.HasIndex("Status"); b.ToTable("Projects"); }); }
}
