using MassTransit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Consumers.Common
{
    public class TConsumer<T> : IConsumer<T> where T : class
    {
        public virtual Task Consume(ConsumeContext<T> context)
        {
            Console.WriteLine($"Received {typeof(T).Name}: {context.Message}");
            return Task.CompletedTask;
        }
    }
}
