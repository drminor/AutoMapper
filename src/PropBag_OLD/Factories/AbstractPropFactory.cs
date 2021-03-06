﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Reflection;
using System.ComponentModel;
using System.Globalization;

namespace DRM.PropBag
{
    public abstract class AbstractPropFactory
    {
        public abstract bool ProvidesStorage { get; }

        public abstract IProp<T> Create<T>(
            T initialValue,
            string propertyName, object extraInfo = null,
            bool hasStorage = true, bool typeIsSolid = true,
            Action<T, T> doWhenChanged = null, bool doAfterNotify = false, Func<T,T,bool> comparer = null);

        public abstract IProp<T> CreateWithNoValue<T>(
            string propertyName, object extraInfo = null,
            bool hasStorage = true, bool typeIsSolid = true,
            Action<T, T> doWhenChanged = null, bool doAfterNotify = false, Func<T,T,bool> comparer = null);

        public abstract IPropGen CreateGenFromObject(Type typeOfThisProperty,
            object value,
            string propertyName, object extraInfo,
            bool hasStorage, bool isTypeSolid,
            Delegate doWhenChanged, bool doAfterNotify, Delegate comparer, bool useRefEquality = false);

        public abstract IPropGen CreateGenFromString(Type typeOfThisProperty,
            string value, bool useDefault,
            string propertyName, object extraInfo,
            bool hasStorage, bool isTypeSolid,
            Delegate doWhenChanged, bool doAfterNotify, Delegate comparer, bool useRefEquality = false);

        public abstract IPropGen CreateGenWithNoValue(Type typeOfThisProperty,
            string propertyName, object extraInfo,
            bool hasStorage, bool isTypeSolid,
            Delegate doWhenChanged, bool doAfterNotify, Delegate comparer, bool useRefEquality = false);
        
        public virtual IPropGen CreatePropInferType(object value, string propertyName, object extraInfo, bool hasStorage)
        {
            System.Type typeOfThisValue;
            bool typeIsSolid;

            if (value == null)
            {
                typeOfThisValue = typeof(object);
                typeIsSolid = false;
            }
            else
            {
                // TODO, we probably need to be more creative when determining the type of this new value.
                typeOfThisValue = value.GetType();
                typeIsSolid = true;
            }

            IPropGen prop = this.CreateGenFromObject(typeOfThisValue, value, propertyName, extraInfo, 
                hasStorage, typeIsSolid, null, false, null, false);
            return prop;
        }

        public virtual Func<T,T,bool> GetRefEqualityComparer<T>()
        {
            var y = RefEqualityComparer<T>.Default;

            DRM.PropBag.RefEqualityComparer<T> x = RefEqualityComparer<T>.Default;

            Func<T, T, bool> result = x.Equals;
            return x.Equals; // result;
            //return RefEqualityComparer<T>.Default;
        }

        public virtual T GetDefaultValue<T>()
        {
            return default(T);
        }

        public virtual T GetValueFromObject<T>(object value)
        {
            if (value == null)
            {
                if (typeof(T).IsValueType) throw new InvalidCastException("Cannot set an object that have a ValueType to null.");
                return (T)(object)null;
            }

            // value is already of the correct type.
            if (typeof(T) == typeof(object)) return (T)(object)value;

            Type s = value.GetType();

            // value is already of the correct type.
            if (s == typeof(T)) return (T)(object)value;

            object parameter = new ControlModel.TwoTypes(s, typeof(T));

            return (T)PropFactoryValueConverter.ConvertBack(value, typeof(T), parameter, CultureInfo.CurrentCulture);
        }

        public virtual T GetValueFromString<T>(string value)
        {
            if (typeof(T) == typeof(string)) return (T)(object)value;

            Type s = typeof(string);
            object parameter = new ControlModel.TwoTypes(typeof(string), typeof(T));

            return (T) PropFactoryValueConverter.ConvertBack(value, typeof(T), parameter, CultureInfo.CurrentCulture);
        }


        #region Shared Delegate Creation Logic

        static private Type gmtType = typeof(APFGenericMethodTemplates);

        protected virtual CreatePropFromObjectDelegate GetPropCreator(Type typeOfThisValue)
        {
            MethodInfo mi = gmtType.GetMethod("CreatePropFromObject", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(typeOfThisValue);
            CreatePropFromObjectDelegate result = (CreatePropFromObjectDelegate)Delegate.CreateDelegate(typeof(CreatePropFromObjectDelegate), mi);

            return result;
        }

        protected virtual CreatePropFromStringDelegate GetPropFromStringCreator(Type typeOfThisValue)
        {
            MethodInfo mi = gmtType.GetMethod("CreatePropFromString", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(typeOfThisValue);
            CreatePropFromStringDelegate result = (CreatePropFromStringDelegate)Delegate.CreateDelegate(typeof(CreatePropFromStringDelegate), mi);

            return result;
        }

        protected virtual  CreatePropWithNoValueDelegate GetPropWithNoValueCreator(Type typeOfThisValue)
        {
            MethodInfo mi = gmtType.GetMethod("CreatePropWithNoValue", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(typeOfThisValue);
            CreatePropWithNoValueDelegate result = (CreatePropWithNoValueDelegate)Delegate.CreateDelegate(typeof(CreatePropWithNoValueDelegate), mi);

            return result;
        }

        #endregion
    }

    static class APFGenericMethodTemplates
    {
        private static IProp<T> CreatePropFromObject<T>(AbstractPropFactory propFactory,
            object value,
            string propertyName, object extraInfo,
            bool hasStorage, bool isTypeSolid,
            Delegate doWhenChanged, bool doAfterNotify, Delegate comparer, bool useRefEquality = false)
        {
            Func<T,T,bool> compr = useRefEquality ? propFactory.GetRefEqualityComparer<T>() : (Func<T,T,bool>)comparer;

            T initVal = propFactory.GetValueFromObject<T>(value);

            return propFactory.Create<T>(initVal, propertyName, extraInfo, hasStorage, isTypeSolid,
                (Action<T,T>) doWhenChanged, doAfterNotify, compr);
        }

        private static IProp<T> CreatePropFromString<T>(AbstractPropFactory propFactory,
            string value, bool useDefault,
            string propertyName, object extraInfo,
            bool hasStorage, bool isTypeSolid,
            Delegate doWhenChanged, bool doAfterNotify, Delegate comparer, bool useRefEquality = false)
        {
            Func<T, T, bool> compr = useRefEquality ? propFactory.GetRefEqualityComparer<T>() : (Func<T, T, bool>)comparer;

            T initVal;
            if (useDefault)
            {
                initVal = propFactory.GetDefaultValue<T>();
            }
            else
            {
                initVal = propFactory.GetValueFromString<T>(value);
            }

            return propFactory.Create<T>(initVal, propertyName, extraInfo, hasStorage, isTypeSolid,
                (Action<T, T>)doWhenChanged, doAfterNotify, compr);
        }

        public static IProp<T> CreatePropWithNoValue<T>(AbstractPropFactory propFactory,
            string propertyName, object extraInfo,
            bool hasStorage, bool isTypeSolid,
            Delegate doWhenChanged, bool doAfterNotify, Delegate comparer, bool useRefEquality = false)
        {
            Func<T,T,bool> compr = useRefEquality ? propFactory.GetRefEqualityComparer<T>() : (Func<T,T,bool>)comparer;

            return propFactory.CreateWithNoValue<T>(propertyName, extraInfo, hasStorage, isTypeSolid, 
                (Action<T, T>)doWhenChanged, doAfterNotify, compr);
        }

    }


}
