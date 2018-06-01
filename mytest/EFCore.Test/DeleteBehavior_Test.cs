using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace EFCore.Test
{
    public class DeleteBehavior_Test
    {
        private ITestOutputHelper _testOutputHelper;


        public DeleteBehavior_Test(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }



        //https://docs.microsoft.com/zh-cn/previous-versions/sql/sql-server-2005/ms186973(v%3dsql.90)
        //https://docs.microsoft.com/zh-cn/ef/core/saving/cascade-delete
        //https://github.com/aspnet/EntityFramework.Docs/blob/live/entity-framework/core/saving/cascade-delete.md
        //https://github.com/aspnet/EntityFramework.Docs.zh-cn/blob/live/entity-framework/core/saving/cascade-delete.md
 //增加注释1


        [InlineData(DeleteBehavior.Cascade)]
        [InlineData(DeleteBehavior.SetNull)]
        [InlineData(DeleteBehavior.ClientSetNull)]
        [InlineData(DeleteBehavior.Restrict)]
        [Theory]
        public void Create_Database(DeleteBehavior behavior)
        {
            using (var northwindContext = new NorthwindContext(behavior))
            {
                northwindContext.Database.EnsureDeleted();
                northwindContext.Database.EnsureCreated();
            }
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
        [InlineData(DeleteBehavior.Cascade, true)]
        [InlineData(DeleteBehavior.Cascade, false)]
        /*
         * 如果关联实体未被跟踪，主实体的状态标记为删除，执行SaveChage时，通过数据库的行为关联数据行的外键更新为Null；
         * 如果关联实体已经被跟踪，将主实体的状态标记为删除时，关联将实体的外键被设置为Null ,同时将关联实体的状态标记为修改，执行SaveChange时，先更新关联实体的数据 ，然后删除主实体；
         * 外键不能设置不可能空字段。
         */
        [InlineData(DeleteBehavior.SetNull, true)]
        [InlineData(DeleteBehavior.SetNull, false)]
        /*
         * 数据库不会执行任何行为。
         * 关联实体必须被跟踪，将主实体的状态标记为删除时，关联将实体的外键被设置为Null ,同时将关联实体的状态标记为修改，执行SaveChange时，先更新关联实体的数据 ，然后删除主实体；
         * 外键不能设置不可能空字段。
         */
        [InlineData(DeleteBehavior.ClientSetNull, true)]
        [InlineData(DeleteBehavior.ClientSetNull, false)]
        /*
         * EF不执行任何操作，由开发人员决定关联实体的行为，可以将关联实体的状态设置为删除，也可以将关联实体的外键设置为null。
         * 因为要修改关联实体的状态或外键的值，所以关联实体必须被跟踪。
         */
        [InlineData(DeleteBehavior.Restrict, true)]
        [InlineData(DeleteBehavior.Restrict, false)]

        [Theory]
        public void Execute(DeleteBehavior behavior, bool includeDetail)
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
                var queryable = northwindContext.Set<Order>().Where(e => e.OrderID == orderId);
                //if (behavior == DeleteBehavior.ClientSetNull|| behavior==DeleteBehavior.Restrict)
                if (includeDetail)
                {
                    queryable = queryable.Include(e => e.OrderDetails);
                }

                var order = queryable.Single(); //.Include(e=>e.OrderDetails).Single();
                northwindContext.Set<Order>().Remove(order);
                if (behavior == DeleteBehavior.Restrict
                    && order.OrderDetails != null)
                {
                    foreach (var orderDetail in order.OrderDetails)
                    {
                        orderDetail.OrderID = null;
                    }
                }

                try
                {
                    northwindContext.SaveChanges();
                    DumpSql();
                }
                catch (Exception)
                {
                    DumpSql();
                    throw;
                }

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

        private void DumpSql()
        {
            foreach (var logMessage in MyLoggerProvider.LogMessages)
            {
                _testOutputHelper.WriteLine("    " + logMessage);
            }
        }
    }

    internal class MyLoggerProvider :   ILoggerFactory
    {

        public static IList<string> LogMessages = new List<string>();
        public ILogger CreateLogger(string categoryName) => new SampleLogger();
        public void AddProvider(ILoggerProvider provider)
        {
            
        }

        public void Dispose()
        {
        }

        private class SampleLogger : ILogger
        {
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter)
            {
                if (eventId.Id == RelationalEventId.CommandExecuting.Id)
                {
                    var message = formatter(state, exception);
                    var commandIndex = Math.Max(message.IndexOf("UPDATE"), message.IndexOf("DELETE"));
                    if (commandIndex >= 0)
                    {
                        var truncatedMessage = message.Substring(commandIndex, message.IndexOf(";", commandIndex) - commandIndex).Replace(Environment.NewLine, " ");

                        for (var i = 0; i < 4; i++)
                        {
                            var paramIndex = message.IndexOf($"@p{i}='");
                            if (paramIndex >= 0)
                            {
                                var paramValue = message.Substring(paramIndex + 5, 1);
                                if (paramValue == "'")
                                {
                                    paramValue = "NULL";
                                }

                                truncatedMessage = truncatedMessage.Replace($"@p{i}", paramValue);
                            }
                        }

                        LogMessages.Add(truncatedMessage);
                    }
                }
            }

            public IDisposable BeginScope<TState>(TState state) => null;
        }
    }
}
