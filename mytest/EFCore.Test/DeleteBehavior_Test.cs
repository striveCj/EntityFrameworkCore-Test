using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EFCore.Test
{
    public class DeleteBehavior_Test
    {

        public DeleteBehavior_Test()
        {
           
        }


        //https://docs.microsoft.com/zh-cn/previous-versions/sql/sql-server-2005/ms186973(v%3dsql.90)
        //https://docs.microsoft.com/zh-cn/ef/core/saving/cascade-delete
        //https://github.com/aspnet/EntityFramework.Docs/blob/live/entity-framework/core/saving/cascade-delete.md
        //https://github.com/aspnet/EntityFramework.Docs.zh-cn/blob/live/entity-framework/core/saving/cascade-delete.md

        /*
         * 如果关联实体未被跟踪，主实体的状态标记为删除，执行SaveChage时，通过数据库的行为删除关联的数据行；
         * 如果关联实体已经被跟踪，将主实体的状态标记为删除时，关联将实体的状态也会标记为删除，执行SaveChange时，先删除关联实体，然后删除主实体；
         * 外键也可以设置不可能空。
         */
        [InlineData(DeleteBehavior.Cascade)]
        /*
         * 如果关联实体未被跟踪，主实体的状态标记为删除，执行SaveChage时，通过数据库的行为关联数据行的外键更新为Null；
         * 如果关联实体已经被跟踪，将主实体的状态标记为删除时，关联将实体的外键被设置为Null ,同时将关联实体的状态标记为修改，执行SaveChange时，先更新关联实体的数据 ，然后删除主实体；
         * 外键不能设置不可能空字段。
         */
        [InlineData(DeleteBehavior.SetNull)]//
        /*
         * 数据库不会执行任何行为。
         * 关联实体必须被跟踪，将主实体的状态标记为删除时，关联将实体的外键被设置为Null ,同时将关联实体的状态标记为修改，执行SaveChange时，先更新关联实体的数据 ，然后删除主实体；
         * 外键不能设置不可能空字段。
         */
        [InlineData(DeleteBehavior.ClientSetNull)]
     /*
      * EF不执行任何操作，由开发人员决定关联实体的行为，可以将关联实体的状态设置为删除，也可以将关联实体的外键设置为null。
      * 因为要修改关联实体的状态或外键的值，所以关联实体必须被跟踪。
      */
        [InlineData(DeleteBehavior.Restrict)]

        [Theory]
        public void Execute(DeleteBehavior behavior)
        {
            using (var northwindContext = new NorthwindContext(behavior))
            {
                northwindContext.Database.EnsureDeleted();
                northwindContext.Database.EnsureCreated();
            }

            int orderId;
            int detailId;
            using (var northwindContext = new NorthwindContext(behavior))
            {
                var order = new Order
                {
                    Name = "Order1"
                };

                var orderDetail = new OrderDetail
                {
                    ProductID = 11
                };
                order.OrderDetails = new List<OrderDetail>
                {
                    orderDetail
                };


                northwindContext.Set<Order>().Add(order);

                northwindContext.SaveChanges();

                orderId = order.OrderID;
                detailId = orderDetail.DetailId;
            }

            using (var northwindContext = new NorthwindContext(behavior))
            {
               var queryable = northwindContext.Set<Order>().Where(e=>e.OrderID==orderId);
                //if (behavior == DeleteBehavior.ClientSetNull|| behavior==DeleteBehavior.Restrict)
                {
                    queryable = queryable.Include(e => e.OrderDetails);
                }
                    var order = queryable.Single();//.Include(e=>e.OrderDetails).Single();
                northwindContext.Set<Order>().Remove(order);
                if (behavior == DeleteBehavior.Restrict)
                {
                    foreach (var orderDetail in order.OrderDetails)
                    {
                        orderDetail.OrderID = null;
                    }
                }
                northwindContext.SaveChanges();
            }

            using (var northwindContext = new NorthwindContext(behavior))
            {
                var orderDetail = northwindContext.Set<OrderDetail>().Find(detailId);
                if (behavior == DeleteBehavior.Cascade)
                {
                    Assert.Null(orderDetail);
                }
                else
                {
                    Assert.NotNull(orderDetail);
                }
            }
        }
    }
}
