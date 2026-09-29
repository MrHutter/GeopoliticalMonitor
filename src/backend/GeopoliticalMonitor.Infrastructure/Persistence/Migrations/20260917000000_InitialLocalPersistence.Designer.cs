using System;
using GeopoliticalMonitor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeopoliticalMonitor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GeopoliticalMonitorDbContext))]
[Migration("20260917000000_InitialLocalPersistence")]
public partial class InitialLocalPersistence
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "10.0.12");

        modelBuilder.Entity("GeopoliticalMonitor.Infrastructure.Persistence.Entities.ApplicationPreferenceEntity", b =>
        {
            b.Property<string>("Key")
                .HasMaxLength(64)
                .HasColumnType("TEXT");

            b.Property<DateTimeOffset>("UpdatedAt")
                .HasColumnType("TEXT");

            b.Property<string>("Value")
                .IsRequired()
                .HasMaxLength(256)
                .HasColumnType("TEXT");

            b.HasKey("Key");

            b.ToTable("ApplicationPreferences");
        });
#pragma warning restore 612, 618
    }
}
