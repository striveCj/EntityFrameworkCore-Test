using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.SqlClient;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;

namespace EFCore.Test
{
    public class NorthwindContext : DbContext
    {
        public NorthwindContext(DeleteBehavior deleteBehavior)
        {
            DeleteBehavior = deleteBehavior;
        }

        public NorthwindContext()
        {
        }

        public virtual DbSet<Order> Orders { get; set; }
        public virtual DbSet<OrderDetail> OrderDetails { get; set; }

        public override int SaveChanges()
        {
            MyLoggerProvider.LogMessages.Clear();
            return base.SaveChanges();
        }

        public DeleteBehavior DeleteBehavior { get; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var sqlConnectionStringBuilder = new SqlConnectionStringBuilder
            {
                DataSource = "192.168.10.226",
                InitialCatalog = "Northwind_Test",
                UserID = "sa",
                Password = "w1!"
            };
            optionsBuilder.UseSqlServer(sqlConnectionStringBuilder.ConnectionString).ReplaceService<IModelCacheKeyFactory, DynamicModelCacheKeyFactory>();
            optionsBuilder.EnableSensitiveDataLogging();
            //  optionsBuilder.UseLazyLoadingProxies();
            optionsBuilder.UseLoggerFactory(new MyLoggerProvider());
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>(
                builder =>
                {
                    builder.Property(e => e.OrderID);

                    builder.Property(e => e.OrderDate);
                    builder.HasQueryFilter(e => e.OrderID > 0);
                    builder.HasIndex(e => e.OrderID).IsUnique();

                    builder.HasMany<OrderDetail>(e => e.OrderDetails).WithOne(e => e.Order).HasForeignKey(e => e.OrderID).OnDelete(this.DeleteBehavior);
                });



            modelBuilder.Entity<OrderDetail>(
                e =>
                {
                    e.HasKey(od => od.DetailId);
                    e.ToTable("Order Details");
                    if (DeleteBehavior == DeleteBehavior.Cascade)
                    {
                        e.Property(od => od.OrderID).IsRequired();
                    }
                });
        }
    }

    public class DynamicModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context)
        {
            var northwindContext = (NorthwindContext)context;
            return (
                northwindContext.GetType(), northwindContext.DeleteBehavior
            );
        }
    }



    public class Order
    {
        //private readonly ILazyLoader _lazyLoader;
        //private ICollection<OrderDetail> _orderDetails;

        //public Order(ILazyLoader lazyLoader)
        //{
        //    _lazyLoader = lazyLoader;
        //}

        //public Order()
        //{
        //}

        public int OrderID { get; set; }

        public string Name { get; set; }

        public DateTime? OrderDate { get; set; }

        //[ForeignKey(nameof(OrderDetail.OrderID))]

        //public  ICollection<OrderDetail> OrderDetails
        //{
        //    get => _lazyLoader?.Load(this,ref _orderDetails)?? _orderDetails;
        //    set => _orderDetails = value;
        //}

        public ICollection<OrderDetail> OrderDetails { get; set; }
    }

    public class OrderDetail
    {
        //private readonly ILazyLoader _lazyLoader;
        //private Order _order;

        //public OrderDetail(ILazyLoader lazyLoader)
        //{
        //    _lazyLoader = lazyLoader;
        //}

        //public OrderDetail()
        //{
        //    ;
        //}

        public int DetailId { get; set; }

        public int? OrderID { get; set; }
        public int ProductID { get; set; }


        //public  Order Order
        //{
        //    get => _lazyLoader?.Load(this, ref _order)?? _order;
        //    set => _order = value;
        //}
        public Order Order { get; set; }
    }
}
