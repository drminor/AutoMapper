using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace AutoMapper.Internal
{
    using Configuration;
    using System.Threading;

    public static class ReflectionHelper
    { 
        //static Lazy<MethodInfo> _getMemberValueMethod;
        //static Lazy<MethodInfo> _setMemberValueMethod;

        static Lazy<MethodInfo> _getPropertyValueMethod;
        static Lazy<MethodInfo> _setPropertyValueMethod;

        static ReflectionHelper()
        {
            //_getMemberValueMethod = new Lazy<MethodInfo>(() => GetTheMemberValueMethod(), LazyThreadSafetyMode.PublicationOnly);
            //_setMemberValueMethod = new Lazy<MethodInfo>(() => GetTheSetMemberValueMethod(), LazyThreadSafetyMode.PublicationOnly);

            _getPropertyValueMethod = new Lazy<MethodInfo>(() => GetThePropertyValueMethod(), LazyThreadSafetyMode.PublicationOnly);
            _setPropertyValueMethod = new Lazy<MethodInfo>(() => GetTheSetPropertyValueMethod(), LazyThreadSafetyMode.PublicationOnly);

        }

        public static bool CanBeSet(MemberInfo propertyOrField)
        {
            return propertyOrField is FieldInfo field ? 
                        !field.IsInitOnly : 
                        ((PropertyInfo)propertyOrField).CanWrite;
        }

        public static object GetDefaultValue(ParameterInfo parameter)
        {
            if (parameter.DefaultValue == null && parameter.ParameterType.IsValueType())
            {
                return Activator.CreateInstance(parameter.ParameterType);
            }
            return parameter.DefaultValue;
        }

        public static object MapMember(ResolutionContext context, MemberInfo member, object value, object destination)
        {
            var memberType = GetMemberType(member);
            var destValue = GetMemberValue(member, destination);
            return context.Mapper.Map(value, destValue, value?.GetType() ?? memberType, memberType, context);
        }

        public static object MapMember(ResolutionContext context, MemberInfo member, object value)
        {
            var memberType = GetMemberType(member);
            return context.Mapper.Map(value, null, value?.GetType() ?? memberType, memberType, context);
        }

        public static bool IsDynamic(object obj) => obj is IDynamicMetaObjectProvider;

        public static bool IsDynamic(Type type) => typeof(IDynamicMetaObjectProvider).IsAssignableFrom(type);

        public static void SetMemberValue(MemberInfo propertyOrField, object target, object value)
        {
            if (propertyOrField is PropertyInfo property)
            {
                property.SetValue(target, value, null);
                return;
            }
            if (propertyOrField is FieldInfo field)
            {
                field.SetValue(target, value);
                return;
            }
            throw Expected(propertyOrField);
        }

        private static ArgumentOutOfRangeException Expected(MemberInfo propertyOrField)
            => new ArgumentOutOfRangeException(nameof(propertyOrField), "Expected a property or field, not " + propertyOrField);

        private static ArgumentOutOfRangeException ExpectedProperty(MemberInfo propertyOrField)
            => new ArgumentOutOfRangeException(nameof(propertyOrField), "Expected a property, not " + propertyOrField);


        public static object GetMemberValue(MemberInfo propertyOrField, object target)
        {
            if (propertyOrField is PropertyInfo property)
            {
                return property.GetValue(target, null);
            }
            if (propertyOrField is FieldInfo field)
            {
                return field.GetValue(target);
            }
            throw Expected(propertyOrField);
        }

        public static object GetPropertyValue(MemberInfo propertyOrField, object target, object[] index)
        {
            if (propertyOrField is PropertyInfo property)
            {
                return property.GetValue(target, index);
            }
            throw ExpectedProperty(propertyOrField);
        }

        public static void SetPropertyValue(MemberInfo propertyOrField, object target, object value, object[] index)
        {
            if (propertyOrField is PropertyInfo property)
            {
                property.SetValue(target, value, index);
                return;
            }
            throw ExpectedProperty(propertyOrField);
        }

        public static IEnumerable<MemberInfo> GetMemberPath(Type type, string fullMemberName)
        {
            MemberInfo property = null;
            foreach (var memberName in fullMemberName.Split('.'))
            {
                var currentType = GetCurrentType(property, type);
                yield return property = currentType.GetFieldOrProperty(memberName);
            }
        }

        private static Type GetCurrentType(MemberInfo member, Type type)
        {
            var memberType = member?.GetMemberType() ?? type;
            if (memberType.IsGenericType() && typeof(IEnumerable).IsAssignableFrom(memberType))
            {
                memberType = memberType.GetTypeInfo().GenericTypeArguments[0];
            }
            return memberType;
        }

        public static MemberInfo GetFieldOrProperty(LambdaExpression expression)
        {
            var memberExpression = expression.Body as MemberExpression;
            return memberExpression != null
                ? memberExpression.Member
                : throw new ArgumentOutOfRangeException(nameof(expression), "Expected a property/field access expression, not " + expression);
        }

        public static MemberInfo FindProperty(LambdaExpression lambdaExpression)
        {
            Expression expressionToCheck = lambdaExpression;

            var done = false;

            while (!done)
            {
                switch (expressionToCheck.NodeType)
                {
                    case ExpressionType.Convert:
                        expressionToCheck = ((UnaryExpression)expressionToCheck).Operand;
                        break;
                    case ExpressionType.Lambda:
                        expressionToCheck = ((LambdaExpression)expressionToCheck).Body;
                        break;
                    case ExpressionType.MemberAccess:
                        var memberExpression = ((MemberExpression)expressionToCheck);

                        if (memberExpression.Expression.NodeType != ExpressionType.Parameter &&
                            memberExpression.Expression.NodeType != ExpressionType.Convert)
                        {
                            throw new ArgumentException(
                                $"Expression '{lambdaExpression}' must resolve to top-level member and not any child object's properties. You can use ForPath, a custom resolver on the child type or the AfterMap option instead.",
                                nameof(lambdaExpression));
                        }

                        var member = memberExpression.Member;

                        return member;
                    default:
                        done = true;
                        break;
                }
            }

            throw new AutoMapperConfigurationException(
                "Custom configuration for members is only supported for top-level individual members on a type.");
        }

        public static Type GetMemberType(MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case MethodInfo mInfo:
                    return mInfo.ReturnType;
                case PropertyInfo pInfo:
                    return pInfo.PropertyType;
                case FieldInfo fInfo:
                    return fInfo.FieldType;
                case null:
                    throw new ArgumentNullException(nameof(memberInfo));
                default:
                    throw new ArgumentOutOfRangeException(nameof(memberInfo));
            }
        }

        /// <summary>
        /// if targetType is oldType, method will return newType
        /// if targetType is not oldType, method will return targetType
        /// if targetType is generic type with oldType arguments, method will replace all oldType arguments on newType
        /// </summary>
        /// <param name="targetType"></param>
        /// <param name="oldType"></param>
        /// <param name="newType"></param>
        /// <returns></returns>
        public static Type ReplaceItemType(Type targetType, Type oldType, Type newType)
        {
            if (targetType == oldType)
                return newType;

            if (targetType.IsGenericType())
            {
                var genSubArgs = targetType.GetTypeInfo().GenericTypeArguments;
                var newGenSubArgs = new Type[genSubArgs.Length];
                for (var i = 0; i < genSubArgs.Length; i++)
                    newGenSubArgs[i] = ReplaceItemType(genSubArgs[i], oldType, newType);
                return targetType.GetGenericTypeDefinition().MakeGenericType(newGenSubArgs);
            }

            return targetType;
        }

        //public static MethodInfo GetMemberValueMethod
        //{
        //    get
        //    {
        //        return _getMemberValueMethod.Value;
        //    }
        //}

        //public static MethodInfo SetMemberValueMethod
        //{
        //    get
        //    {
        //        return _setMemberValueMethod.Value;
        //    }
        //}

        public static MethodInfo GetPropertyValueMethod
        {
            get
            {
                return _getPropertyValueMethod.Value;
            }
        }

        public static MethodInfo SetPropertyValueMethod
        {
            get
            {
                return _setPropertyValueMethod.Value;
            }
        }

        private static MethodInfo GetTheMemberValueMethod()
        {
#if !NET40 && !NET45
            return typeof(AutoMapper.Internal.ReflectionHelper).GetDeclaredMethod("GetMemberValue");
#else
            return typeof(AutoMapper.Internal.ReflectionHelper).GetMethod("GetMemberValue", BindingFlags.Static | BindingFlags.Public);
#endif
        }

        private static MethodInfo GetTheSetMemberValueMethod()
        {
#if !NET40 && !NET45
            return typeof(AutoMapper.Internal.ReflectionHelper).GetDeclaredMethod("SetMemberValue");
#else
            return typeof(AutoMapper.Internal.ReflectionHelper).GetMethod("SetMemberValue", BindingFlags.Static | BindingFlags.Public);
#endif
        }

        private static MethodInfo GetThePropertyValueMethod()
        {
#if !NET40 && !NET45
            return typeof(AutoMapper.Internal.ReflectionHelper).GetDeclaredMethod("GetPropertyValue");
#else
            return typeof(AutoMapper.Internal.ReflectionHelper).GetMethod("GetPropertyValue", BindingFlags.Static | BindingFlags.Public);
#endif
        }

        private static MethodInfo GetTheSetPropertyValueMethod()
        {
#if !NET40 && !NET45
            return typeof(AutoMapper.Internal.ReflectionHelper).GetDeclaredMethod("SetPropertyValue");
#else
            return typeof(AutoMapper.Internal.ReflectionHelper).GetMethod("SetPropertyValue", BindingFlags.Static | BindingFlags.Public);
#endif
        }

        public static bool IsFieldOrProperty(MemberInfo memberInfo)
        {
            return memberInfo is PropertyInfo || memberInfo is FieldInfo;
        }
    }
}
