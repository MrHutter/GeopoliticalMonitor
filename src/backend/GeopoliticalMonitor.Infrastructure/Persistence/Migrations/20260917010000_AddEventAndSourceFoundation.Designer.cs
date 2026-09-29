using System;
using GeopoliticalMonitor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeopoliticalMonitor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GeopoliticalMonitorDbContext))]
[Migration("20260917010000_AddEventAndSourceFoundation")]
public partial class AddEventAndSourceFoundation
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        GeopoliticalMonitorDbContextModelSnapshot.BuildCurrentModel(modelBuilder);
#pragma warning restore 612, 618
    }
}