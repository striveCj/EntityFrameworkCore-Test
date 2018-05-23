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

        [InlineData(DeleteBehavior.Cascade)]
        [InlineData(DeleteBehavior.SetNull)]
        [InlineData(DeleteBehavior.ClientSetNull)]
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
                if (behavior == DeleteBehavior.ClientSetNull|| behavior==DeleteBehavior.Restrict)
                {
                    queryable = queryable.Include(e => e.OrderDetails);
                }
                    var order = queryable.Single();//.Include(e=>e.OrderDetails).Single();
                northwindContext.Set<Order>().Remove(order);
                //if (behavior == DeleteBehavior.Restrict)
                //{
                //    foreach (var orderDetail in order.OrderDetails)
                //    {
                //        orderDetail.OrderID = null;
                //    }
                //}
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
