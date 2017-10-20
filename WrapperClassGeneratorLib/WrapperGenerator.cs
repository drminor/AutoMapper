namespace DRMWrapperClassGenLib
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Text.RegularExpressions;

    using DRM.TypeSafePropertyBag;


    public static class WrapperGenerator
    {
        private static readonly byte[] privateKey =
            StringToByteArray(
                "002400000480000094000000060200000024000052534131000400000100010079dfef85ed6ba841717e154f13182c0a6029a40794a6ecd2886c7dc38825f6a4c05b0622723a01cd080f9879126708eef58f134accdc99627947425960ac2397162067507e3c627992aa6b92656ad3380999b30b5d5645ba46cc3fcc6a1de5de7afebcf896c65fb4f9547a6c0c6433045fceccb1fa15e960d519d0cd694b29a4");

        private static readonly byte[] privateKeyToken = StringToByteArray("be96cd2c38ef1005");

        //private static readonly MethodInfo delegate_Combine = typeof(Delegate).GetDeclaredMethod("Combine", new[] { typeof(Delegate), typeof(Delegate) });

        //private static readonly MethodInfo delegate_Remove = typeof(Delegate).GetDeclaredMethod("Remove", new[] { typeof(Delegate), typeof(Delegate) });

        private static Type _typeDefsType = typeof(IDictionary<string, Type>);
        private static Type _ITypeSafePropBagType = typeof(ITypeSafePropBag);
        private static Type _namedValueType = typeof(IEnumerable<KeyValuePair<string, ValPlusType>>);

        // Constructor that take just a TypeDefs dictionary.
        private static readonly ConstructorInfo proxyBase_ctor1 =
            typeof(TypeSafePropBagBase).GetDeclaredConstructor(new Type[] { _typeDefsType });

        //// Constructor that take just a ITypeSafePropBag
        //private static readonly ConstructorInfo proxyBase_ctor2 =
        //    typeof(TypeSafePropBagBase).GetDeclaredConstructor(new Type[] { _ITypeSafePropBagType, _typeDefsType });

        // Constructor that take just a list of string / ValPlusType KeyValuePairs.
        private static readonly ConstructorInfo proxyBase_ctor3 =
            typeof(TypeSafePropBagBase).GetDeclaredConstructor(new Type[] { _namedValueType, _typeDefsType });



        private static readonly ModuleBuilder proxyModule = CreateProxyModule();

        private static ModuleBuilder CreateProxyModule()
        {
            AssemblyName name = new AssemblyName("DRM.WrapperGenerator.Proxies");
            name.SetPublicKey(privateKey);
            name.SetPublicKeyToken(privateKeyToken);

#if NET40
            AssemblyBuilder builder = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
#else
            AssemblyBuilder builder = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
#endif

            return builder.DefineDynamicModule("Propbag.Proxies.emit");
        }

        public static Type EmitWrapper<T>(TypeDescription<T> typeDescription) where T: ITypeSafePropBag
        {
            var interfaceType = _ITypeSafePropBagType;
            var additionalProperties = typeDescription.AdditionalProperties;
            //var propertyNames = string.Join("_", additionalProperties.Select(p => p.Name));
            //string propertyNames = "Temp_Test";

            string name = "Temp_Test";
                //$"Proxy{propertyNames}<{Regex.Replace(interfaceType.AssemblyQualifiedName ?? interfaceType.FullName ?? interfaceType.Name, @"[\s,]+", "_")}>";

            var allInterfaces = new List<Type> { interfaceType };
            allInterfaces.AddRange(interfaceType.GetTypeInfo().ImplementedInterfaces);
            Debug.WriteLine(name, "Emitting Clas Wrapper for type");

            TypeBuilder typeBuilder = proxyModule.DefineType(name,
                TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Public, typeof(TypeSafePropBagBase),
                interfaceType.IsInterface() ? new[] { interfaceType } : new Type[0]);

            ConstructorBuilder constructorBuilder1 = typeBuilder.DefineConstructor(MethodAttributes.Public,
                CallingConventions.Standard, new Type[] { _typeDefsType });

            //ConstructorBuilder constructorBuilder2 = typeBuilder.DefineConstructor(MethodAttributes.Public,
            //    CallingConventions.Standard, new Type[] { _ITypeSafePropBagType, _typeDefsType });

            ConstructorBuilder constructorBuilder3 = typeBuilder.DefineConstructor(MethodAttributes.Public,
                CallingConventions.Standard, new Type[] { _namedValueType, _typeDefsType });

            BuildConstructor(constructorBuilder1, proxyBase_ctor1);
            //BuildConstructor(constructorBuilder2, proxyBase_ctor2);
            BuildConstructor(constructorBuilder3, proxyBase_ctor3);


            var propertiesToImplement = new List<PropertyDescription>();

            // first we collect all properties, those with setters before getters in order to enable less specific redundant getters
            foreach(var property in additionalProperties)
            {
                if(property.CanWrite)
                {
                    propertiesToImplement.Insert(0, property);
                }
                else
                {
                    propertiesToImplement.Add(property);
                }
            }

            var fieldBuilders = new Dictionary<string, PropertyEmitter>();
            foreach(var property in propertiesToImplement)
            {
                if(fieldBuilders.TryGetValue(property.Name, out PropertyEmitter propertyEmitter))
                {
                    if((propertyEmitter.PropertyType != property.Type) &&
                        ((property.CanWrite) || (!property.Type.IsAssignableFrom(propertyEmitter.PropertyType))))
                    {
                        throw new ArgumentException(
                            $"The interface has a conflicting property {property.Name}",
                            nameof(interfaceType));
                    }
                }
                else
                {
                    propertyEmitter = new PropertyEmitter(typeBuilder, property); //, propertyChangedField));
                    fieldBuilders.Add(property.Name, propertyEmitter);

                }
            }
            return typeBuilder.CreateType();
        }

        private static void BuildConstructor(ConstructorBuilder cb, ConstructorInfo ci)
        {
            ILGenerator ctorIl = cb.GetILGenerator();
            ctorIl.Emit(OpCodes.Ldarg_0);
            ctorIl.Emit(OpCodes.Ldarg_1);
            ctorIl.Emit(OpCodes.Call, ci);
            ctorIl.Emit(OpCodes.Ret);
        }

        private static byte[] StringToByteArray(string hex)
        {
            int numberChars = hex.Length;
            byte[] bytes = new byte[numberChars / 2];
            for(int i = 0; i < numberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
    }


    public static class TypeExtension
    {
        public static bool IsInterface(this Type type) => type.GetTypeInfo().IsInterface;

        public static IEnumerable<MethodInfo> GetDeclaredMethods(this Type type) => type.GetTypeInfo().DeclaredMethods;

        public static MethodInfo GetDeclaredMethod(this Type type, string name) => type.GetAllMethods().FirstOrDefault(mi => mi.Name == name);

        public static MethodInfo GetDeclaredMethod(this Type type, string name, Type[] parameters) =>
                type.GetAllMethods().Where(mi => mi.Name == name).MatchParameters(parameters);

        public static IEnumerable<ConstructorInfo> GetDeclaredConstructors(this Type type) => type.GetTypeInfo().DeclaredConstructors;

        public static ConstructorInfo GetDeclaredConstructor(this Type type, Type[] parameters) =>
            type.GetDeclaredConstructors().MatchParameters(parameters);

        public static IEnumerable<MethodInfo> GetAllMethods(this Type type) => type.GetRuntimeMethods();


        private static TMethod MatchParameters<TMethod>(this IEnumerable<TMethod> methods, Type[] parameters) where TMethod : MethodBase =>
            methods.FirstOrDefault(mi => mi.GetParameters().Select(pi => pi.ParameterType).SequenceEqual(parameters));

    }
}