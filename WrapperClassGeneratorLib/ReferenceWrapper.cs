using DRM.TypeSafePropertyBag;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace DRMWrapperClassGenLib
{
    public class ReferenceWrapper : TypeSafePropBagBase
    {

        private static readonly MethodInfo ProxBaseGetVal =
            typeof(TypeSafePropBagBase).GetTypeInfo().DeclaredMethods.Single(m => m.Name == "GetVal");

        private static readonly MethodInfo ProxBaseSetValNT =
            typeof(TypeSafePropBagBase).GetTypeInfo().DeclaredMethods.Single(m => m.Name == "SetValWithNoType");

        Type sType;

        public ReferenceWrapper(Dictionary<string, Type> typeDefs) : base(typeDefs)
        {
            sType = typeof(string);
        }

        public string GetPropString(MethodBuilder getter, string name)
        {
            return (string)base.GetItWithNoType(name);
            //object val = base.GetVal(name);
            //string rVal = (string)val;
            //return rVal;
        }

        public void SetPropString(MethodBuilder setter, string name, Type pType, object value)
        {
            base.SetItWithType(value, pType, name);
        }

        public void SetPropString2(MethodBuilder setter, string name, object value)
        {
            base.SetItWithType(value, sType, name);
        }

        string _propString = "dummy";
        public string PropString
        {
            get
            {
                string _ourName = "PropString";
                Type _ourType = this.PropString.GetType();

                //object val = ProxBaseGetVal.Invoke(null, new object[] { _ourName });

                //ValPlusType r = NamedValuesWithType[_ourName];
                //object val = r.Value;
                object val = base.GetItWithNoType(_ourName);
                string rVal = (string)val;
                return rVal;

                //string _ourName = "PropString";
                //return (string) base.NamedValuesWithType[_ourName].Value;
            }
            set
            {
                string _ourName = "PropString";
                Type _ourType = this._propString.GetType();

                base.SetItWithType(value, _ourType, _ourName);

                //ProxBaseSetVal.Invoke(null, new object[] { _ourName, value, _ourType });
                //ValPlusType n = new ValPlusType(value, _ourType);
                //base.NamedValuesWithType.Add(_ourName, n);

                //base.NamedValuesWithType.Add("PropString", new ValPlusType(value, typeof(String)));
            }
        }

        public string PropString2
        {
            get
            {
                return (string) base.GetItWithNoType("PropString");
            }
            set
            {
                Type tt = PropString2.GetType();
                base.SetItWithNoType(value, "PropString2");
            }
        }

        public int PropInt
        {
            get
            {
                string _ourName = "PropInt";
                object val = base.GetItWithNoType(_ourName);
                int rVal = (int)val;
                return rVal;
            }
            set
            {
                string _ourName = "PropInt";
                Type _ourType = typeof(Int32);
                base.SetItWithType(value, _ourType, _ourName);
            }
        }
    }
}
