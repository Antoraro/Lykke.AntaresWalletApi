using System.Collections.Generic;
using AntaresWalletApi.Profiles;
using Autofac;
using AutoMapper;
using JetBrains.Annotations;

namespace AntaresWalletApi.Modules
{
    [UsedImplicitly]
    public class AutoMapperModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<GrpcProfile>().As<Profile>();

            builder.Register(c =>
            {
                var mapperConfiguration = new MapperConfiguration(cfg =>
                {
                    foreach (var profile in c.Resolve<IEnumerable<Profile>>())
                    {
                        cfg.AddProfile(profile);
                    }
                });

                mapperConfiguration.AssertConfigurationIsValid();

                return mapperConfiguration;
            }).AsSelf().SingleInstance();

            builder.Register(c => c.Resolve<MapperConfiguration>().CreateMapper(c.Resolve))
                .As<IMapper>()
                .SingleInstance();
        }
    }
}
