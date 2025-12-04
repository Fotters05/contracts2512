using System.Data.Entity;
using System.Data.Entity.Core.Common;
using System.Data.Entity.Infrastructure;
using Npgsql;

namespace Contract2512.Services
{
    public class NpgsqlDbConfiguration : DbConfiguration
    {
        public NpgsqlDbConfiguration()
        {
            // Регистрация провайдера Npgsql для Entity Framework 6
            SetProviderFactory("Npgsql", NpgsqlFactory.Instance);
            
            // Регистрация сервисов провайдера через рефлексию
            // (типы из EntityFramework6.Npgsql могут быть недоступны напрямую)
            var npgsqlServicesType = System.Type.GetType("Npgsql.NpgsqlServices, EntityFramework6.Npgsql, Culture=neutral, PublicKeyToken=5d8b90d52f46fda7");
            if (npgsqlServicesType != null)
            {
                var instanceProperty = npgsqlServicesType.GetProperty("Instance", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (instanceProperty != null)
                {
                    var servicesInstance = instanceProperty.GetValue(null);
                    if (servicesInstance != null)
                    {
                        SetProviderServices("Npgsql", (DbProviderServices)servicesInstance);
                    }
                }
            }
            
            // Регистрация фабрики соединений через рефлексию
            var connectionFactoryType = System.Type.GetType("Npgsql.NpgsqlConnectionFactory, EntityFramework6.Npgsql, Culture=neutral, PublicKeyToken=5d8b90d52f46fda7");
            if (connectionFactoryType != null)
            {
                var connectionFactory = System.Activator.CreateInstance(connectionFactoryType);
                if (connectionFactory != null)
                {
                    SetDefaultConnectionFactory((IDbConnectionFactory)connectionFactory);
                }
            }
        }
    }
}

