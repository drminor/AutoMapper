using System;
using Shouldly;
using Xunit;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;

using AutoMapper.Configuration.Conventions;

namespace AutoMapper.UnitTests.PropBagDynamic
{
#if NET452
    public class When_using_PropBagDyn
    {
        public class Source
        {
            //string _item1;
            //public string GetItem1()
            //{
            //    return _item1;
            //}

            public string Item1 { get; set; }
            //(string value)
            //{
            //    _item1 = value;
            //}

            public int MyInteger;
        }

        public class Destination : DRM.PropBag.PropBagDyn
        {
            
        }

        private Destination CreateTestDest()
        {
            dynamic result = new Destination();

            string t = "This is from the dest.";
            int i = 300;

            DRM.PropBag.PropBagDyn pd = result as DRM.PropBag.PropBagDyn;

            pd.AddProp<string>("Item1", null, false, null, null, t);
            pd.AddProp<int>("MyInteger", null, false, null, null, i);
            //result.Item1 = t;
            //result.MyInteger = i;

            return result as Destination;
        }

        [Fact]
        public void ShouldMap()
        {
        //    Dictionary<string, Type> props = new Dictionary<string, Type>
        //    {
        //        { "Item1", typeof(string) },
        //        { "MyInteger", typeof(int) }
        //    };

        //    List<MemberInfo> extraMembers = GetExtraMembers<Destination>(props, useStandardDelegates: false);

        //    MemberInfo f = extraMembers.First();
        //    bool fp = f.IsPublic();

            var config = new MapperConfiguration(cfg =>
                {
                    //cfg
                    //.AddMemberConfiguration()
                    //.AddMember<NameSplitMember>()
                    //.AddName<PrePostfixName>(_ => _.AddStrings(p => p.Prefixes, "Get")
                    //        .AddStrings(p => p.DestinationPostfixes, "Set"));

                    //cfg.ShouldMapField = ShouldMap;
                    //cfg.ShouldMapProperty = ShouldMap;

                    //cfg
                    //.CreateMap<Source, Destination>();

                    //cfg
                    //.CreateMap<Destination, Source>();

                });


            var mapper = config.CreateMapper();

            Source src = new Source
            {
                Item1 = "This is it",
                MyInteger = 2
            };

            //src.SetItem1("This is it");

            dynamic originalDest = CreateTestDest();

            var newDest = mapper.Map<Source, Destination>(src, originalDest);
            //newDest.GetIt("Item1", typeof(string)).ShouldBe("This is it");
            //newDest.GetIt("MyInteger", typeof(int)).ShouldBe(2);

            //Destination destinationUsedAsSource = new Destination();
            ////destinationUsedAsSource.SetItWithType("Item1", typeof(string), "This is the initial value of src2.Item1");
            ////destinationUsedAsSource.SetItWithType("MyInteger", typeof(int), -1);

            //var newSource = mapper.Map<Destination, Source>(destinationUsedAsSource);
            //newSource.Item1.ShouldBe("This is the initial value of src2.Item1");
            //newSource.MyInteger.ShouldBe(-1);
        }
        

    }
#endif



}

